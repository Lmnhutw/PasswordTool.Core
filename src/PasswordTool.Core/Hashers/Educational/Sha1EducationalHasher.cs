namespace PasswordTool.Core.Hashers.Educational;

public sealed class Sha1EducationalHasher : NotImplementedPasswordHasher
{
    public override string AlgorithmName => PasswordHasherNames.Sha1;
}
