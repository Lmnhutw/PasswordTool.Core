namespace PasswordTool.WinForms;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.AddMessageFilter(new ClipboardShortcutBlocker());
        Application.Run(new MainForm());
    }
}
