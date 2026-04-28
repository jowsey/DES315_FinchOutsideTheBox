using System;
using System.Security.Cryptography;

namespace Util
{
    public static class Base64Url
    {
        public static string Generate(int length)
        {
            var bytes = new byte[(length * 3 + 3) / 4];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=')[..length];
        }
    }
}