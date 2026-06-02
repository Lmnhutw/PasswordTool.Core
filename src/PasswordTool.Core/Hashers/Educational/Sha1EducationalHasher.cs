namespace PasswordTool.Core.Hashers.Educational;

public sealed class Sha1EducationalHasher : LegacyEducationalHasherBase
{
    public Sha1EducationalHasher() : base("LEGACY-SHA1", usesSalt: false)
    {
    }

    public override string AlgorithmName => PasswordHasherNames.Sha1;
}
