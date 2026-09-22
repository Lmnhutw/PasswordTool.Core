using System.Runtime.InteropServices;
using Microsoft.Win32;
using PasswordTool.Presentation;

namespace PasswordTool_WinUI;

internal sealed class SystemLockMonitor : ISystemLockMonitor, IDisposable
{
    private readonly Timer timer;
    private TimeSpan inactivityTimeout;
    private int lockRaised;
    private bool monitoring;
    private bool disposed;

    public SystemLockMonitor() => timer = new Timer(CheckIdle, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

    public event EventHandler? LockRequired;

    public void Start(TimeSpan inactivityTimeout)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (inactivityTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(inactivityTimeout));
        this.inactivityTimeout = inactivityTimeout;
        Interlocked.Exchange(ref lockRaised, 0);
        if (!monitoring)
        {
            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
            SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
            monitoring = true;
        }

        timer.Change(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public void Stop()
    {
        timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        if (!monitoring) return;
        SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
        SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
        monitoring = false;
        Interlocked.Exchange(ref lockRaised, 0);
    }

    private void CheckIdle(object? state)
    {
        if (GetSystemIdleTime() >= inactivityTimeout) RequestLock();
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

    private static TimeSpan GetSystemIdleTime()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;
        var elapsed = unchecked((uint)Environment.TickCount - info.TickCount);
        return TimeSpan.FromMilliseconds(elapsed);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Stop();
        timer.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint TickCount;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);
}
