namespace PasswordTool.Core.Hashers.Educational;

public sealed class Sha256SaltedEducationalHasher : LegacyEducationalHasherBase
{
    public Sha256SaltedEducationalHasher() : base("LEGACY-SHA256-SALTED", usesSalt: true)
    {
    }

    public override string AlgorithmName => PasswordHasherNames.Sha256Salted;
}
