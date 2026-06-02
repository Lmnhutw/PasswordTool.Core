namespace PasswordTool.Core.Hashers.Educational;

public sealed class Md5EducationalHasher : LegacyEducationalHasherBase
{
    public Md5EducationalHasher() : base("LEGACY-MD5", usesSalt: false)
    {
    }

    public override string AlgorithmName => PasswordHasherNames.Md5;
}
