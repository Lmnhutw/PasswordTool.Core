using System.Runtime.InteropServices;

namespace PasswordTool.WinForms;

internal sealed class SensitiveClipboardService : IDisposable
{
    private readonly System.Windows.Forms.Timer clearTimer = new() { Interval = 30_000 };
    private string? ownedValue;

    public SensitiveClipboardService()
    {
        clearTimer.Tick += (_, _) => ClearIfOwned();
    }

    public void Copy(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("There is no value to copy.", nameof(value));
        }

        Clipboard.SetText(value);
        ownedValue = value;
        clearTimer.Stop();
        clearTimer.Start();
    }

    public void ClearIfOwned()
    {
        clearTimer.Stop();
        if (ownedValue is null) return;
        try
        {
            if (Clipboard.ContainsText() && string.Equals(Clipboard.GetText(), ownedValue, StringComparison.Ordinal))
            {
                Clipboard.Clear();
            }
        }
        catch (ExternalException)
        {
        }
        finally
        {
            ownedValue = null;
        }
    }

    public void Dispose()
    {
        ClearIfOwned();
        clearTimer.Dispose();
    }
}
