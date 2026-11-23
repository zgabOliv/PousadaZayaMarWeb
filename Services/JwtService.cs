using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using PousadaMareMansa.Web.Models;
namespace PousadaMareMansa.Web.Services;

/// <summary>Gera tokens JWT (HS256, 2 horas) com nome e perfil do administrador.</summary>
public class JwtService
{
    private readonly IConfiguration _cfg;
    public JwtService(IConfiguration cfg) => _cfg = cfg;

    public (string Token, DateTime Expira) Gerar(AdminUser u)
    {
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg["Jwt:Key"]!));
        var expira = DateTime.UtcNow.AddHours(2);
        var jwt = new JwtSecurityToken(
            issuer: _cfg["Jwt:Issuer"], audience: _cfg["Jwt:Audience"],
            claims: new[]
            {
                new System.Security.Claims.Claim("sub", u.Login),
                new System.Security.Claims.Claim("name", u.Login),
                new System.Security.Claims.Claim("role", u.Perfil)
            },
            expires: expira,
            signingCredentials: new SigningCredentials(chave, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expira);
    }

    /// <summary>Token do hóspede (perfil "Hospede", 8 horas). Não serve para as rotas administrativas.</summary>
    public (string Token, DateTime Expira) GerarHospede(PerfilHospede p)
    {
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg["Jwt:Key"]!));
        var expira = DateTime.UtcNow.AddHours(8);
        var jwt = new JwtSecurityToken(
            issuer: _cfg["Jwt:Issuer"], audience: _cfg["Jwt:Audience"],
            claims: new[]
            {
                new System.Security.Claims.Claim("sub", p.IdHospede.ToString()),
                new System.Security.Claims.Claim("name", p.Nome),
                new System.Security.Claims.Claim("role", "Hospede")
            },
            expires: expira,
            signingCredentials: new SigningCredentials(chave, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expira);
    }
}
