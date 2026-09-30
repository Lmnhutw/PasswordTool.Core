using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PasswordTool.Core.Models;
using PasswordTool.Presentation;
using QRCoder;
using Windows.Storage.Streams;

namespace PasswordTool_WinUI;

public sealed partial class MainPage : Page
{
    public ShellViewModel ViewModel { get; } = App.Services.GetRequiredService<ShellViewModel>();
    private readonly ISystemLockMonitor systemLockMonitor = App.Services.GetRequiredService<ISystemLockMonitor>();
    private readonly ISensitiveClipboardService sensitiveClipboard = App.Services.GetRequiredService<ISensitiveClipboardService>();
    private readonly IUserDialogService dialogs = App.Services.GetRequiredService<IUserDialogService>();
    private readonly PasswordGeneratorDialogService passwordGeneratorDialog = App.Services.GetRequiredService<PasswordGeneratorDialogService>();
    private int lifecycleLockInProgress;
    private Guid? editingItemId;
    private string preservedTotpSecret = string.Empty;
    private AuthenticatorSetup? settingsAuthenticatorSetup;
    private bool showingTrash;

    public MainPage()
    {
        InitializeComponent();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        Loaded += async (_, _) =>
        {
            systemLockMonitor.LockRequired += SystemLockMonitor_LockRequired;
            ApplyShellState();
            FocusCurrentAuthenticationStep();
        };
        Unloaded += (_, _) =>
        {
            systemLockMonitor.LockRequired -= SystemLockMonitor_LockRequired;
            systemLockMonitor.Stop();
        };
    }

    public static Visibility BoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility InvertBoolToVisibility(bool value) => value ? Visibility.Collapsed : Visibility.Visible;
    public static HorizontalAlignment NotesRevealAlignment(bool hideNotes) => hideNotes ? HorizontalAlignment.Center : HorizontalAlignment.Right;
    public static string ItemAutomationId(string action, Guid id) => $"{action}_{id:N}";
    public static string GroupAutomationId(Guid? id) => id is null ? "Group_Ungrouped" : $"Group_{id:N}";
    public static string GroupChevron(bool expanded) => expanded ? "\uE70D" : "\uE76C";
    public static Brush GroupBrush(string? value) => string.IsNullOrWhiteSpace(value)
        ? (Brush)Application.Current.Resources["VaultGroupBackgroundBrush"]
        : new SolidColorBrush(Windows.UI.Color.FromArgb(255,
            Convert.ToByte(value.Substring(1, 2), 16),
            Convert.ToByte(value.Substring(3, 2), 16),
            Convert.ToByte(value.Substring(5, 2), 16)));
    public static Brush GroupTextBrush(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
        var r = Convert.ToByte(value.Substring(1, 2), 16);
        var g = Convert.ToByte(value.Substring(3, 2), 16);
        var b = Convert.ToByte(value.Substring(5, 2), 16);
        return new SolidColorBrush((r * 299 + g * 587 + b * 114) / 1000 >= 140
            ? Windows.UI.Color.FromArgb(255, 0, 0, 0)
            : Windows.UI.Color.FromArgb(255, 255, 255, 255));
    }

    private void VaultRow_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DependencyObject row) return;
        ApplyVaultIconHover(row);
    }

    private static void ApplyVaultIconHover(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Button button)
            {
                button.Padding = new Thickness(0);
                button.PointerEntered -= VaultIconButton_PointerEntered;
                button.PointerExited -= VaultIconButton_PointerExited;
                button.PointerEntered += VaultIconButton_PointerEntered;
                button.PointerExited += VaultIconButton_PointerExited;
                continue;
            }

            ApplyVaultIconHover(child);
        }
    }

    private static void VaultIconButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Button button) return;
        button.BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
    }

    private static void VaultIconButton_PointerExited(object sender, PointerRoutedEventArgs e) =>
        ((Button)sender).BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    private void GroupTab_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button { Tag: VaultItemGroup { IsExpanded: false } } button)
            button.Background = (Brush)Application.Current.Resources["VaultGroupHoverBrush"];
    }

    private void GroupTab_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button { Tag: VaultItemGroup group } button)
            button.Background = GroupBrush(group.AccentColor);
    }

    private async void UnlockButton_Click(object sender, RoutedEventArgs e)
    {
        if (!UnlockButton.IsEnabled) return;
        UnlockButton.IsEnabled = false;
        try
        {
            var password = MasterPasswordInput.Password;
            if (!await ViewModel.ValidateMasterPasswordAsync(password)) return;
            var code = await dialogs.PromptTotpAsync(
                "Unlock vault", "Enter the current 6-digit code from Google Authenticator.");
            if (code is null) return;

            await ViewModel.UnlockAsync(password, code);

            if (ViewModel.IsUnlocked)
            {
                MasterPasswordInput.Password = string.Empty;
            }
            ApplyShellState();
            if (ViewModel.IsUnlocked) systemLockMonitor.Start(ViewModel.InactivityTimeout, ViewModel.VaultOpenDuration);
        }
        finally
        {
            UnlockButton.IsEnabled = true;
        }
    }

    private void MasterPasswordInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            UnlockButton_Click(UnlockButton, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private async void ShellNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var selectedItem = args.SelectedItemContainer ?? args.SelectedItem as NavigationViewItem;
        if (selectedItem?.Tag is not string tag) return;
        if (Enum.TryParse<AppRoute>(tag, out var route))
        {
            showingTrash = false;
            ViewModel.Navigate(route);
            if (route == AppRoute.Settings) await ViewModel.Settings.LoadAsync();
            if (route == AppRoute.Backup) await ViewModel.Backup.LoadAsync();
            ApplyRoute(route);
        }
    }

    private async void LockVaultButton_Click(object sender, RoutedEventArgs e)
    {
        App.Services.GetRequiredService<DialogLifetime>().DismissAll();
        systemLockMonitor.Stop();
        await ViewModel.LockCommand.ExecuteAsync(null);
        showingTrash = false;
        ClearEditor();
        ClearSettingsInputs();
        ClearBackupInputs();
        ApplyShellState();
        MasterPasswordInput.Focus(FocusState.Programmatic);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.IsUnlocked) or nameof(ShellViewModel.FlowState)) ApplyShellState();
    }

    private void CreateVaultButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.BeginNewVault();
        ApplyShellState();
        FocusCurrentAuthenticationStep();
    }

    private void RecoverVaultButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.BeginRecovery();
        ApplyShellState();
    }

    private async void ChooseRecoveryButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.SelectRecoveryFileAsync();

    private async void InspectRecoveryButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.InspectRecoveryAsync(RecoveryPassphraseInput.Password);
        RecoverySummaryInfoBar.IsOpen = !string.IsNullOrWhiteSpace(ViewModel.RecoverySummary);
    }

    private void ContinueRecoveryButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ContinueRecovery();
        ApplyShellState();
        FocusCurrentAuthenticationStep();
    }

    private async void ContinueMasterPasswordButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.PrepareAuthenticator(NewMasterPasswordInput.Password, ConfirmMasterPasswordInput.Password);
        ApplyShellState();
        if (ViewModel.FlowState == AppFlowState.SetupAuthenticator && ViewModel.PendingAuthenticatorSetup is { } setup)
        {
            AuthenticatorSecretText.Text = setup.SecretBase32;
            AuthenticatorQrImage.Source = await CreateQrBitmapAsync(setup.OtpAuthUri);
            AuthenticatorConfirmationInput.FocusFirst();
        }
    }

    private async void CompleteAuthenticatorButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.CompleteAuthenticatorSetupAsync(
            NewMasterPasswordInput.Password,
            RecoveryPassphraseInput.Password,
            AuthenticatorConfirmationInput.Code);
        ApplyShellState();
        if (!ViewModel.IsUnlocked) return;

        ClearFirstLaunchInputs();
        systemLockMonitor.Start(ViewModel.InactivityTimeout, ViewModel.VaultOpenDuration);
    }

    private async void CopyAuthenticatorSecretButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.PendingAuthenticatorSetup is { } setup) await sensitiveClipboard.CopyAsync(setup.SecretBase32);
    }

    private void BackToWelcomeButton_Click(object sender, RoutedEventArgs e)
    {
        ClearFirstLaunchInputs();
        ViewModel.CancelFirstLaunchStep();
        ApplyShellState();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e) => Application.Current.Exit();

    private void SensitiveClipboardKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (FocusManager.GetFocusedElement(XamlRoot) is FrameworkElement { Tag: "SensitiveInput" })
            args.Handled = true;
    }

    private void SensitiveTextBox_ContextMenuOpening(object sender, ContextMenuEventArgs e) => e.Handled = true;

    private void SystemLockMonitor_LockRequired(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            if (!ViewModel.IsUnlocked || Interlocked.Exchange(ref lifecycleLockInProgress, 1) != 0) return;
            try
            {
                systemLockMonitor.Stop();
                App.Services.GetRequiredService<DialogLifetime>().DismissAll();
                await ViewModel.LockCommand.ExecuteAsync(null);
                showingTrash = false;
                ClearEditor();
                ClearSettingsInputs();
                ClearBackupInputs();
                ApplyShellState();
                MasterPasswordInput.Focus(FocusState.Programmatic);
            }
            finally
            {
                Interlocked.Exchange(ref lifecycleLockInProgress, 0);
            }
        });
    }

    private void ApplyShellState()
    {
        AuthenticationPanel.Visibility = ViewModel.IsUnlocked ? Visibility.Collapsed : Visibility.Visible;
        ShellNavigation.Visibility = ViewModel.IsUnlocked ? Visibility.Visible : Visibility.Collapsed;
        LoadingPanel.Visibility = Visibility.Collapsed;
        PartialStoragePanel.Visibility = Visibility.Collapsed;
        FirstLaunchPanel.Visibility = Visibility.Collapsed;
        RecoveryPanel.Visibility = Visibility.Collapsed;
        CreateMasterPasswordPanel.Visibility = Visibility.Collapsed;
        SetupAuthenticatorPanel.Visibility = Visibility.Collapsed;
        UnlockPanel.Visibility = Visibility.Collapsed;

        if (!ViewModel.IsUnlocked)
        {
            App.Services.GetRequiredService<DialogLifetime>().DismissAll();
            switch (ViewModel.FlowState)
            {
                case AppFlowState.Loading:
                    LoadingPanel.Visibility = Visibility.Visible;
                    break;
                case AppFlowState.FirstLaunch:
                    FirstLaunchPanel.Visibility = Visibility.Visible;
                    break;
                case AppFlowState.Recover when ViewModel.HasPartialStorage:
                    PartialStoragePanel.Visibility = Visibility.Visible;
                    break;
                case AppFlowState.Recover:
                    RecoveryPanel.Visibility = Visibility.Visible;
                    break;
                case AppFlowState.CreateMasterPassword:
                    CreateMasterPasswordPanel.Visibility = Visibility.Visible;
                    break;
                case AppFlowState.SetupAuthenticator:
                    SetupAuthenticatorPanel.Visibility = Visibility.Visible;
                    break;
                default:
                    UnlockPanel.Visibility = Visibility.Visible;
                    break;
            }
        }

        ApplyRoute(ViewModel.CurrentRoute);
    }

    private void FocusCurrentAuthenticationStep()
    {
        if (ViewModel.IsUnlocked) return;
        switch (ViewModel.FlowState)
        {
            case AppFlowState.Unlock:
                MasterPasswordInput.Focus(FocusState.Programmatic);
                break;
            case AppFlowState.CreateMasterPassword:
                NewMasterPasswordInput.Focus(FocusState.Programmatic);
                break;
        }
    }

    private void ClearFirstLaunchInputs()
    {
        NewMasterPasswordInput.Password = string.Empty;
        ConfirmMasterPasswordInput.Password = string.Empty;
        RecoveryPassphraseInput.Password = string.Empty;
        AuthenticatorConfirmationInput.Clear();
        AuthenticatorSecretText.Text = string.Empty;
        AuthenticatorQrImage.Source = null;
        RecoverySummaryInfoBar.IsOpen = false;
    }

    private static async Task<BitmapImage> CreateQrBitmapAsync(string value)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(value, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(data);
        var png = qrCode.GetGraphic(12);
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(png);
            await writer.StoreAsync();
            writer.DetachStream();
        }
        stream.Seek(0);
        var bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(stream);
        return bitmap;
    }

    private void ApplyRoute(AppRoute route)
    {
        VaultPage.Visibility = route == AppRoute.Vault && !showingTrash ? Visibility.Visible : Visibility.Collapsed;
        TrashPage.Visibility = route == AppRoute.Vault && showingTrash ? Visibility.Visible : Visibility.Collapsed;
        EditorPage.Visibility = route == AppRoute.ItemEditor ? Visibility.Visible : Visibility.Collapsed;
        HashToolPage.Visibility = route == AppRoute.HashTool ? Visibility.Visible : Visibility.Collapsed;
        BackupPage.Visibility = route == AppRoute.Backup ? Visibility.Visible : Visibility.Collapsed;
        SecurityCheckPage.Visibility = route == AppRoute.SecurityCheck ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = route == AppRoute.Settings ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddItemButton_Click(object sender, RoutedEventArgs e)
    {
        ClearEditor();
        ViewModel.BeginAddItem();
        ApplyRoute(AppRoute.ItemEditor);
        EditorItemTitle.Focus(FocusState.Programmatic);
    }

    private async void AddGroupButton_Click(object sender, RoutedEventArgs e)
    {
        var name = await PromptAsync("Create group", "Group name", string.Empty);
        if (name is not null) await ViewModel.CreateGroupAsync(name);
    }

    private async void VaultItems_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (sender is ListView { SelectedItem: VaultItemListItem item }) await OpenEditorAsync(item.Id);
    }

    private async void VaultItems_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter) return;
        e.Handled = true;
        if (sender is ListView { SelectedItem: VaultItemListItem item }) await OpenEditorAsync(item.Id);
    }

    private async Task OpenSelectedEditorAsync()
    {
        if (ViewModel.Vault.SelectedItem is not { } selected) return;
        await OpenEditorAsync(selected.Id);
    }

    private void GroupHeaderButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is VaultItemGroup group) ViewModel.Vault.ToggleGroup(group);
        if (sender is Button { Tag: VaultItemGroup selected } button)
            button.Background = GroupBrush(selected.AccentColor);
    }

    private async void RenameGroupMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not VaultItemGroup { Id: { } id } group) return;
        var name = await PromptAsync("Rename group", "Group name", group.Name);
        if (name is not null) await ViewModel.UpdateGroupAsync(id, name, group.AccentColor);
    }

    private async void ChangeGroupColorMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not VaultItemGroup { Id: { } id } group) return;
        var color = await PromptAsync("Change group color", "Accent color (#RRGGBB), or leave blank for default", group.AccentColor ?? string.Empty);
        if (color is not null) await ViewModel.UpdateGroupAsync(id, group.Name, color);
    }

    private async void DeleteGroupMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is VaultItemGroup { Id: { } id } group) await ViewModel.DeleteGroupAsync(id, group.Name);
    }

    private async Task<string?> PromptAsync(string title, string header, string value)
    {
        var input = new TextBox { Header = header, Text = value, MinWidth = 320 };
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = title, Content = input, PrimaryButtonText = "Save", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary };
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? input.Text : null;
    }

    private async Task OpenEditorAsync(Guid itemId)
    {
        var item = await ViewModel.GetItemForEditingAsync(itemId);
        if (item is null) return;

        editingItemId = item.Id;
        EditorTitle.Text = "Edit item";
        EditorItemTitle.Text = item.Title;
        EditorUsername.Text = item.Username;
        EditorPassword.Password = item.Password;
        EditorRecoveryCodes.Text = string.Join(Environment.NewLine, item.RecoveryCodes);
        preservedTotpSecret = item.TotpSecretBase32;
        EditorUrl.Text = item.Url;
        EditorGroup.SelectedItem = ViewModel.Vault.GroupOptions.FirstOrDefault(option => option.GroupId == item.GroupId && !option.CreatesNew) ?? ViewModel.Vault.GroupOptions[0];
        EditorTags.Text = string.Join(", ", item.Tags);
        EditorNotes.Text = item.Notes;
        EditorFavorite.IsChecked = item.IsFavorite;
        EditorHideUrl.IsChecked = item.HideUrl;
        EditorHideNotes.IsChecked = item.HideNotes;
        ViewModel.Navigate(AppRoute.ItemEditor);
        ApplyRoute(AppRoute.ItemEditor);
        EditorItemTitle.Focus(FocusState.Programmatic);
    }

    private async void SaveEditorButton_Click(object sender, RoutedEventArgs e)
    {
        var groupOption = EditorGroup.SelectedItem as VaultGroupOption ?? ViewModel.Vault.GroupOptions[0];
        if (groupOption.CreatesNew)
        {
            var name = await PromptAsync("Create group", "Group name", string.Empty);
            if (name is null) return;
            var group = await ViewModel.CreateGroupAsync(name);
            if (group is null) return;
            groupOption = ViewModel.Vault.GroupOptions.First(option => option.GroupId == group.Id);
        }
        var saved = await ViewModel.SaveItemAsync(new VaultItemEditorInput(
            editingItemId,
            EditorItemTitle.Text,
            EditorUsername.Text,
            EditorPassword.Password,
            EditorRecoveryCodes.Text,
            preservedTotpSecret,
            EditorUrl.Text,
            EditorNotes.Text,
            groupOption.GroupId,
            EditorTags.Text,
            EditorFavorite.IsChecked == true,
            EditorHideUrl.IsChecked == true,
            EditorHideNotes.IsChecked == true));
        if (!saved) return;
        ClearEditor();
        ApplyRoute(AppRoute.Vault);
    }

    private void CancelEditorButton_Click(object sender, RoutedEventArgs e)
    {
        ClearEditor();
        ViewModel.Navigate(AppRoute.Vault);
        ApplyRoute(AppRoute.Vault);
    }

    private async void GeneratePasswordButton_Click(object sender, RoutedEventArgs e)
    {
        var generated = await passwordGeneratorDialog.ShowAsync();
        if (generated is not null) EditorPassword.Password = generated;
    }

    private void ClearEditor()
    {
        editingItemId = null;
        preservedTotpSecret = string.Empty;
        EditorTitle.Text = "Add item";
        EditorItemTitle.Text = string.Empty;
        EditorUsername.Text = string.Empty;
        EditorPassword.Password = string.Empty;
        EditorRecoveryCodes.Text = string.Empty;
        EditorUrl.Text = string.Empty;
        EditorGroup.SelectedIndex = 0;
        EditorTags.Text = string.Empty;
        EditorNotes.Text = string.Empty;
        EditorFavorite.IsChecked = false;
        EditorHideUrl.IsChecked = false;
        EditorHideNotes.IsChecked = false;
    }

    private void ClearSettingsInputs()
    {
        SettingsMasterPassword.Password = string.Empty;
        CurrentMasterPassword.Password = string.Empty;
        ReplacementMasterPassword.Password = string.Empty;
        ConfirmReplacementMasterPassword.Password = string.Empty;
        AuthenticatorResetMasterPassword.Password = string.Empty;
        AuthenticatorResetCode.Clear();
        SettingsAuthenticatorSecret.Text = string.Empty;
        SettingsAuthenticatorQr.Source = null;
        SettingsAuthenticatorQr.Visibility = Visibility.Collapsed;
        settingsAuthenticatorSetup = null;
    }

    private void ClearBackupInputs()
    {
        BackupPassphrase.Password = string.Empty;
        BackupPassphraseConfirmation.Password = string.Empty;
        SnapshotMasterPassword.Password = string.Empty;
    }

    private async void CopyUsernameRowButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetItemId(sender) is { } id) await ViewModel.CopyUsernameAsync(id);
    }

    private async void RevealPasswordRowButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetItemId(sender) is { } id) await ViewModel.RevealPasswordAsync(id);
    }

    private async void CopyPasswordRowButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetItemId(sender) is { } id) await ViewModel.CopyPasswordAsync(id);
    }

    private async void RevealRecoveryCodesRowButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetItemId(sender) is { } id) await ViewModel.RevealRecoveryCodesAsync(id);
    }

    private async void RevealNotesRowButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetItemId(sender) is { } id) await ViewModel.RevealNotesAsync(id);
    }

    private async void EditRowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetItemId(sender) is { } id) await OpenEditorAsync(id);
    }

    private async void PasswordHistoryRowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetItemId(sender) is { } id) await ViewModel.ViewPasswordHistoryAsync(id);
    }

    private async void TrashRowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetItemId(sender) is { } id) await ViewModel.DeleteItemAsync(id);
    }

    private static Guid? GetItemId(object sender) => (sender as FrameworkElement)?.Tag is Guid id ? id : null;

    private async void OpenTrashButton_Click(object sender, RoutedEventArgs e)
    {
        showingTrash = true;
        await ViewModel.Trash.LoadAsync();
        ApplyRoute(AppRoute.Vault);
    }

    private void CloseTrashButton_Click(object sender, RoutedEventArgs e)
    {
        showingTrash = false;
        ApplyRoute(AppRoute.Vault);
    }

    private async void RestoreTrashButton_Click(object sender, RoutedEventArgs e) => await ViewModel.Trash.RestoreSelectedAsync();
    private async void DeleteTrashPermanentlyButton_Click(object sender, RoutedEventArgs e) => await ViewModel.Trash.PermanentlyDeleteSelectedAsync();

    private void SearchKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!ViewModel.IsUnlocked || ViewModel.CurrentRoute != AppRoute.Vault) return;
        VaultSearchInput.Focus(FocusState.Programmatic);
        VaultSearchInput.SelectAll();
        args.Handled = true;
    }

    private void NewItemKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!ViewModel.IsUnlocked) return;
        AddItemButton_Click(sender, new RoutedEventArgs());
        args.Handled = true;
    }

    private void GenerateHashButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.HashTool.GenerateCommand.Execute(HashGeneratePassword.Password);
        HashGeneratePassword.Password = string.Empty;
        HashErrorInfoBar.IsOpen = !string.IsNullOrWhiteSpace(ViewModel.HashTool.ErrorMessage);
    }

    private void VerifyHashButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.HashTool.VerifyCommand.Execute(new HashVerificationRequest(HashVerifyPassword.Password, HashVerifyStored.Text));
        HashVerifyPassword.Password = string.Empty;
        HashErrorInfoBar.IsOpen = !string.IsNullOrWhiteSpace(ViewModel.HashTool.ErrorMessage);
    }

    private void InspectHashButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.HashTool.InspectCommand.Execute(HashInspectStored.Text);
        HashErrorInfoBar.IsOpen = !string.IsNullOrWhiteSpace(ViewModel.HashTool.ErrorMessage);
        var info = ViewModel.HashTool.Inspection;
        HashInspectionText.Text = info is null
            ? string.Empty
            : $"Algorithm: {info.AlgorithmName}\nSecure for password storage: {(info.IsSecureForPasswordStorage ? "Yes" : "No")}\n" +
              $"Version: {info.Version?.ToString() ?? "n/a"}\nIterations: {info.Iterations?.ToString("N0") ?? "n/a"}\n" +
              $"Work factor: {info.WorkFactor?.ToString() ?? "n/a"}\nMemory cost: {info.MemoryCost?.ToString("N0") ?? "n/a"}\n{info.Notes}";
    }

    private async void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!await ViewModel.Settings.SaveAsync(SettingsMasterPassword.Password)) return;
        SettingsMasterPassword.Password = string.Empty;
        ViewModel.InactivityTimeout = TimeSpan.FromMinutes(ViewModel.Settings.InactivityTimeoutMinutes);
        ViewModel.VaultOpenDuration = TimeSpan.FromHours(5);
        systemLockMonitor.Start(ViewModel.InactivityTimeout, ViewModel.VaultOpenDuration);
    }

    private async void ChangeMasterPasswordButton_Click(object sender, RoutedEventArgs e)
    {
        var changed = await ViewModel.Settings.ChangeMasterPasswordAsync(
            CurrentMasterPassword.Password,
            ReplacementMasterPassword.Password,
            ConfirmReplacementMasterPassword.Password);
        if (!changed) return;
        CurrentMasterPassword.Password = string.Empty;
        ReplacementMasterPassword.Password = string.Empty;
        ConfirmReplacementMasterPassword.Password = string.Empty;
    }

    private async void UpgradeKdfButton_Click(object sender, RoutedEventArgs e)
    {
        if (await ViewModel.Settings.UpgradeKdfAsync(CurrentMasterPassword.Password))
            CurrentMasterPassword.Password = string.Empty;
    }

    private async void PrepareAuthenticatorResetButton_Click(object sender, RoutedEventArgs e)
    {
        settingsAuthenticatorSetup = ViewModel.Settings.PrepareAuthenticator();
        SettingsAuthenticatorSecret.Text = settingsAuthenticatorSetup.SecretBase32;
        SettingsAuthenticatorQr.Source = await CreateQrBitmapAsync(settingsAuthenticatorSetup.OtpAuthUri);
        SettingsAuthenticatorQr.Visibility = Visibility.Visible;
        AuthenticatorResetCode.FocusFirst();
    }

    private async void ResetAuthenticatorButton_Click(object sender, RoutedEventArgs e)
    {
        if (settingsAuthenticatorSetup is null) return;
        var reset = await ViewModel.Settings.ResetAuthenticatorAsync(
            AuthenticatorResetMasterPassword.Password,
            settingsAuthenticatorSetup,
            AuthenticatorResetCode.Code);
        if (!reset) return;

        AuthenticatorResetMasterPassword.Password = string.Empty;
        AuthenticatorResetCode.Clear();
        SettingsAuthenticatorSecret.Text = string.Empty;
        SettingsAuthenticatorQr.Source = null;
        SettingsAuthenticatorQr.Visibility = Visibility.Collapsed;
        settingsAuthenticatorSetup = null;
    }

    private async void ExportBackupButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.Equals(BackupPassphrase.Password, BackupPassphraseConfirmation.Password, StringComparison.Ordinal))
        {
            ViewModel.Backup.StatusMessage = "The backup passphrase values do not match.";
            ViewModel.Backup.IsStatusOpen = true;
            return;
        }
        await ViewModel.Backup.ExportAsync(BackupPassphrase.Password);
        BackupPassphraseConfirmation.Password = string.Empty;
    }

    private async void VerifyBackupButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.Backup.VerifyAsync(BackupPassphrase.Password);

    private async void PreviewBackupImportButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.Backup.PreviewImportAsync(BackupPassphrase.Password);

    private async void ImportBackupButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.Backup.ImportBackupAsync(BackupPassphrase.Password);

    private async void PreviewCsvButton_Click(object sender, RoutedEventArgs e) => await ViewModel.Backup.PreviewCsvAsync();
    private async void ImportCsvButton_Click(object sender, RoutedEventArgs e) => await ViewModel.Backup.ImportCsvAsync();

    private async void RestoreSnapshotButton_Click(object sender, RoutedEventArgs e)
    {
        if (!await ViewModel.Backup.RestoreSnapshotAsync(SnapshotMasterPassword.Password)) return;
        SnapshotMasterPassword.Password = string.Empty;
        systemLockMonitor.Stop();
        await ViewModel.LockCommand.ExecuteAsync(null);
        ClearEditor();
        ClearSettingsInputs();
        ClearBackupInputs();
        ApplyShellState();
        FocusCurrentAuthenticationStep();
    }

    private async void RunSecurityCheckButton_Click(object sender, RoutedEventArgs e) => await ViewModel.SecurityCheck.RunAsync();

    private async void EditSecurityFindingButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SecurityCheck.SelectedFinding is not { } finding) return;
        ViewModel.Vault.SelectItem(finding.ItemId);
        await OpenSelectedEditorAsync();
    }
}
