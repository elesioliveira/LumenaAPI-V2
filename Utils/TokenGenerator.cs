using System.Security.Cryptography;

public static class TokenGenerator
{
    public static string GenerateRecoveryToken()
    {
        using var rng = RandomNumberGenerator.Create();
        byte[] bytes = new byte[22];
        rng.GetBytes(bytes);

        // String segura com 128 caracteres hexadecimal
        return Convert.ToHexString(bytes);
    }
}