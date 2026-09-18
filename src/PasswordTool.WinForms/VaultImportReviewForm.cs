using PasswordTool.Core.Models;

namespace PasswordTool.WinForms;

internal sealed class VaultImportReviewForm : Form
{
    public VaultImportReviewForm(VaultBackupImportPlan plan)
    {
        Text = "Review JSON Import";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(700, 470);
        Padding = new Padding(16);
        FormIconService.Apply(this);
        UiTheme.Apply(this);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        var summary = new Label
        {
            Text = $"New: {plan.NewItemCount}    Duplicate: {plan.DuplicateCount}    Conflict: {plan.ConflictCount}\r\nDuplicates and conflicts will not be imported or overwrite existing items.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SystemColors.Window,
            ClipboardCopyMode = DataGridViewClipboardCopyMode.Disable,
            MultiSelect = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Title", FillWeight = 50 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Type", FillWeight = 25 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", FillWeight = 25 });
        foreach (var item in plan.Items)
        {
            grid.Rows.Add(item.Title, item.Type == VaultItemType.Password ? "Password" : "Recovery codes", item.Status);
        }

        var importButton = new Button
        {
            Text = plan.NewItemCount > 0 ? $"Import {plan.NewItemCount} New" : "Nothing to Import",
            Width = 140,
            Enabled = plan.NewItemCount > 0,
            DialogResult = DialogResult.OK
        };
        var cancelButton = new Button { Text = plan.NewItemCount > 0 ? "Cancel" : "Close", Width = 100, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(importButton);

        layout.Controls.Add(summary, 0, 0);
        layout.Controls.Add(grid, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        Controls.Add(layout);
        AcceptButton = importButton;
        CancelButton = cancelButton;
    }
}
