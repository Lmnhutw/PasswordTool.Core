namespace PasswordTool.Presentation;

public sealed class NavigationService : INavigationService
{
    private readonly Stack<AppRoute> backStack = [];

    public AppRoute CurrentRoute { get; private set; } = AppRoute.Vault;

    public void Navigate(AppRoute route)
    {
        if (route == CurrentRoute) return;
        backStack.Push(CurrentRoute);
        CurrentRoute = route;
    }

    public bool TryGoBack()
    {
        if (!backStack.TryPop(out var route)) return false;
        CurrentRoute = route;
        return true;
    }

    public void ResetForLock()
    {
        backStack.Clear();
        CurrentRoute = AppRoute.Vault;
    }
}
