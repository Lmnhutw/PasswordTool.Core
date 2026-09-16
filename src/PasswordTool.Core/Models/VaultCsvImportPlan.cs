namespace PasswordTool.Core.Models;

public sealed record VaultCsvImportPlanItem(string Title, string Username, string Url, VaultCsvImportStatus Status);

public sealed class VaultCsvImportPlan
{
    public VaultCsvImportPlan(IReadOnlyList<VaultCsvImportPlanItem> items)
    {
        Items = items;
    }

    public IReadOnlyList<VaultCsvImportPlanItem> Items { get; }

    public int NewItemCount => Items.Count(item => item.Status == VaultCsvImportStatus.New);

    public int DuplicateCount => Items.Count(item => item.Status == VaultCsvImportStatus.Duplicate);
}
