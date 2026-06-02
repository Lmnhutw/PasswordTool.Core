using PasswordTool.Core.Abstractions;
using PasswordTool.Core.Registry;

namespace PasswordTool.Core.Services;

public sealed class PasswordHasherFactory
{
    private readonly IPasswordHasherRegistry registry;

    public PasswordHasherFactory() : this(new PasswordHasherRegistry())
    {
    }

    public PasswordHasherFactory(IPasswordHasherRegistry registry)
    {
        this.registry = registry;
    }

    public IPasswordHasher Create(string algorithmName)
    {
        return registry.GetHasher(algorithmName);
    }
}
