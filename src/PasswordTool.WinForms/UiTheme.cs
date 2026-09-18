namespace PasswordTool.WinForms;

internal static class UiTheme
{
    public static readonly Color WindowBackground = Color.FromArgb(247, 249, 252);
    public static readonly Color Surface = Color.White;
    public static readonly Color SurfaceMuted = Color.FromArgb(242, 245, 249);
    public static readonly Color Primary = Color.FromArgb(15, 108, 189);
    public static readonly Color PrimaryHover = Color.FromArgb(0, 90, 158);
    public static readonly Color PrimaryPressed = Color.FromArgb(0, 76, 135);
    public static readonly Color TextPrimary = Color.FromArgb(31, 31, 31);
    public static readonly Color TextSecondary = Color.FromArgb(84, 93, 104);
    public static readonly Color Border = Color.FromArgb(210, 216, 225);
    public static readonly Color Selection = Color.FromArgb(226, 239, 252);
    public static readonly Color WarningBackground = Color.FromArgb(255, 247, 224);
    public static readonly Color WarningText = Color.FromArgb(89, 67, 0);
    public static readonly Color SuccessBackground = Color.FromArgb(230, 247, 237);
    public static readonly Color SuccessText = Color.FromArgb(16, 92, 49);
    public static readonly Color Danger = Color.FromArgb(196, 43, 28);

    public static Font DefaultFont { get; } = new("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

    public static void Apply(Form form)
    {
        form.BackColor = WindowBackground;
        form.ForeColor = TextPrimary;
        ApplyChildren(form.Controls);
    }

    public static void StylePrimaryButton(Button button)
    {
        StyleButtonBase(button);
        button.BackColor = Primary;
        button.ForeColor = Color.White;
        button.FlatAppearance.BorderColor = Primary;
        button.FlatAppearance.MouseOverBackColor = PrimaryHover;
        button.FlatAppearance.MouseDownBackColor = PrimaryPressed;
    }

    public static void StyleSecondaryButton(Button button)
    {
        StyleButtonBase(button);
        button.BackColor = Surface;
        button.ForeColor = TextPrimary;
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseOverBackColor = SurfaceMuted;
        button.FlatAppearance.MouseDownBackColor = Selection;
    }

    public static void StyleDangerButton(Button button)
    {
        StyleSecondaryButton(button);
        button.ForeColor = Danger;
        button.FlatAppearance.BorderColor = Color.FromArgb(232, 174, 168);
    }

    public static void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = Surface;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.EnableHeadersVisualStyles = false;
        grid.GridColor = Border;
        grid.RowTemplate.Height = 44;
        grid.ColumnHeadersHeight = 38;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = TextPrimary;
        grid.DefaultCellStyle.SelectionBackColor = Selection;
        grid.DefaultCellStyle.SelectionForeColor = TextPrimary;
        grid.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
        grid.ColumnHeadersDefaultCellStyle.BackColor = SurfaceMuted;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextPrimary;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = SurfaceMuted;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextPrimary;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font(DefaultFont, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(252, 253, 255);
    }

    public static void StyleMenu(ContextMenuStrip menu)
    {
        menu.BackColor = Surface;
        menu.ForeColor = TextPrimary;
        menu.Font = DefaultFont;
        menu.Padding = new Padding(4);
        menu.ShowImageMargin = false;
    }

    private static void ApplyChildren(Control.ControlCollection controls)
    {
        foreach (Control control in controls)
        {
            switch (control)
            {
                case Button button:
                    if (button.Text.StartsWith("Delete", StringComparison.OrdinalIgnoreCase))
                    {
                        StyleDangerButton(button);
                    }
                    else if (IsPrimaryAction(button.Text))
                    {
                        StylePrimaryButton(button);
                    }
                    else
                    {
                        StyleSecondaryButton(button);
                    }
                    break;
                case DataGridView grid:
                    StyleGrid(grid);
                    break;
                case TextBoxBase textBox:
                    textBox.BackColor = textBox.ReadOnly ? SurfaceMuted : Surface;
                    textBox.ForeColor = TextPrimary;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case ComboBox comboBox:
                    comboBox.BackColor = Surface;
                    comboBox.ForeColor = TextPrimary;
                    comboBox.FlatStyle = FlatStyle.Flat;
                    break;
                case NumericUpDown numericUpDown:
                    numericUpDown.BackColor = Surface;
                    numericUpDown.ForeColor = TextPrimary;
                    numericUpDown.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case Label label when label.ForeColor == SystemColors.ControlText:
                    label.ForeColor = TextPrimary;
                    break;
                case GroupBox groupBox:
                    groupBox.ForeColor = TextPrimary;
                    break;
            }

            if (control.HasChildren)
            {
                ApplyChildren(control.Controls);
            }
        }
    }

    private static void StyleButtonBase(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.UseVisualStyleBackColor = false;
        button.AutoEllipsis = true;
    }

    private static bool IsPrimaryAction(string text)
    {
        var normalized = text.Replace("&", string.Empty, StringComparison.Ordinal).Trim();
        return normalized is "Save" or "Create" or "Continue" or "Confirm" or "Verify" or "Change"
            or "Generate" or "Use This" or "Use These Codes" or "Restore" or "Restore selected"
            or "Edit selected item" or "Generate Hash" or "Verify Password" or "Inspect Hash"
            || normalized.StartsWith("Import ", StringComparison.OrdinalIgnoreCase);
    }
}
