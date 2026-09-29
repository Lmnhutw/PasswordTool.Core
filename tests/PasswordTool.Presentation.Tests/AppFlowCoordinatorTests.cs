using PasswordTool.Presentation;
using PasswordTool.Core.Services;

namespace PasswordTool.Presentation.Tests;

public sealed class AppFlowCoordinatorTests
{
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
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
