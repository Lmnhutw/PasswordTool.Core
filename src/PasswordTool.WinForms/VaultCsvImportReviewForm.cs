using PasswordTool.Core.Models;

namespace PasswordTool.WinForms;

internal sealed class VaultCsvImportReviewForm : Form
{
    public VaultCsvImportReviewForm(VaultCsvImportPlan plan)
    {
        Text = "Review CSV Import";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(850, 500);
        Padding = new Padding(12);
        FormIconService.Apply(this);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.Controls.Add(new Label
        {
            Text = $"{plan.NewItemCount} new, {plan.DuplicateCount} duplicate. Existing items will not be overwritten.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            ClipboardCopyMode = DataGridViewClipboardCopyMode.Disable
        };
        grid.Columns.Add("Title", "Title");
        grid.Columns.Add("Username", "Username");
        grid.Columns.Add("Url", "URL");
        grid.Columns.Add("Status", "Status");
        foreach (var item in plan.Items) grid.Rows.Add(item.Title, item.Username, item.Url, item.Status);
        root.Controls.Add(grid, 0, 1);

        var importButton = new Button
        {
            Text = plan.NewItemCount > 0 ? $"Import {plan.NewItemCount} New" : "Nothing to Import",
            Width = 140,
            Enabled = plan.NewItemCount > 0,
            DialogResult = DialogResult.OK
        };
        var cancelButton = new Button { Text = "Cancel", Width = 100, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(importButton);
        root.Controls.Add(buttons, 0, 2);
        Controls.Add(root);
        AcceptButton = importButton;
        CancelButton = cancelButton;
    }
}
