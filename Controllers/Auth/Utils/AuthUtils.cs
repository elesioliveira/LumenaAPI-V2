// using System.Security.Cryptography;
// using System.Text;

// public static class PasswordHasher
// {
//     public static bool VerifyPassword(string password, byte[] storedHash, byte[] storedSalt)
//     {
//         using var hmac = new HMACSHA512(storedSalt);
//         var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
//         return computedHash.SequenceEqual(storedHash);
//     }
// }
