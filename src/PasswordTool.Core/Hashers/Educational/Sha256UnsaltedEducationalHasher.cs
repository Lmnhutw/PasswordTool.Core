namespace PasswordTool.Core.Hashers.Educational;

public sealed class Sha256UnsaltedEducationalHasher : NotImplementedPasswordHasher
{
    public override string AlgorithmName => PasswordHasherNames.Sha256Unsalted;
}
