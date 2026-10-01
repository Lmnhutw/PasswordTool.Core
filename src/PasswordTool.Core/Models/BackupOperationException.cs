namespace PasswordTool.Core.Models;

// Messages are fixed by the backup workflow, never taken from file contents or passwords.
public sealed class BackupOperationException : Exception
{
    internal BackupOperationException(string message, Exception innerException)
        : base(message, innerException) { }
}
