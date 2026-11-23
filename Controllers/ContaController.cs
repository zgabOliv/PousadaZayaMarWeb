using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PousadaMareMansa.Web.Data;
using PousadaMareMansa.Web.Models;
using PousadaMareMansa.Web.Services;
namespace PousadaMareMansa.Web.Controllers;

/// <summary>Conta do hóspede (cookie "Hospede", separado do cookie do painel administrativo).</summary>
public class ContaController : Controller
{
    public const string Esquema = "Hospede";
    private readonly ContaRepository _repo;
    private readonly ContaService _conta;
    public ContaController(ContaRepository repo, ContaService conta) { _repo = repo; _conta = conta; }

    private string BaseUrl => $"{Request.Scheme}://{Request.Host}";
    private int IdAtual => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private void AddErro(SqlException ex) =>
        ModelState.AddModelError("", ex.Number >= 50000 ? ex.Message : "Erro ao acessar o sistema. Tente novamente.");

    private async Task EfetuarLogin(int id, string nome)
    {
        var ident = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Name, nome) }, Esquema);
        await HttpContext.SignInAsync(Esquema, new ClaimsPrincipal(ident));
    }

    // ---------- cadastro ----------
    [HttpGet] public IActionResult Cadastro() => View(new CadastroModel());

    [HttpPost]
    public async Task<IActionResult> Cadastro(CadastroModel m)
    {
        if (!ModelState.IsValid) return View(m);
        try
        {
            var (hash, salt) = PasswordHasher.Gerar(m.Senha);
            var id = await _repo.CadastrarAsync(m.Nome, m.Email, m.Telefone, hash, salt);
            var link = await _conta.EnviarConfirmacaoAsync(id, m.Nome.Trim(), m.Email.Trim().ToLowerInvariant(), BaseUrl);
            await EfetuarLogin(id, m.Nome.Trim());
            TempData["Ok"] = "Conta criada! Enviamos um link de confirmação para o seu e-mail.";
            if (_conta.MostrarLinks) TempData["Link"] = link;
            return RedirectToAction(nameof(MinhasReservas));
        }
        catch (SqlException ex) { AddErro(ex); return View(m); }
    }

    // ---------- entrar / sair ----------
    [HttpGet] public IActionResult Entrar(string? returnUrl) => View(new EntrarModel { ReturnUrl = returnUrl });

    [HttpPost]
    public async Task<IActionResult> Entrar(EntrarModel m)
    {
        if (!ModelState.IsValid) return View(m);
        var (conta, erro) = await _conta.AutenticarAsync(m.Email, m.Senha);
        if (conta is null) { ModelState.AddModelError("", erro!); return View(m); }
        await EfetuarLogin(conta.IdHospede, conta.Nome);
        return Url.IsLocalUrl(m.ReturnUrl) ? LocalRedirect(m.ReturnUrl!) : RedirectToAction(nameof(MinhasReservas));
    }

    [HttpPost]
    public async Task<IActionResult> Sair()
    {
        await HttpContext.SignOutAsync(Esquema);
        return RedirectToAction("Index", "Home");
    }

    // ---------- confirmar e-mail (GET só mostra o botão; o POST consome o token) ----------
    [HttpGet] public IActionResult Confirmar(string token) => View((object)token);

    [HttpPost]
    public async Task<IActionResult> ConfirmarPost(string token)
    {
        try
        {
            await _repo.ConfirmarEmailAsync(TokenService.Hash(token ?? ""));
            TempData["Ok"] = "E-mail confirmado! Agora você pode ver e avaliar suas reservas.";
        }
        catch (SqlException ex) { TempData["Erro"] = ex.Number >= 50000 ? ex.Message : "Erro ao confirmar."; }
        return RedirectToAction(nameof(MinhasReservas));
    }

    [HttpPost, Authorize(AuthenticationSchemes = Esquema)]
    public async Task<IActionResult> ReenviarConfirmacao()
    {
        var p = await _repo.PerfilAsync(IdAtual);
        if (p is null) return RedirectToAction(nameof(Entrar));
        var link = await _conta.EnviarConfirmacaoAsync(p.IdHospede, p.Nome, p.Email, BaseUrl);
        TempData["Ok"] = "Enviamos um novo link para o seu e-mail.";
        if (_conta.MostrarLinks) TempData["Link"] = link;
        return RedirectToAction(nameof(MinhasReservas));
    }

    // ---------- esqueci a senha ----------
    [HttpGet] public IActionResult Esqueci() => View(new EsqueciModel());

    [HttpPost]
    public async Task<IActionResult> Esqueci(EsqueciModel m)
    {
        if (!ModelState.IsValid) return View(m);
        var link = await _conta.EnviarRedefinicaoAsync(m.Email, BaseUrl);
        // Resposta igual exista a conta ou não (não revela quais e-mails têm cadastro).
        TempData["Ok"] = "Se o e-mail tiver cadastro, enviamos um link para criar uma nova senha.";
        if (_conta.MostrarLinks && link is not null) TempData["Link"] = link;
        return RedirectToAction(nameof(Entrar));
    }

    [HttpGet] public IActionResult Redefinir(string token) => View(new RedefinirModel { Token = token ?? "" });

    [HttpPost]
    public async Task<IActionResult> Redefinir(RedefinirModel m)
    {
        if (!ModelState.IsValid) return View(m);
        try
        {
            var (hash, salt) = PasswordHasher.Gerar(m.Senha);
            await _repo.RedefinirSenhaAsync(TokenService.Hash(m.Token), hash, salt);
            TempData["Ok"] = "Senha alterada. Entre com a nova senha.";
            return RedirectToAction(nameof(Entrar));
        }
        catch (SqlException ex) { AddErro(ex); return View(m); }
    }

    // ---------- minhas reservas ----------
    [Authorize(AuthenticationSchemes = Esquema)]
    public async Task<IActionResult> MinhasReservas()
    {
        var p = await _repo.PerfilAsync(IdAtual);
        if (p is null) { await HttpContext.SignOutAsync(Esquema); return RedirectToAction(nameof(Entrar)); }
        ViewBag.Perfil = p;
        var lista = new List<MinhaReserva>();
        if (p.EmailConfirmado)
        {
            try { lista = await _repo.MinhasReservasAsync(p.IdHospede); }
            catch (SqlException ex) { TempData["Erro"] = ex.Number >= 50000 ? ex.Message : "Erro ao carregar reservas."; }
        }
        return View(lista);
    }

    [HttpPost, Authorize(AuthenticationSchemes = Esquema)]
    public async Task<IActionResult> Cancelar(string codigo)
    {
        try
        {
            var gratis = await _repo.CancelarAsync(IdAtual, codigo);
            TempData["Ok"] = gratis ? "Reserva cancelada sem custos." : "Reserva cancelada. Como faltam menos de 7 dias, entre em contato sobre eventuais valores.";
        }
        catch (SqlException ex) { TempData["Erro"] = ex.Number >= 50000 ? ex.Message : "Erro ao cancelar."; }
        return RedirectToAction(nameof(MinhasReservas));
    }

    // ---------- avaliar estadia ----------
    [HttpGet, Authorize(AuthenticationSchemes = Esquema)]
    public IActionResult Avaliar(string codigo) => View(new AvaliarModel { Codigo = codigo ?? "" });

    [HttpPost, Authorize(AuthenticationSchemes = Esquema)]
    public async Task<IActionResult> Avaliar(AvaliarModel m)
    {
        if (!ModelState.IsValid) return View(m);
        try
        {
            await _repo.AvaliarAsync(IdAtual, m.Codigo, m.Nota, m.Comentario);
            TempData["Ok"] = "Obrigado pela avaliação!";
            return RedirectToAction(nameof(MinhasReservas));
        }
        catch (SqlException ex) { AddErro(ex); return View(m); }
    }
}
