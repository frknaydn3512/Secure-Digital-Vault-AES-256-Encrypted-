using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SecretNotepad
{
    public static class CryptoHelper
    {
        private static readonly byte[] Salt = new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 };
        private const int Iterations = 300000; // 300 Bin döngü: Brute-Force saldırılarını patlatır!

        // Şifreyi doğrulamak için geri döndürülemez Hash (Özet) oluşturur
        public static string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("x2")); // Hex formatına çevirir
                }
                return builder.ToString();
            }
        }

        public static string Encrypt(string clearText, string password)
        {
            if (string.IsNullOrEmpty(clearText)) return clearText;
            byte[] clearBytes = Encoding.UTF8.GetBytes(clearText);

            using (Aes encryptor = Aes.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(password, Salt, Iterations, HashAlgorithmName.SHA256);
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(clearBytes, 0, clearBytes.Length);
                        cs.Close();
                    }
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        public static string Decrypt(string cipherText, string password)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;
            try
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherText);

                using (Aes encryptor = Aes.Create())
                {
                    Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(password, Salt, Iterations, HashAlgorithmName.SHA256);
                    encryptor.Key = pdb.GetBytes(32);
                    encryptor.IV = pdb.GetBytes(16);

                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(cipherBytes, 0, cipherBytes.Length);
                            cs.Close();
                        }
                        return Encoding.UTF8.GetString(ms.ToArray());
                    }
                }
            }
            catch
            {
                return null; // Şifre yanlışsa veya dosya bozuksa buraya düşer
            }
        }
    }
}