namespace PasswordTool_WinUI;

internal sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = "Local\\PasswordTool.WinUI.SingleInstance";
    private readonly Mutex mutex;
    private bool ownsMutex;

    private SingleInstanceGuard(Mutex mutex, bool ownsMutex)
    {
        this.mutex = mutex;
        this.ownsMutex = ownsMutex;
    }

    public static SingleInstanceGuard TryAcquire(out bool acquired)
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out acquired);
        return new SingleInstanceGuard(mutex, acquired);
    }

    public void Dispose()
    {
        if (ownsMutex)
        {
            mutex.ReleaseMutex();
            ownsMutex = false;
        }

        mutex.Dispose();
    }
}
