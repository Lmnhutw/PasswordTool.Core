using PasswordTool.Core.Models;

namespace PasswordTool.WinForms;

public sealed class VaultSecurityCheckForm : Form
{
    private readonly DataGridView grid;
    private readonly Button editSelectedButton;

    public VaultSecurityCheckForm(IReadOnlyList<VaultSecurityFinding> findings)
    {
        Text = "Local Security Check";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(900, 460);
        Padding = new Padding(16);
        FormIconService.Apply(this);
        UiTheme.Apply(this);

        var rows = findings.Select(finding => new FindingRow(
            finding.ItemId,
            finding.Title,
            finding.DisplayLabel,
            finding.Recommendation,
            finding.PasswordChangedAt is { } changedAt ? changedAt.ToLocalTime().ToString("g") : "—"))
            .ToList();
        var affectedCount = rows.Select(row => row.ItemId).Distinct().Count();

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var status = new Label
        {
            Text = rows.Count == 0
                ? "No weak, reused, or one-year-old passwords found."
                : $"{rows.Count} finding(s) across {affectedCount} affected item(s). Password values are never shown.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AccessibleName = "Security Check summary"
        };
        grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AccessibleName = "Security Check findings"
        };
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(FindingRow.Title), HeaderText = "Item", FillWeight = 24 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(FindingRow.Label), HeaderText = "Finding", FillWeight = 22 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(FindingRow.Recommendation), HeaderText = "Recommendation", FillWeight = 40 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(FindingRow.PasswordChangedAt), HeaderText = "Password changed", FillWeight = 14 });
        grid.DataSource = rows;

        var emptyState = new Label
        {
            Text = "Your active password entries have no current local Security Check findings.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = rows.Count == 0,
            AccessibleName = "Security Check empty state"
        };
        var findingsPanel = new Panel { Dock = DockStyle.Fill };
        findingsPanel.Controls.Add(grid);
        findingsPanel.Controls.Add(emptyState);

        editSelectedButton = new Button { Text = "Edit selected item", AutoSize = true, Enabled = false, AccessibleName = "Edit selected Security Check item" };
        var closeButton = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel, AccessibleName = "Close Security Check" };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        actions.Controls.Add(closeButton);
        actions.Controls.Add(editSelectedButton);
        editSelectedButton.Click += (_, _) => EditSelected();
        grid.SelectionChanged += (_, _) => editSelectedButton.Enabled = SelectedItemId.HasValue;
        grid.CellDoubleClick += (_, eventArgs) => { if (eventArgs.RowIndex >= 0) EditSelected(); };

        layout.Controls.Add(status, 0, 0);
        layout.Controls.Add(findingsPanel, 0, 1);
        layout.Controls.Add(actions, 0, 2);
        Controls.Add(layout);
        CancelButton = closeButton;
    }

    public Guid? SelectedItemId => grid.CurrentRow?.DataBoundItem is FindingRow row ? row.ItemId : null;

    private void EditSelected()
    {
        if (!SelectedItemId.HasValue) return;
        DialogResult = DialogResult.OK;
        Close();
    }

    private sealed record FindingRow(Guid ItemId, string Title, string Label, string Recommendation, string PasswordChangedAt);
}
