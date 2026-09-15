using System.Security.Cryptography;
using System.Text;

namespace TodoApp.Application.Common;

public static class TokenHelper
{
    /// <summary>
    /// Verilen token değerini SHA-256 algoritmasıyla tek yönlü hash'e dönüştürür.
    /// Veritabanında düz metin (plain text) yerine bu hash saklanır (Audit #9).
    /// </summary>
    public static string HashToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

