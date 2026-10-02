using Microsoft.Win32;
using PasswordTool.Presentation;

namespace PasswordTool_WinUI;

internal sealed class SystemLockMonitor : ISystemLockMonitor, IDisposable
{
    private int lockRaised;
    private bool monitoring;
    private bool disposed;


    public event EventHandler? LockRequired;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Interlocked.Exchange(ref lockRaised, 0);
        if (!monitoring)
        {
            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
            SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
            monitoring = true;
        }

    }

    public void Stop()
    {
        if (!monitoring) return;
        SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
        SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
        monitoring = false;
        Interlocked.Exchange(ref lockRaised, 0);
    }


    private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionLock
            or SessionSwitchReason.ConsoleDisconnect
            or SessionSwitchReason.RemoteDisconnect)
        {
            RequestLock();
        }
    }

    private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode is PowerModes.Suspend or PowerModes.Resume) RequestLock();
    }

    private void RequestLock()
    {
        if (Interlocked.Exchange(ref lockRaised, 1) == 0) LockRequired?.Invoke(this, EventArgs.Empty);
    }


    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Stop();
    }

}
