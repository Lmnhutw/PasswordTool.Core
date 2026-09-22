namespace PasswordTool.Presentation;

public interface INavigationService
{
    AppRoute CurrentRoute { get; }
    void Navigate(AppRoute route);
    bool TryGoBack();
    void ResetForLock();
}

public interface IUserDialogService
{
    Task<bool> ConfirmAsync(string title, string message, string confirmText, CancellationToken cancellationToken = default);
    Task ShowErrorAsync(string title, string message, CancellationToken cancellationToken = default);
    Task<string?> PromptSensitiveTotpAsync(string title, string message, CancellationToken cancellationToken = default);
    Task<string?> PromptBackupPassphraseAsync(string title, string message, CancellationToken cancellationToken = default);
    Task ShowSecretAsync(string title, string value, bool multiline, CancellationToken cancellationToken = default);
}

public interface IFilePickerService
{
    Task<string?> PickOpenPathAsync(CancellationToken cancellationToken = default);
    Task<string?> PickSavePathAsync(string suggestedFileName, CancellationToken cancellationToken = default);
}

public interface ISensitiveClipboardService
{
    Task CopyAsync(string value, CancellationToken cancellationToken = default);
    Task ClearOwnedValueAsync(CancellationToken cancellationToken = default);
}

public interface ISystemLockMonitor
{
    event EventHandler? LockRequired;
    void Start(TimeSpan inactivityTimeout);
    void Stop();
}
