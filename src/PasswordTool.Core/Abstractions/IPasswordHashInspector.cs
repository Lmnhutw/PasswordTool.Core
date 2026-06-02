using PasswordTool.Core.Models;

namespace PasswordTool.Core.Abstractions;

public interface IPasswordHashInspector
{
    PasswordHashInfo Inspect(string storedHash);
}
