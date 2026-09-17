using PasswordTool.Core.Models;

namespace PasswordTool.WinForms;

public sealed class VaultSecurityCheckForm : Form
{
    public VaultSecurityCheckForm(IReadOnlyList<VaultSecurityFinding> findings)
    {
        Text = "Local Security Check";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(780, 430);
        Padding = new Padding(16);
        FormIconService.Apply(this);
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false
        };
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(VaultSecurityFinding.Title), HeaderText = "Item", FillWeight = 30 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(VaultSecurityFinding.Type), HeaderText = "Finding", FillWeight = 22 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(VaultSecurityFinding.Description), HeaderText = "Recommendation", FillWeight = 48 });
        grid.DataSource = findings.ToList();
        var status = new Label
        {
            Text = findings.Count == 0 ? "No weak, reused, or one-year-old passwords found." : $"{findings.Count} finding(s). Password values never leave this device.",
            Dock = DockStyle.Top,
            Height = 34
        };
        Controls.Add(grid);
        Controls.Add(status);
    }
}
