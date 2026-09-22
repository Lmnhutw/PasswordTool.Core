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
        try
        {
            var items = await flow.GetDeletedItemsAsync();
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
        if (SelectedItem is not { } item) return;
        if (!await dialogs.ConfirmAsync("Delete permanently", $"Permanently delete '{item.Title}'? This cannot be undone.", "Delete permanently")) return;
        var code = await RequestSensitiveCodeAsync();
        if (code is null) return;
        try
        {
            await flow.PermanentlyDeleteItemAsync(item.Id, code);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private async Task<string?> RequestSensitiveCodeAsync()
    {
        if (await flow.IsSensitiveSessionActiveAsync()) return string.Empty;
        return await dialogs.PromptSensitiveTotpAsync("Delete permanently", "Confirm before permanently deleting this item.");
    }

    private void ShowError(Exception exception)
    {
        ErrorMessage = errorMapper.Map(exception);
        IsErrorOpen = true;
    }
}
