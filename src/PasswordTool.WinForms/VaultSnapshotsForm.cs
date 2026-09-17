using PasswordTool.Core.Models;
using PasswordTool.Core.Services;

namespace PasswordTool.WinForms;

public sealed class VaultSnapshotsForm : Form
{
    private readonly VaultService vaultService;
    private readonly ListBox snapshots = new() { Dock = DockStyle.Fill };

    public VaultSnapshotsForm(VaultService vaultService)
    {
        this.vaultService = vaultService;
        Text = "Encrypted Vault Snapshots";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(590, 350);
        Padding = new Padding(16);
        FormIconService.Apply(this);
        foreach (var snapshot in vaultService.GetSnapshots()) snapshots.Items.Add(new SnapshotRow(snapshot));
        var restore = new Button { Text = "Restore selected", Dock = DockStyle.Bottom, Height = 38 };
        restore.Click += Restore_Click;
        Controls.Add(snapshots);
        Controls.Add(restore);
    }

    public bool Restored { get; private set; }

    private void Restore_Click(object? sender, EventArgs e)
    {
        if (snapshots.SelectedItem is not SnapshotRow selected) return;
        if (MessageBox.Show("Restore this complete config + vault snapshot? The current state will first be snapshotted.",
            "PasswordTool", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        using var prompt = new MasterPasswordPromptForm("Restore Snapshot", "Enter the current Master Password to authorize restore.");
        if (prompt.ShowDialog(this) != DialogResult.OK) return;
        if (!vaultService.TryRestoreSnapshot(selected.Info.Id, prompt.MasterPassword, out var error))
        {
            MessageBox.Show(error, "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        Restored = true;
        DialogResult = DialogResult.OK;
        Close();
    }

    private sealed record SnapshotRow(VaultSnapshotInfo Info)
    {
        public override string ToString() => Info.CreatedAtUtc.ToLocalTime().ToString("F");
    }
}
