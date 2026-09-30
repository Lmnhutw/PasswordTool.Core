using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PasswordTool.Core.Models;

namespace PasswordTool.Presentation;

public sealed partial class VaultWorkspaceViewModel : ObservableObject
{
    private readonly AppFlowCoordinator flow;
    private IReadOnlyList<VaultItemListItem> allItems = [];
    private IReadOnlyList<VaultGroup> groups = [];
    private readonly HashSet<Guid?> collapsedGroups = [];

    public VaultWorkspaceViewModel(AppFlowCoordinator flow)
    {
        this.flow = flow;
        SelectedViewFilter = ViewFilters[0];
        SelectedSortOrder = SortOrders[0];
    }

    public ObservableCollection<VaultItemListItem> Items { get; } = [];
    public ObservableCollection<VaultItemGroup> ItemGroups { get; } = [];
    public ObservableCollection<VaultGroupOption> GroupOptions { get; } = [new("Ungrouped", null), new("Create new group...", null, true)];
    public IReadOnlyList<VaultFilterOption> ViewFilters { get; } = [new("All items", VaultViewFilter.All), new("Favorites", VaultViewFilter.Favorites), new("Passwords", VaultViewFilter.Passwords), new("Recovery codes", VaultViewFilter.RecoveryCodes)];
    public IReadOnlyList<VaultSortOption> SortOrders { get; } = [new("Title A–Z", VaultSortOrder.TitleAscending), new("Title Z–A", VaultSortOrder.TitleDescending), new("Recently updated", VaultSortOrder.UpdatedNewest), new("Oldest updated", VaultSortOrder.UpdatedOldest)];

    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial VaultFilterOption SelectedViewFilter { get; set; }
    [ObservableProperty] public partial VaultSortOption SelectedSortOrder { get; set; }
    [ObservableProperty] public partial VaultItemListItem? SelectedItem { get; set; }
    public string ItemCountText => $"{Items.Count:N0} item{(Items.Count == 1 ? string.Empty : "s")}";
    public bool IsEmpty => Items.Count == 0 && !IsBusy;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            allItems = await flow.GetListItemsAsync(cancellationToken);
            groups = await flow.GetGroupsAsync(cancellationToken);
            RefreshGroupOptions();
            ApplyFilter();
        }
        finally { IsBusy = false; OnPropertyChanged(nameof(IsEmpty)); }
    }

    public void Clear()
    {
        allItems = []; groups = []; Items.Clear(); ItemGroups.Clear(); GroupOptions.Clear();
        GroupOptions.Add(new("Ungrouped", null)); GroupOptions.Add(new("Create new group...", null, true));
        SearchText = string.Empty; IsBusy = false; SelectedItem = null;
        OnPropertyChanged(nameof(ItemCountText)); OnPropertyChanged(nameof(IsEmpty));
    }

    public bool SelectItem(Guid id)
    {
        SearchText = string.Empty; SelectedViewFilter = ViewFilters[0];
        SelectedItem = Items.FirstOrDefault(candidate => candidate.Id == id);
        return SelectedItem is not null;
    }

    public void ToggleGroup(VaultItemGroup group)
    {
        group.IsExpanded = !group.IsExpanded;
        if (group.IsExpanded) collapsedGroups.Remove(group.Id); else collapsedGroups.Add(group.Id);
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedViewFilterChanged(VaultFilterOption value) => ApplyFilter();
    partial void OnSelectedSortOrderChanged(VaultSortOption value) => ApplyFilter();

    private void ApplyFilter()
    {
        if (SelectedViewFilter is null || SelectedSortOrder is null) return;
        var query = SearchText.Trim();
        IEnumerable<VaultItemListItem> filtered = string.IsNullOrEmpty(query) ? allItems : allItems.Where(item => item.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) || item.Username.Contains(query, StringComparison.CurrentCultureIgnoreCase) || item.Url.Contains(query, StringComparison.CurrentCultureIgnoreCase) || item.Tags.Any(tag => tag.Contains(query, StringComparison.CurrentCultureIgnoreCase)));
        filtered = SelectedViewFilter.Value switch { VaultViewFilter.Favorites => filtered.Where(item => item.IsFavorite), VaultViewFilter.Passwords => filtered.Where(item => item.HasPassword), VaultViewFilter.RecoveryCodes => filtered.Where(item => item.RecoveryCodeCount > 0), _ => filtered };
        filtered = SelectedSortOrder.Value switch { VaultSortOrder.TitleDescending => filtered.OrderByDescending(item => item.Title, StringComparer.CurrentCultureIgnoreCase), VaultSortOrder.UpdatedNewest => filtered.OrderByDescending(item => item.UpdatedAt), VaultSortOrder.UpdatedOldest => filtered.OrderBy(item => item.UpdatedAt), _ => filtered.OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase) };
        var visible = filtered.ToList();
        Items.Clear(); foreach (var item in visible) Items.Add(item);
        ItemGroups.Clear();
        foreach (var group in groups.OrderBy(group => group.SortOrder).ThenBy(group => group.Name, StringComparer.CurrentCultureIgnoreCase)) AddVisibleGroup(group.Id, group.Name, group.AccentColor, group.SortOrder, visible);
        AddVisibleGroup(null, "Ungrouped", null, int.MaxValue, visible);
        OnPropertyChanged(nameof(ItemCountText)); OnPropertyChanged(nameof(IsEmpty));
    }

    private void AddVisibleGroup(Guid? id, string name, string? accentColor, int sortOrder, IReadOnlyList<VaultItemListItem> visible)
    {
        var items = visible.Where(item => item.GroupId == id).ToList();
        if (items.Count == 0) return;
        var group = new VaultItemGroup(id, name, accentColor, sortOrder) { IsExpanded = !collapsedGroups.Contains(id) };
        for (var index = 0; index < items.Count; index++)
            group.Items.Add(items[index] with { IsLastInGroup = index == items.Count - 1 });
        ItemGroups.Add(group);
    }

    private void RefreshGroupOptions()
    {
        GroupOptions.Clear(); GroupOptions.Add(new("Ungrouped", null));
        foreach (var group in groups.OrderBy(group => group.SortOrder).ThenBy(group => group.Name, StringComparer.CurrentCultureIgnoreCase)) GroupOptions.Add(new(group.Name, group.Id));
        GroupOptions.Add(new("Create new group...", null, true));
    }
}
