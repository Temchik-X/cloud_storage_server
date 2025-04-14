using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Application.Services
{
    public static class PasswordHasher
    {
        private const int SaltSize = 16; // Размер соли (в байтах)
        private const int HashSize = 32; // Размер хеша (в байтах)
        private const int Iterations = 10000; // Количество итераций

        public static string HashPassword(string password)
        {
            using var deriveBytes = new Rfc2898DeriveBytes(password, SaltSize, Iterations, HashAlgorithmName.SHA256);
            byte[] salt = deriveBytes.Salt;
            byte[] hash = deriveBytes.GetBytes(HashSize);

            return Convert.ToBase64String(salt) + "." + Convert.ToBase64String(hash);
        }

        public static bool VerifyPassword(string password, string hashedPassword)
        {
            var parts = hashedPassword.Split('.');
            if (parts.Length != 2) return false;

            byte[] salt = Convert.FromBase64String(parts[0]);
            byte[] hash = Convert.FromBase64String(parts[1]);

            using var deriveBytes = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
            byte[] computedHash = deriveBytes.GetBytes(HashSize);

            return CryptographicOperations.FixedTimeEquals(computedHash, hash);
        }
    }
}
