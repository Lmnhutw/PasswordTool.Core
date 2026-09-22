using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PasswordTool.Presentation;

namespace PasswordTool_WinUI;

internal sealed class NavigationDialogService(ISensitiveClipboardService clipboard, DialogLifetime lifetime) : IUserDialogService
{

    public Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmText,
        CancellationToken cancellationToken = default) =>
        ShowConfirmAsync(title, message, confirmText, cancellationToken);

    public async Task ShowErrorAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        await ShowDialogAsync(new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close
        }, cancellationToken);
    }

    public Task<string?> PromptSensitiveTotpAsync(
        string title,
        string message,
        CancellationToken cancellationToken = default) =>
        PromptSecretAsync(title, message, "6-digit code", cancellationToken);

    public Task<string?> PromptBackupPassphraseAsync(
        string title,
        string message,
        CancellationToken cancellationToken = default) =>
        PromptSecretAsync(title, message, "Backup passphrase", cancellationToken);

    public async Task ShowSecretAsync(
        string title,
        string value,
        bool multiline,
        CancellationToken cancellationToken = default)
    {
        var text = new TextBlock
        {
            Text = value,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            IsTextSelectionEnabled = false
        };
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new ScrollViewer { Content = text, MaxHeight = 360 },
            PrimaryButtonText = "Copy securely",
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close
        };
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            args.Cancel = true;
            await clipboard.CopyAsync(value, cancellationToken);
        };
        try { await ShowDialogAsync(dialog, cancellationToken); }
        finally { text.Text = string.Empty; value = string.Empty; }
    }

    private async Task<bool> ShowConfirmAsync(
        string title,
        string message,
        string confirmText,
        CancellationToken cancellationToken)
    {
        var result = await ShowDialogAsync(new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = confirmText,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        }, cancellationToken);
        return result == ContentDialogResult.Primary;
    }

    private async Task<string?> PromptSecretAsync(
        string title,
        string message,
        string header,
        CancellationToken cancellationToken)
    {
        var input = new PasswordBox { Header = header };
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(input);
        var result = await ShowDialogAsync(new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = "Continue",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        }, cancellationToken);
        var value = result == ContentDialogResult.Primary ? input.Password : null;
        input.Password = string.Empty;
        return value;
    }

    private Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog, CancellationToken cancellationToken) =>
        lifetime.ShowAsync(dialog, cancellationToken);
}
