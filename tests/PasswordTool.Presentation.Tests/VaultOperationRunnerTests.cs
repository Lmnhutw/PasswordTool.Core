using PasswordTool.Presentation;

namespace PasswordTool.Presentation.Tests;

public sealed class VaultOperationRunnerTests
{
    [Fact]
    public async Task RunAsync_SerializesConcurrentOperations()
    {
        using var runner = new VaultOperationRunner();
        var active = 0;
        var maximumActive = 0;

        Task RunOne() => runner.RunAsync(() =>
        {
            var current = Interlocked.Increment(ref active);
            maximumActive = Math.Max(maximumActive, current);
            Thread.Sleep(20);
            Interlocked.Decrement(ref active);
        });

        await Task.WhenAll(RunOne(), RunOne(), RunOne());

        Assert.Equal(1, maximumActive);
    }
}
