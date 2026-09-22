namespace PasswordTool.Presentation;

public interface IVaultOperationRunner
{
    Task<T> RunAsync<T>(Func<T> operation, CancellationToken cancellationToken = default);
    Task RunAsync(Action operation, CancellationToken cancellationToken = default);
}

/// <summary>Serializes all access to the stateful VaultService off the UI thread.</summary>
public sealed class VaultOperationRunner : IVaultOperationRunner, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<T> RunAsync<T>(Func<T> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(operation, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task RunAsync(Action operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Run(operation, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose() => gate.Dispose();
}
