using PasswordTool.Presentation;

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
}
