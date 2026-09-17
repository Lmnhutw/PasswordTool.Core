namespace PasswordTool.WinForms;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var singleInstance = new Mutex(initiallyOwned: true, "Local\\PasswordTool.WinForms.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("PasswordTool is already running.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.AddMessageFilter(new ClipboardShortcutBlocker());
        Application.Run(new MainForm());
    }
}
