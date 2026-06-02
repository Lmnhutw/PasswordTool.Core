using PasswordTool.Core.Models;

namespace PasswordTool.Core.Abstractions;

public interface IPasswordHasherRegistry
{
    IReadOnlyList<PasswordHasherDescriptor> GetAvailableHashers();

    IPasswordHasher GetHasher(string algorithmName);
}
