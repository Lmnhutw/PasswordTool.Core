using System.Security.Cryptography;

namespace PasswordTool.Core.Security;

public static class FixedTimeComparison
{
    public static bool Equals(byte[] left, byte[] right)
    {
        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}
