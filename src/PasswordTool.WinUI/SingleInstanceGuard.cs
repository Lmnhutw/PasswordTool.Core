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
        var name = MutexName;
#if DEBUG
        if (Environment.GetEnvironmentVariable("PASSWORDTOOL_UI_TEST_DIRECTORY") is { Length: > 0 } directory)
            name += ".Test." + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(directory))));
#endif
        var mutex = new Mutex(initiallyOwned: true, name, out acquired);
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
