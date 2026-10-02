using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using PasswordTool.Presentation;
using Windows.ApplicationModel.DataTransfer;

namespace PasswordTool_WinUI;

internal sealed class SensitiveClipboardService : ISensitiveClipboardService, IDisposable
{
    private static readonly TimeSpan ClearDelay = TimeSpan.FromSeconds(30);
    private readonly object sync = new();
    private CancellationTokenSource? expiration;
    private byte[]? ownedValueHash;
    private long ownershipGeneration;
    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();
    private bool disposed;

    public Task CopyAsync(string value, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();

        return RunOnUiThreadAsync(() =>
        {
            var package = new DataPackage();
            package.SetText(value);
            if (!Clipboard.SetContentWithOptions(package, new ClipboardContentOptions
                { IsAllowedInHistory = false, IsRoamable = false }))
                throw new InvalidOperationException("The clipboard is unavailable.");

            lock (sync)
            {
                ownershipGeneration++;
                ClearOwnedHash();
                ownedValueHash = HashValue(value);
                expiration?.Cancel();
                expiration?.Dispose();
                expiration = new CancellationTokenSource();
                _ = ClearAfterDelayAsync(expiration.Token);
            }

            // Publish the text before reporting success to external paste targets.
            Clipboard.Flush();
            return Task.CompletedTask;
        });
    }

    public Task ClearOwnedValueAsync(CancellationToken cancellationToken = default)
    {
        if (disposed) return Task.CompletedTask;
        cancellationToken.ThrowIfCancellationRequested();
        return RunOnUiThreadAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[]? expected;
            long expectedGeneration;
            lock (sync)
            {
                expected = ownedValueHash is null ? null : [.. ownedValueHash];
                expectedGeneration = ownershipGeneration;
            }

            if (expected is null) return;
            try
            {
                var sequence = GetClipboardSequenceNumber();
                var content = Clipboard.GetContent();
                if (!content.Contains(StandardDataFormats.Text)) return;
                var current = await content.GetTextAsync();
                var currentHash = HashValue(current);
                try
                {
                    lock (sync)
                    {
                        if (expectedGeneration == ownershipGeneration &&
                            sequence == GetClipboardSequenceNumber() &&
                            CryptographicOperations.FixedTimeEquals(currentHash, expected)) Clipboard.Clear();
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(currentHash);
                }
            }
            catch (COMException)
            {
                // Clipboard ownership is best-effort; never clear unrelated clipboard data.
            }
            finally
            {
                CryptographicOperations.ZeroMemory(expected);
                lock (sync)
                {
                    if (expectedGeneration == ownershipGeneration)
                    {
                        ClearOwnedHash();
                        expiration?.Cancel();
                        expiration?.Dispose();
                        expiration = null;
                    }
                }
            }
        });
    }

    private async Task ClearAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(ClearDelay, cancellationToken);
            await ClearOwnedValueAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static Task RunOnUiThreadAsync(Func<Task> action)
    {
        if (App.DispatcherQueue.HasThreadAccess) return action();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!App.DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    await action();
                    completion.SetResult();
                }
                catch (Exception exception)
                {
                    completion.SetException(exception);
                }
            }))
        {
            completion.SetException(new InvalidOperationException("The PasswordTool window is no longer available."));
        }

        return completion.Task;
    }

    private void ClearOwnedHash()
    {
        if (ownedValueHash is null) return;
        CryptographicOperations.ZeroMemory(ownedValueHash);
        ownedValueHash = null;
    }

    private static byte[] HashValue(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        try { return SHA256.HashData(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lock (sync)
        {
            expiration?.Cancel();
            expiration?.Dispose();
            expiration = null;
            ClearOwnedHash();
        }
    }
}
