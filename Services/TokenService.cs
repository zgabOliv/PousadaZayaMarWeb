using System.Security.Cryptography;
using System.Text;
namespace PousadaMareMansa.Web.Services;

/// <summary>Tokens de e-mail (confirmar/redefinir): o banco guarda só o hash SHA-256; o token original vai no link.</summary>
public static class TokenService
{
    public static (string Token, byte[] Hash) Novo()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (token, Hash(token));
    }
    public static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}
