using System.Security.Cryptography;
using System.Text;

namespace RemoteSentinel.Core.Security;

/// <summary>
/// Clase estática que proporciona métodos para proteger y desproteger cadenas sensibles usando el sistema de encriptación DPAPI de Windows (Data Protection API).
/// </summary>
internal static class SecretProtector
{
    /// Datos adicionales (entropy) que se usan para reforzar la seguridad
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("RemoteSentinel|v1");

    /// Comprueba si una cadena ya está protegida.
    internal static bool IsProtected(string v) => v != null && v.StartsWith("enc:", StringComparison.Ordinal);

    /// Recibe un texto plano y devuelve su versión protegida (cifrada) en Base64, precedida por el prefijo "enc:".
    internal static string Protect(string plain)
    {
        if (string.IsNullOrEmpty(plain)) return "";
        var cipher = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser);
        return "enc:" + Convert.ToBase64String(cipher);
    }

    /// Recibe una cadena y devuelve el texto plano desencriptado.
    internal static string Unprotect(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (!IsProtected(value)) return value;
        var raw = Convert.FromBase64String(value.Substring(4));
        var plain = ProtectedData.Unprotect(raw, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plain);
    }
}
