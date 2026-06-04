namespace PasswordTool.WinForms;

internal static class FormIconService
{
    private const string IconRelativePath = "Assets\\Icons\\app.ico";
    private static Icon? cachedIcon;

    public static void Apply(Form form)
    {
        ArgumentNullException.ThrowIfNull(form);

        var icon = GetIcon();
        if (icon is not null)
        {
            form.Icon = icon;
        }
    }

    private static Icon? GetIcon()
    {
        if (cachedIcon is not null)
        {
            return cachedIcon;
        }

        var iconPath = Path.Combine(AppContext.BaseDirectory, IconRelativePath);
        if (!File.Exists(iconPath))
        {
            return null;
        }

        cachedIcon = new Icon(iconPath);
        return cachedIcon;
    }
}
