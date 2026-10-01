using PasswordTool.Presentation;
using PasswordTool.Core.Services;

namespace PasswordTool.Presentation.Tests;

public sealed class AppFlowCoordinatorTests
{
    [Fact]
    public async Task Lock_discards_a_completed_read_and_unlock_waiting_to_return()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PasswordTool.Presentation.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var totp = new TotpService();
            var secret = totp.GenerateSecret();
            using var vault = new VaultService(new VaultStorageService(directory), new EncryptionService(), totp);
            vault.InitializeNewVault("correct horse battery staple", secret, totp.GetCurrentCode(secret).Code);
            var runner = new DelayedResultRunner();
            var flow = new AppFlowCoordinator(vault, runner, totp);
            var read = flow.GetListItemsAsync();
            await runner.Completed.Task;
            await flow.LockAsync();
            runner.Release.SetResult();
            await Assert.ThrowsAsync<OperationCanceledException>(() => read);

            runner = new DelayedResultRunner();
            flow = new AppFlowCoordinator(vault, runner, totp);
            var unlock = flow.UnlockAsync("correct horse battery staple", string.Empty);
            await runner.Completed.Task;
            await flow.LogoutAsync();
            runner.Release.SetResult();
            Assert.False((await unlock).Success);
            Assert.False(flow.IsSignedIn);
            Assert.Equal(AppFlowState.Unlock, flow.FlowState);
            Assert.Throws<InvalidOperationException>(() => vault.GetItems());
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class DelayedResultRunner : IVaultOperationRunner
    {
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<T> RunAsync<T>(Func<T> operation, CancellationToken cancellationToken = default)
        {
            var result = operation();
            Completed.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return result;
        }
        public Task RunAsync(Action operation, CancellationToken cancellationToken = default)
        {
            operation();
            return Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Expiry_denies_operations_in_both_vault_states_and_requires_fresh_totp(bool locked)
    {
        var directory = Path.Combine(Path.GetTempPath(), "PasswordTool.Presentation.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var now = DateTimeOffset.UtcNow;
            var totp = new TotpService();
            var secret = totp.GenerateSecret();
            using var vault = new VaultService(new VaultStorageService(directory), new EncryptionService(), totp, utcNow: () => now);
            const string password = "correct horse battery staple";
            vault.InitializeNewVault(password, secret, totp.GetCurrentCode(secret).Code);
            using var runner = new VaultOperationRunner();
            var flow = new AppFlowCoordinator(vault, runner, totp);
            if (locked) await flow.LockAsync();
            now = now.AddHours(5);
            Assert.False(flow.IsSignedIn);
            await Assert.ThrowsAsync<OperationCanceledException>(() => flow.GetListItemsAsync());
            await flow.LockAsync();
            Assert.Equal(AppFlowState.Unlock, flow.FlowState);
            Assert.False((await flow.UnlockAsync(password, string.Empty)).Success);
            Assert.True((await flow.UnlockAsync(password, totp.GetCurrentCode(secret).Code)).Success);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Settings_completion_after_lock_is_cancelled_without_a_fatal_error()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PasswordTool.Presentation.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var totp = new TotpService();
            var secret = totp.GenerateSecret();
            using var vault = new VaultService(new VaultStorageService(directory), new EncryptionService(), totp);
            const string password = "correct horse battery staple";
            vault.InitializeNewVault(password, secret, totp.GetCurrentCode(secret).Code);
            var runner = new DelayedResultRunner();
            var flow = new AppFlowCoordinator(vault, runner, totp);
            var settings = new SettingsViewModel(flow, new UserErrorMapper());
            var save = settings.SaveAsync(password);
            await runner.Completed.Task;
            await flow.LockAsync();
            runner.Release.SetResult();
            Assert.False(await save);
            Assert.False(settings.IsStatusOpen);
            Assert.True(flow.IsSignedIn);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
    [Theory]
    [InlineData(false, false, AppFlowState.FirstLaunch)]
    [InlineData(true, false, AppFlowState.Unlock)]
    [InlineData(false, true, AppFlowState.Recover)]
    public void ResolveInitialState_MapsStorageState(bool initialized, bool partialStorage, AppFlowState expected)
    {
        Assert.Equal(expected, AppFlowCoordinator.ResolveInitialState(initialized, partialStorage));
    }

    [Fact]
    public async Task Master_password_unlock_requires_valid_authenticator_code()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PasswordTool.Presentation.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var totp = new TotpService();
            var secret = totp.GenerateSecret();
            var code = totp.GetCurrentCode(secret).Code;
            var wrongCode = Enumerable.Range(0, 10).Select(i => i.ToString("D6"))
                .First(candidate => !totp.VerifyCode(secret, candidate));
            using var vault = new VaultService(new VaultStorageService(directory), new EncryptionService(), totp);
            vault.InitializeNewVault("correct horse battery staple", secret, code);
            vault.ClearSession();

            using var runner = new VaultOperationRunner();
            var flow = new AppFlowCoordinator(vault, runner, totp);
            var rejected = await flow.UnlockAsync("correct horse battery staple", wrongCode);

            Assert.False(rejected.Success);
            Assert.Equal(AppFlowState.Unlock, flow.FlowState);
            Assert.Throws<InvalidOperationException>(() => vault.GetItems());

            var unlocked = await flow.UnlockAsync("correct horse battery staple", code);
            Assert.True(unlocked.Success);
            Assert.Equal(AppFlowState.Unlocked, flow.FlowState);
            await flow.LockAsync();
            Assert.True(flow.IsSignedIn);
            Assert.Equal(AppFlowState.Unlock, flow.FlowState);
            Assert.Throws<InvalidOperationException>(() => vault.GetItems());
            Assert.True((await flow.UnlockAsync("correct horse battery staple", string.Empty)).Success);
            await flow.LogoutAsync();
            Assert.False(flow.IsSignedIn);
            Assert.False((await flow.UnlockAsync("correct horse battery staple", string.Empty)).Success);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
