using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PasswordTool.Presentation;

public sealed partial class TrashViewModel(
    AppFlowCoordinator flow,
    IUserDialogService dialogs,
    IUserErrorMapper errorMapper,
    VaultWorkspaceViewModel vault) : ObservableObject
{
    public ObservableCollection<TrashItemListItem> Items { get; } = [];
    [ObservableProperty] public partial TrashItemListItem? SelectedItem { get; set; }
    [ObservableProperty] public partial string ErrorMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsErrorOpen { get; set; }

    public async Task LoadAsync()
    {
        var version = flow.LifecycleVersion;
        try
        {
            var items = await flow.GetDeletedItemsAsync();
            if (!flow.IsCurrentUnlock(version)) return;
            Items.Clear();
            foreach (var item in items) Items.Add(item);
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    public async Task RestoreSelectedAsync()
    {
        if (SelectedItem is not { } item) return;
        try
        {
            await flow.RestoreDeletedItemAsync(item.Id);
            await LoadAsync();
            await vault.RefreshAsync();
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    public async Task PermanentlyDeleteSelectedAsync()
    {
        var version = flow.LifecycleVersion;
        if (SelectedItem is not { } item) return;
        if (!await dialogs.ConfirmAsync("Delete permanently", $"Permanently delete '{item.Title}'? This cannot be undone.", "Delete permanently") || !flow.IsCurrentUnlock(version)) return;
        try
        {
            await flow.PermanentlyDeleteItemAsync(item.Id, string.Empty);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void ShowError(Exception exception)
    {
        if (exception is OperationCanceledException) return;
        ErrorMessage = errorMapper.Map(exception);
        IsErrorOpen = true;
    }

    public void Clear()
    {
        Items.Clear();
        SelectedItem = null;
        ErrorMessage = string.Empty;
        IsErrorOpen = false;
    }
}
