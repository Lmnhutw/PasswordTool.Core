namespace PasswordTool.Core.Hashers.Educational;

public sealed class Sha512UnsaltedEducationalHasher : LegacyEducationalHasherBase
{
    public Sha512UnsaltedEducationalHasher() : base("LEGACY-SHA512", usesSalt: false)
    {
    }

    public override string AlgorithmName => PasswordHasherNames.Sha512Unsalted;
}
