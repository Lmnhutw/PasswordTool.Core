using PasswordTool.Core.Abstractions;
using PasswordTool.Core.Models;

namespace PasswordTool.Core.Inspection;

public sealed class PasswordHashInspector : IPasswordHashInspector
{
    public PasswordHashInfo Inspect(string storedHash)
    {
        throw new NotImplementedException("Hash inspection parsing has not been implemented yet.");
    }
}
