using PasswordTool.Core.Abstractions;
using PasswordTool.Core.Hashers;
using PasswordTool.Core.Models;

namespace PasswordTool.Core.Registry;

public sealed class PasswordHasherRegistry : IPasswordHasherRegistry
{
    private static readonly PasswordHasherDescriptor[] Descriptors =
    [
        new()
        {
            AlgorithmName = PasswordHasherNames.Argon2id,
            SecurityCategory = PasswordHasherSecurityCategory.ProductionSafe,
            IsDefaultRecommendation = true,
            Description = "Recommended modern memory-hard password hashing algorithm."
        },
        new()
        {
            AlgorithmName = PasswordHasherNames.Bcrypt,
            SecurityCategory = PasswordHasherSecurityCategory.ProductionSafe,
            IsDefaultRecommendation = false,
            Description = "Common real-world password hashing format with a configurable work factor."
        },
        new()
        {
            AlgorithmName = PasswordHasherNames.Pbkdf2Sha256,
            SecurityCategory = PasswordHasherSecurityCategory.ProductionSafe,
            IsDefaultRecommendation = false,
            Description = ".NET and enterprise-friendly PBKDF2-HMAC-SHA256 format."
        },
        new()
        {
            AlgorithmName = PasswordHasherNames.Pbkdf2Sha512,
            SecurityCategory = PasswordHasherSecurityCategory.ProductionSafe,
            IsDefaultRecommendation = false,
            Description = "PBKDF2-HMAC-SHA512 variant for comparison."
        },
        new()
        {
            AlgorithmName = PasswordHasherNames.Scrypt,
            SecurityCategory = PasswordHasherSecurityCategory.ProductionSafe,
            IsDefaultRecommendation = false,
            Description = "Memory-hard password hashing algorithm with N, r, and p parameters."
        },
        new()
        {
            AlgorithmName = PasswordHasherNames.AspNetCoreIdentity,
            SecurityCategory = PasswordHasherSecurityCategory.FrameworkFormat,
            IsDefaultRecommendation = false,
            Description = "Framework-specific ASP.NET Core Identity password hash format."
        },
        new()
        {
            AlgorithmName = PasswordHasherNames.Md5,
            SecurityCategory = PasswordHasherSecurityCategory.EducationalOnly,
            IsDefaultRecommendation = false,
            Description = "Educational only. Do not use for password storage."
        },
        new()
        {
            AlgorithmName = PasswordHasherNames.Sha1,
            SecurityCategory = PasswordHasherSecurityCategory.EducationalOnly,
            IsDefaultRecommendation = false,
            Description = "Educational only. Do not use for password storage."
        },
        new()
        {
            AlgorithmName = PasswordHasherNames.Sha256Unsalted,
            SecurityCategory = PasswordHasherSecurityCategory.EducationalOnly,
            IsDefaultRecommendation = false,
            Description = "Educational only. Fast unsalted hashes are unsafe for password storage."
        },
        new()
        {
            AlgorithmName = PasswordHasherNames.Sha512Unsalted,
            SecurityCategory = PasswordHasherSecurityCategory.EducationalOnly,
            IsDefaultRecommendation = false,
            Description = "Educational only. Fast unsalted hashes are unsafe for password storage."
        },
        new()
        {
            AlgorithmName = PasswordHasherNames.Sha256Salted,
            SecurityCategory = PasswordHasherSecurityCategory.EducationalOnly,
            IsDefaultRecommendation = false,
            Description = "Educational only. Salt alone does not make fast hashes safe for password storage."
        },
        new()
        {
            AlgorithmName = PasswordHasherNames.Sha512Salted,
            SecurityCategory = PasswordHasherSecurityCategory.EducationalOnly,
            IsDefaultRecommendation = false,
            Description = "Educational only. Salt alone does not make fast hashes safe for password storage."
        }
    ];

    public IReadOnlyList<PasswordHasherDescriptor> GetAvailableHashers()
    {
        return Descriptors;
    }

    public IPasswordHasher GetHasher(string algorithmName)
    {
        throw new NotImplementedException("Hasher construction will be added when algorithm implementations are built.");
    }
}
