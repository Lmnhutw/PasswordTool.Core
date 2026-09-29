using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PasswordTool.Presentation;

public sealed partial class VaultItemGroup(Guid? id, string name, string? accentColor, int sortOrder) : ObservableObject
{
    public Guid? Id { get; } = id;
    public string Name { get; } = name;
    public string? AccentColor { get; } = accentColor;
    public int SortOrder { get; } = sortOrder;
    public bool IsSystemGroup => Id is null;
    public ObservableCollection<VaultItemListItem> Items { get; } = [];
    public string CountText => Items.Count.ToString("N0");
    [ObservableProperty] public partial bool IsExpanded { get; set; } = true;
}
