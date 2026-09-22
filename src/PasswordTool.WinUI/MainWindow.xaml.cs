using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using Windows.Graphics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace PasswordTool_WinUI;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    private const int MinimumLogicalWidth = 900;
    private const int MinimumLogicalHeight = 600;
    private bool enforcingMinimumSize;
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var scale = GetDpiForWindow(hwnd) / 96d;
        AppWindow.Resize(new SizeInt32((int)(1120 * scale), (int)(720 * scale)));
        AppWindow.Changed += AppWindow_Changed;

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage));
    }

    private void AppWindow_Changed(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowChangedEventArgs args)
    {
        if (!args.DidSizeChange || enforcingMinimumSize) return;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var scale = GetDpiForWindow(hwnd) / 96d;
        var minimumWidth = (int)(MinimumLogicalWidth * scale);
        var minimumHeight = (int)(MinimumLogicalHeight * scale);
        if (sender.Size.Width >= minimumWidth && sender.Size.Height >= minimumHeight) return;

        enforcingMinimumSize = true;
        sender.Resize(new SizeInt32(
            Math.Max(sender.Size.Width, minimumWidth),
            Math.Max(sender.Size.Height, minimumHeight)));
        enforcingMinimumSize = false;
    }
}
