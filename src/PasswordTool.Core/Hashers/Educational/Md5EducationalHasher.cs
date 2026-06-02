namespace PasswordTool.Core.Hashers.Educational;

public sealed class Md5EducationalHasher : NotImplementedPasswordHasher
{
    public override string AlgorithmName => PasswordHasherNames.Md5;
}
