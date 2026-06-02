namespace PasswordTool.Core.Hashers.Educational;

public sealed class Sha512UnsaltedEducationalHasher : NotImplementedPasswordHasher
{
    public override string AlgorithmName => PasswordHasherNames.Sha512Unsalted;
}
