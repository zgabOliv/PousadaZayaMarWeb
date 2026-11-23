using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PousadaMareMansa.Web.Data;
using PousadaMareMansa.Web.Models;
using PousadaMareMansa.Web.Services;
namespace PousadaMareMansa.Web.Controllers.Api;

[Route("api/v1/auth")]
public class AuthController : ApiBase
{
    private readonly PousadaRepository _repo;
    private readonly JwtService _jwt;
    public AuthController(PousadaRepository repo, JwtService jwt) { _repo = repo; _jwt = jwt; }

    /// <summary>Autentica um administrador e devolve o token JWT (limite: 5 tentativas por minuto).</summary>
    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest r)
    {
        var u = await _repo.ObterAdminAsync(r.Login);
        if (u is null || !PasswordHasher.Verificar(r.Senha, u.SenhaHash, u.Salt))
            return Unauthorized(new ProblemDetails { Status = 401, Title = "Usuário ou senha incorretos." });
        var (token, expira) = _jwt.Gerar(u);
        return new LoginResponse { Token = token, ExpiraEm = expira, Perfil = u.Perfil };
    }
}
