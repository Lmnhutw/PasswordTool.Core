namespace PasswordTool.Core.Hashers.Educational;

public sealed class Sha512SaltedEducationalHasher : LegacyEducationalHasherBase
{
    public Sha512SaltedEducationalHasher() : base("LEGACY-SHA512-SALTED", usesSalt: true)
    {
    }

    public override string AlgorithmName => PasswordHasherNames.Sha512Salted;
}
