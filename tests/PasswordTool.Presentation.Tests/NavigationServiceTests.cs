using PasswordTool.Presentation;

namespace PasswordTool.Presentation.Tests;

public sealed class NavigationServiceTests
{
    [Fact]
    public void NavigateAndGoBack_PreserveRouteHistory()
    {
        var navigation = new NavigationService();

        navigation.Navigate(AppRoute.Backup);
        navigation.Navigate(AppRoute.Settings);

        Assert.Equal(AppRoute.Settings, navigation.CurrentRoute);
        Assert.True(navigation.TryGoBack());
        Assert.Equal(AppRoute.Backup, navigation.CurrentRoute);
        Assert.True(navigation.TryGoBack());
        Assert.Equal(AppRoute.Vault, navigation.CurrentRoute);
        Assert.False(navigation.TryGoBack());
    }

    [Fact]
    public void ResetForLock_ClearsHistoryAndReturnsToVault()
    {
        var navigation = new NavigationService();
        navigation.Navigate(AppRoute.HashTool);

        navigation.ResetForLock();

        Assert.Equal(AppRoute.Vault, navigation.CurrentRoute);
        Assert.False(navigation.TryGoBack());
    }
}
