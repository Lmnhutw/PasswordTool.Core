namespace PasswordTool.WinForms;

internal sealed class RecoveryCodesReviewForm : Form
{
    public RecoveryCodesReviewForm(IReadOnlyList<string> codes)
    {
        Text = "Review Recovery Codes";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(560, 430);
        Padding = new Padding(16);
        FormIconService.Apply(this);
        UiTheme.Apply(this);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        var heading = new Label
        {
            Text = $"{codes.Count} recovery codes detected. Confirm every code before using this list.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var countNote = new Label
        {
            Text = codes.Count == 10
                ? "The expected set of 10 codes was detected."
                : "Code counts vary by service; verify this count against the source app.",
            Dock = DockStyle.Fill,
            ForeColor = codes.Count == 10 ? Color.DarkGreen : Color.DarkOrange,
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
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "#", FillWeight = 15 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Recovery code", FillWeight = 85 });
        for (var index = 0; index < codes.Count; index++)
        {
            grid.Rows.Add(index + 1, codes[index]);
        }

        var useButton = new Button { Text = "Use These Codes", Width = 140, DialogResult = DialogResult.OK };
        var cancelButton = new Button { Text = "Cancel", Width = 100, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(useButton);

        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(countNote, 0, 1);
        layout.Controls.Add(grid, 0, 2);
        layout.Controls.Add(buttons, 0, 3);
        Controls.Add(layout);
        AcceptButton = useButton;
        CancelButton = cancelButton;
    }
}
