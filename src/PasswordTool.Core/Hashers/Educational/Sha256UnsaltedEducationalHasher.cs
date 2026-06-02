namespace PasswordTool.Core.Hashers.Educational;

public sealed class Sha256UnsaltedEducationalHasher : LegacyEducationalHasherBase
{
    public Sha256UnsaltedEducationalHasher() : base("LEGACY-SHA256", usesSalt: false)
    {
    }

    public override string AlgorithmName => PasswordHasherNames.Sha256Unsalted;
}
