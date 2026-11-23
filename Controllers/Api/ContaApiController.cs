using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PousadaMareMansa.Web.Data;
using PousadaMareMansa.Web.Models;
using PousadaMareMansa.Web.Services;
namespace PousadaMareMansa.Web.Controllers.Api;

/// <summary>Conta do hóspede na API (usada pelo app): cadastro, login (JWT), reservas e avaliações.</summary>
[Route("api/v1/conta")]
public class ContaApiController : ApiBase
{
    private const string Bearer = JwtBearerDefaults.AuthenticationScheme;
    private readonly ContaRepository _repo;
    private readonly ContaService _conta;
    private readonly JwtService _jwt;
    public ContaApiController(ContaRepository repo, ContaService conta, JwtService jwt) { _repo = repo; _conta = conta; _jwt = jwt; }

    private string BaseUrl => $"{Request.Scheme}://{Request.Host}";
    private int IdAtual => int.Parse(User.FindFirst("sub")!.Value);

    /// <summary>Cria a conta, envia o e-mail de confirmação e já devolve o token.</summary>
    [HttpPost("cadastro"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<ActionResult<HospedeLoginResponse>> Cadastro(CadastroRequest r)
    {
        var (hash, salt) = PasswordHasher.Gerar(r.Senha);
        var id = await _repo.CadastrarAsync(r.Nome, r.Email, r.Telefone, hash, salt);
        await _conta.EnviarConfirmacaoAsync(id, r.Nome.Trim(), r.Email.Trim().ToLowerInvariant(), BaseUrl);
        var p = (await _repo.PerfilAsync(id))!;
        var (token, expira) = _jwt.GerarHospede(p);
        return StatusCode(201, new HospedeLoginResponse { Token = token, ExpiraEm = expira, Perfil = p });
    }

    /// <summary>Login do hóspede (5 tentativas por minuto; 5 senhas erradas seguidas bloqueiam a conta por 15 min).</summary>
    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<ActionResult<HospedeLoginResponse>> Login(EntrarRequest r)
    {
        var (conta, erro) = await _conta.AutenticarAsync(r.Email, r.Senha);
        if (conta is null) return Unauthorized(new ProblemDetails { Status = 401, Title = erro });
        var p = (await _repo.PerfilAsync(conta.IdHospede))!;
        var (token, expira) = _jwt.GerarHospede(p);
        return new HospedeLoginResponse { Token = token, ExpiraEm = expira, Perfil = p };
    }

    /// <summary>Envia o link para criar nova senha. Sempre responde 204 (não revela se o e-mail existe).</summary>
    [HttpPost("esqueci-senha"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> Esqueci(EmailRequest r)
    {
        await _conta.EnviarRedefinicaoAsync(r.Email, BaseUrl);
        return NoContent();
    }

    [HttpGet("me"), Authorize(AuthenticationSchemes = Bearer, Roles = "Hospede")]
    public async Task<ActionResult<PerfilHospede>> Me()
    {
        var p = await _repo.PerfilAsync(IdAtual);
        return p is null ? NotFound() : p;
    }

    [HttpPost("reenviar-confirmacao"), Authorize(AuthenticationSchemes = Bearer, Roles = "Hospede")]
    public async Task<IActionResult> Reenviar()
    {
        var p = await _repo.PerfilAsync(IdAtual);
        if (p is null) return NotFound();
        await _conta.EnviarConfirmacaoAsync(p.IdHospede, p.Nome, p.Email, BaseUrl);
        return NoContent();
    }

    /// <summary>Reservas da conta (exige e-mail confirmado: 400 com a mensagem da regra caso contrário).</summary>
    [HttpGet("reservas"), Authorize(AuthenticationSchemes = Bearer, Roles = "Hospede")]
    public async Task<ActionResult<List<MinhaReserva>>> Reservas() => await _repo.MinhasReservasAsync(IdAtual);

    [HttpPost("reservas/{codigo}/cancelar"), Authorize(AuthenticationSchemes = Bearer, Roles = "Hospede")]
    public async Task<ActionResult<object>> Cancelar(string codigo)
        => new { gratuito = await _repo.CancelarAsync(IdAtual, codigo) };

    [HttpPost("reservas/{codigo}/avaliacao"), Authorize(AuthenticationSchemes = Bearer, Roles = "Hospede")]
    public async Task<IActionResult> Avaliar(string codigo, AvaliacaoRequest r)
    {
        await _repo.AvaliarAsync(IdAtual, codigo, r.Nota, r.Comentario);
        return NoContent();
    }
}
