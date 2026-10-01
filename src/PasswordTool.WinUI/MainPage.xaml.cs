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
    private readonly Windows.UI.ViewManagement.AccessibilitySettings accessibility = new();
    private readonly Windows.UI.ViewManagement.UISettings uiSettings = new();

    public MainPage()
    {
        InitializeComponent();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.Vault.PropertyChanged += Vault_PropertyChanged;
        Loaded += async (_, _) =>
        {
            systemLockMonitor.LockRequired += SystemLockMonitor_LockRequired;
            uiSettings.ColorValuesChanged += UiSettings_ColorValuesChanged;
            ApplyGroupTabPlacement();
            ApplyShellState();
            FocusCurrentAuthenticationStep();
        };
        Unloaded += (_, _) =>
        {
            systemLockMonitor.LockRequired -= SystemLockMonitor_LockRequired;
            uiSettings.ColorValuesChanged -= UiSettings_ColorValuesChanged;
            systemLockMonitor.Stop();
        };
    }

    public static Visibility BoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
    public static bool Not(bool value) => !value;
    public static bool HasSelection(object? value) => value is not null;
    public static Visibility EmptyVisibility(int count) => count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility InvertBoolToVisibility(bool value) => value ? Visibility.Collapsed : Visibility.Visible;
    public static HorizontalAlignment NotesRevealAlignment(bool hideNotes) => hideNotes ? HorizontalAlignment.Center : HorizontalAlignment.Right;
    public static string ItemAutomationId(string action, Guid id) => $"{action}_{id:N}";
    public static string GroupAutomationId(Guid? id) => id is null ? "Group_Ungrouped" : $"Group_{id:N}";
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
        if (sender is Button { Tag: VaultItemGroup group } button && group != ViewModel.Vault.SelectedGroup)
        {
            button.Background = (Brush)Application.Current.Resources["VaultGroupHoverBrush"];
            if (accessibility.HighContrast) button.Foreground = new SolidColorBrush((Windows.UI.Color)Application.Current.Resources["SystemColorHighlightTextColor"]);
        }
    }

    private void GroupTab_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button) UpdateGroupTab(button);
    }

    private void Vault_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VaultWorkspaceViewModel.IsVerticalTabs)) ApplyGroupTabPlacement();
        if (e.PropertyName == nameof(VaultWorkspaceViewModel.SelectedGroup))
        {
            UpdateGroupTabs();
            DispatcherQueue.TryEnqueue(BringActiveGroupIntoView);
        }
    }

    private void UiSettings_ColorValuesChanged(Windows.UI.ViewManagement.UISettings sender, object args) =>
        DispatcherQueue.TryEnqueue(UpdateGroupTabs);

    private string? DisplayGroupColor(VaultItemGroup group) =>
        !accessibility.HighContrast && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) ? group.AccentColor : null;

    private void UpdateGroupTab(Button button)
    {
        if (button.Tag is not VaultItemGroup group) return;
        var selected = group == ViewModel.Vault.SelectedGroup;
        button.Background = selected ? GroupBrush(DisplayGroupColor(group)) : (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
        button.Foreground = selected ? GroupTextBrush(DisplayGroupColor(group)) : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
        button.BorderBrush = (Brush)Application.Current.Resources[selected ? "AccentFillColorDefaultBrush" : "CardStrokeColorDefaultBrush"];
        button.BorderThickness = ViewModel.Vault.IsVerticalTabs ? new Thickness(1, 1, selected ? 0 : 1, 1) : new Thickness(1, 1, 1, selected ? 0 : 1);
        button.CornerRadius = ViewModel.Vault.IsVerticalTabs ? new CornerRadius(6, 0, 0, 6) : new CornerRadius(6, 6, 0, 0);
        button.FontWeight = selected ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetItemStatus(button, selected ? "Selected" : "Not selected");
    }

    private void UpdateGroupTabs()
    {
        if (GroupTabsRepeater is null) return;
        UpdateGroupTab(AllGroupTab);
        for (var index = 0; index < ViewModel.Vault.GroupTabs.Count; index++)
            if (GroupTabsRepeater.TryGetElement(index) is Button button) UpdateGroupTab(button);
        GroupTableFrame.Background = GroupBrush(DisplayGroupColor(ViewModel.Vault.SelectedGroup));
    }

    private void GroupTab_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Button button) UpdateGroupTab(button);
    }

    private void GroupTab_ActualThemeChanged(FrameworkElement sender, object args) => UpdateGroupTabs();

    private void GroupTabsRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is Button button) UpdateGroupTab(button);
    }

    private void GroupTabPlacement_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.Vault.IsVerticalTabs = GroupTabPlacement.SelectedIndex == 1;
    }

    private void ApplyGroupTabPlacement()
    {
        if (GroupTabStrip is null) return;
        var vertical = ViewModel.Vault.IsVerticalTabs;
        Grid.SetRow(GroupTableFrame, vertical ? 0 : 1);
        Grid.SetColumn(GroupTableFrame, vertical ? 1 : 0);
        Grid.SetRowSpan(GroupTableFrame, vertical ? 2 : 1);
        Grid.SetColumnSpan(GroupTableFrame, vertical ? 1 : 2);
        Grid.SetRowSpan(GroupTabStrip, vertical ? 2 : 1);
        Grid.SetColumnSpan(GroupTabStrip, vertical ? 1 : 2);
        GroupTabStrip.Width = vertical ? 180 : double.NaN;
        GroupTabStrip.Margin = vertical ? new Thickness(0, 12, -1, 12) : new Thickness(12, 0, 12, -1);
        Grid.SetColumnSpan(AllGroupTab, vertical ? 4 : 1);
        AllGroupTab.HorizontalAlignment = vertical ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        Grid.SetRow(GroupTabsScroller, vertical ? 1 : 0);
        Grid.SetColumn(GroupTabsScroller, vertical ? 0 : 2);
        Grid.SetColumnSpan(GroupTabsScroller, vertical ? 4 : 1);
        GroupTabsScroller.Margin = vertical ? new Thickness(0, 4, 0, 0) : new Thickness(4, 0, 0, 0);
        GroupTabsScroller.HorizontalScrollMode = vertical ? ScrollMode.Disabled : ScrollMode.Enabled;
        GroupTabsScroller.HorizontalScrollBarVisibility = vertical ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Hidden;
        GroupTabsScroller.VerticalScrollMode = vertical ? ScrollMode.Enabled : ScrollMode.Disabled;
        GroupTabsScroller.VerticalScrollBarVisibility = vertical ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        ((StackLayout)GroupTabsRepeater.Layout).Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
        GroupTabsScroller.ChangeView(0, 0, null, disableAnimation: true);
        UpdateGroupTabs();
        DispatcherQueue.TryEnqueue(BringActiveGroupIntoView);
    }

    private void BringActiveGroupIntoView()
    {
        var index = ViewModel.Vault.GroupTabs.IndexOf(ViewModel.Vault.SelectedGroup);
        if (index >= 0)
        {
            var element = GroupTabsRepeater.GetOrCreateElement(index);
            GroupTabsRepeater.UpdateLayout();
            element.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true });
        }
        UpdateTabOverflow();
    }

    private void UpdateTabOverflow()
    {
        if (GroupTabsScroller is null) return;
        var show = !ViewModel.Vault.IsVerticalTabs && GroupTabsScroller.ScrollableWidth > 1;
        PreviousGroupTab.Visibility = NextGroupTab.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        PreviousGroupTab.IsEnabled = GroupTabsScroller.HorizontalOffset > 1;
        NextGroupTab.IsEnabled = GroupTabsScroller.HorizontalOffset < GroupTabsScroller.ScrollableWidth - 1;
    }

    private void GroupTabsScroller_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e) => UpdateTabOverflow();
    private void GroupTabsScroller_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateTabOverflow();
    private void GroupTabStrip_SizeChanged(object sender, SizeChangedEventArgs e) => DispatcherQueue.TryEnqueue(BringActiveGroupIntoView);
    private void PreviousGroupTab_Click(object sender, RoutedEventArgs e) => ScrollGroupTabs(-1);
    private void NextGroupTab_Click(object sender, RoutedEventArgs e) => ScrollGroupTabs(1);
    private void ScrollGroupTabs(int direction) => GroupTabsScroller.ChangeView(
        Math.Clamp(GroupTabsScroller.HorizontalOffset + direction * Math.Max(120, GroupTabsScroller.ViewportWidth * 0.75), 0, GroupTabsScroller.ScrollableWidth), null, null);
    private void ClearVaultFilters_Click(object sender, RoutedEventArgs e) => ViewModel.Vault.ResetToDefaultView();

    private void GroupTab_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not Button { Tag: VaultItemGroup group }) return;
        var vertical = ViewModel.Vault.IsVerticalTabs;
        var index = group.IsAll ? 0 : ViewModel.Vault.GroupTabs.IndexOf(group) + 1;
        var next = e.Key switch
        {
            Windows.System.VirtualKey.Home => 0,
            Windows.System.VirtualKey.End => ViewModel.Vault.GroupTabs.Count,
            Windows.System.VirtualKey.Left when !vertical => index - 1,
            Windows.System.VirtualKey.Right when !vertical => index + 1,
            Windows.System.VirtualKey.Up when vertical => index - 1,
            Windows.System.VirtualKey.Down when vertical => index + 1,
            _ => -1
        };
        if (next < 0 || next > ViewModel.Vault.GroupTabs.Count) return;
        e.Handled = true;
        ViewModel.Vault.SelectGroup(next == 0 ? ViewModel.Vault.AllGroup : ViewModel.Vault.GroupTabs[next - 1]);
        var target = next == 0 ? AllGroupTab : (Button)GroupTabsRepeater.GetOrCreateElement(next - 1);
        target.Focus(FocusState.Keyboard);
        BringActiveGroupIntoView();
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
        if (route == AppRoute.ItemEditor) ShellNavigation.SelectedItem = null;
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
        if ((sender as FrameworkElement)?.Tag is VaultItemGroup group) ViewModel.Vault.SelectGroup(group);
    }

    private async void RenameGroupMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not VaultItemGroup { Id: { } id } group) return;
        var name = await PromptAsync("Rename group", "Group name", group.Name, "Confirm");
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

    private async Task<string?> PromptAsync(string title, string header, string value, string primaryText = "Save")
    {
        var input = new TextBox { Header = header, Text = value, MinWidth = 320 };
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = title, Content = input, PrimaryButtonText = primaryText, CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary };
        if (primaryText == "Confirm")
        {
            input.MaxLength = 100;
            dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(input.Text);
            input.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(input.Text);
        }
        dialog.Opened += (_, _) => { input.Focus(FocusState.Programmatic); input.SelectAll(); };
        return await App.Services.GetRequiredService<DialogLifetime>().ShowAsync(dialog, CancellationToken.None) == ContentDialogResult.Primary ? input.Text : null;
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

    private void HashModeSelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (HashGeneratePanel is null) return;
        var mode = sender.Items.IndexOf(sender.SelectedItem);
        HashGeneratePanel.Visibility = mode == 0 ? Visibility.Visible : Visibility.Collapsed;
        HashVerifyPanel.Visibility = mode == 1 ? Visibility.Visible : Visibility.Collapsed;
        HashInspectPanel.Visibility = mode == 2 ? Visibility.Visible : Visibility.Collapsed;
        HashErrorInfoBar.IsOpen = false;
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

    private async void BackupReminderCreate_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.IsBackupReminderOpen = false;
        ViewModel.Navigate(AppRoute.Backup);
        ApplyRoute(AppRoute.Backup);
        await ViewModel.Backup.LoadAsync();
    }

    private void BackupReminderLater_Click(object sender, RoutedEventArgs e) => ViewModel.IsBackupReminderOpen = false;

    private async void ExportBackupButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.Backup.ExportAsync();

    private async void VerifyBackupButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.Backup.VerifyAsync();

    private async void PreviewBackupImportButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.Backup.PreviewImportAsync();

    private async void ImportBackupButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.Backup.ImportBackupAsync();

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
