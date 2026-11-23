using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PousadaMareMansa.Web.Data;
using PousadaMareMansa.Web.Models;
using PousadaMareMansa.Web.Services;
namespace PousadaMareMansa.Web.Controllers;

/// <summary>Painel da equipe. Recepção: resumo, reservas, mapa, hóspedes e mensagens. Gerente: tudo, mais suítes, usuários e relatórios.</summary>
[Authorize]
public class AdminController : Controller
{
    private readonly PousadaRepository _site;
    private readonly AdminRepository _admin;
    public AdminController(PousadaRepository site, AdminRepository admin) { _site = site; _admin = admin; }

    private static DateTime Hoje => DateTime.UtcNow.AddHours(-3).Date;   // horário de Brasília (sem horário de verão)
    private string Msg(SqlException ex, string padrao) => ex.Number >= 50000 ? ex.Message : (ex.Number == 2627 || ex.Number == 2601) ? "Esse registro já existe." : padrao;
    private IActionResult Volta(string? volta, string acao) =>
        Url.IsLocalUrl(volta) ? LocalRedirect(volta!) : RedirectToAction(acao);

    // ---------- login ----------
    [AllowAnonymous, HttpGet] public IActionResult Login() => View(new LoginModel());

    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Login(LoginModel m)
    {
        if (!ModelState.IsValid) return View(m);
        var u = await _site.ObterAdminAsync(m.Login);
        // Mensagem única para usuário inexistente e senha errada (não revela qual falhou).
        if (u is null || !PasswordHasher.Verificar(m.Senha, u.SenhaHash, u.Salt))
        {
            ModelState.AddModelError("", "Usuário ou senha incorretos.");
            return View(m);
        }
        var id = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, u.Login), new Claim(ClaimTypes.Role, u.Perfil) },
            CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(new ClaimsPrincipal(id));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous] public IActionResult Negado() => View();

    // ---------- resumo do dia ----------
    public async Task<IActionResult> Index()
    {
        ViewBag.Movimento = await _admin.MovimentoAsync(Hoje);
        return View(await _admin.ResumoAsync());
    }

    [HttpPost]
    public async Task<IActionResult> CheckIn(string codigo, string? volta)
    {
        try { await _admin.CheckInAsync(codigo); TempData["Ok"] = $"Check-in da reserva {codigo} feito."; }
        catch (SqlException ex) { TempData["Erro"] = Msg(ex, "Erro ao fazer o check-in."); }
        return Volta(volta, nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> CheckOut(string codigo, string? volta)
    {
        try { await _admin.CheckOutAsync(codigo); TempData["Ok"] = $"Check-out da reserva {codigo} feito."; }
        catch (SqlException ex) { TempData["Erro"] = Msg(ex, "Erro ao fazer o check-out."); }
        return Volta(volta, nameof(Index));
    }

    // ---------- reservas ----------
    public async Task<IActionResult> Reservas(string? status, string? q)
    {
        var lista = await _admin.ListarReservasAsync();
        if (!string.IsNullOrWhiteSpace(status)) lista = lista.Where(r => r.Status == status).ToList();
        if (!string.IsNullOrWhiteSpace(q))
            lista = lista.Where(r => r.Codigo.Contains(q, StringComparison.OrdinalIgnoreCase) || r.Hospede.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        ViewBag.Status = status; ViewBag.Q = q;
        return View(lista);
    }

    [HttpGet]
    public async Task<IActionResult> NovaReserva()
    {
        ViewBag.Tipos = await _site.ListarTiposAsync();
        return View(new NovaReservaAdmin { Entrada = Hoje, Saida = Hoje.AddDays(1) });
    }

    [HttpPost]
    public async Task<IActionResult> NovaReserva(NovaReservaAdmin m)
    {
        ViewBag.Tipos = await _site.ListarTiposAsync();
        if (m.Entrada is { } e && m.Saida is { } s && s <= e) ModelState.AddModelError(nameof(m.Saida), "A saída deve ser depois da entrada.");
        if (m.ValorPago is > 0 && string.IsNullOrEmpty(m.Forma)) ModelState.AddModelError(nameof(m.Forma), "Informe a forma de pagamento.");
        if (!ModelState.IsValid) return View(m);
        try
        {
            var codigo = await _admin.CriarReservaAsync(m);
            TempData["Ok"] = $"Reserva {codigo} criada" + (m.ValorPago is > 0 ? " com pagamento registrado." : ". Registre o pagamento para confirmá-la.");
            return RedirectToAction(nameof(Reservas));
        }
        catch (SqlException ex) { ModelState.AddModelError("", Msg(ex, "Erro ao criar a reserva.")); return View(m); }
    }

    [HttpPost]
    public async Task<IActionResult> Pagar(string codigo, string valor, string forma, string? volta)
    {
        if (!Fmt.TentarDecimal(valor, out var v) || v <= 0) { TempData["Erro"] = "Valor inválido."; return Volta(volta, nameof(Reservas)); }
        try { await _admin.PagarAsync(codigo, v, forma); TempData["Ok"] = "Pagamento registrado."; }
        catch (SqlException ex) { TempData["Erro"] = Msg(ex, "Erro ao registrar pagamento."); }
        return Volta(volta, nameof(Reservas));
    }

    [HttpPost, Authorize(Roles = "Gerente")]
    public async Task<IActionResult> CancelarReserva(string codigo)
    {
        try { await _admin.CancelarAsync(codigo); TempData["Ok"] = "Reserva cancelada."; }
        catch (SqlException ex) { TempData["Erro"] = Msg(ex, "Erro ao cancelar."); }
        return RedirectToAction(nameof(Reservas));
    }

    // ---------- mapa de ocupação ----------
    public async Task<IActionResult> Mapa(DateTime? data)
    {
        var d = (data ?? Hoje).Date;
        ViewBag.Data = d;
        return View(await _admin.MapaAsync(d));
    }

    // ---------- hóspedes e mensagens ----------
    public async Task<IActionResult> Hospedes() => View(await _admin.ListarHospedesAsync());
    public async Task<IActionResult> Mensagens() => View(await _admin.ListarMensagensAsync());

    [HttpPost]
    public async Task<IActionResult> MarcarMensagem(int id, bool respondida)
    {
        await _admin.MarcarMensagemAsync(id, respondida);
        return RedirectToAction(nameof(Mensagens));
    }

    // ---------- suítes, tarifas e quartos (gerente) ----------
    [Authorize(Roles = "Gerente")]
    public async Task<IActionResult> Suites()
    {
        ViewBag.Quartos = await _admin.ListarQuartosAsync();
        return View(await _admin.ListarTiposAsync());
    }

    [HttpGet, Authorize(Roles = "Gerente")]
    public async Task<IActionResult> TipoEditar(int? id)
    {
        if (id is null) return View(new TipoEdicao());
        var t = (await _admin.ListarTiposAsync()).FirstOrDefault(x => x.IdTipoQuarto == id);
        if (t is null) return NotFound();
        return View(new TipoEdicao { IdTipoQuarto = t.IdTipoQuarto, Nome = t.Nome, Capacidade = t.Capacidade, PrecoDiaria = t.PrecoDiaria,
            Acessivel = t.Acessivel, Descricao = t.Descricao, Imagem = t.Imagem, Comodidades = t.Comodidades });
    }

    [HttpPost, Authorize(Roles = "Gerente")]
    public async Task<IActionResult> TipoEditar(TipoEdicao m)
    {
        if (!ModelState.IsValid) return View(m);
        try { await _admin.SalvarTipoAsync(m); TempData["Ok"] = "Suíte salva. A nova diária vale só para reservas novas."; return RedirectToAction(nameof(Suites)); }
        catch (SqlException ex) { ModelState.AddModelError("", Msg(ex, "Erro ao salvar.")); return View(m); }
    }

    [HttpGet, Authorize(Roles = "Gerente")]
    public async Task<IActionResult> QuartoEditar(int? id)
    {
        ViewBag.Tipos = await _admin.ListarTiposAsync();
        if (id is null) return View(new QuartoEdicao());
        var q = (await _admin.ListarQuartosAsync()).FirstOrDefault(x => x.IdQuarto == id);
        if (q is null) return NotFound();
        return View(new QuartoEdicao { IdQuarto = q.IdQuarto, Numero = q.Numero, IdTipoQuarto = q.IdTipoQuarto, Ativo = q.Ativo });
    }

    [HttpPost, Authorize(Roles = "Gerente")]
    public async Task<IActionResult> QuartoEditar(QuartoEdicao m)
    {
        ViewBag.Tipos = await _admin.ListarTiposAsync();
        if (!ModelState.IsValid) return View(m);
        try { await _admin.SalvarQuartoAsync(m); TempData["Ok"] = "Quarto salvo."; return RedirectToAction(nameof(Suites)); }
        catch (SqlException ex) { ModelState.AddModelError("", Msg(ex, "Erro ao salvar.")); return View(m); }
    }

    // ---------- usuários (gerente) ----------
    [Authorize(Roles = "Gerente")]
    public async Task<IActionResult> Usuarios()
    {
        ViewBag.Novo = new NovoUsuario();
        return View(await _admin.ListarUsuariosAsync());
    }

    [HttpPost, Authorize(Roles = "Gerente")]
    public async Task<IActionResult> CriarUsuario(NovoUsuario m)
    {
        if (!ModelState.IsValid) { TempData["Erro"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)); return RedirectToAction(nameof(Usuarios)); }
        try
        {
            var (hash, salt) = PasswordHasher.Gerar(m.Senha);
            await _admin.CriarAdminAsync(m.Login.Trim(), hash, salt, m.Perfil);
            TempData["Ok"] = $"Usuário {m.Login} criado.";
        }
        catch (SqlException ex) { TempData["Erro"] = Msg(ex, "Erro ao criar usuário."); }
        return RedirectToAction(nameof(Usuarios));
    }

    [HttpPost, Authorize(Roles = "Gerente")]
    public async Task<IActionResult> AlterarUsuario(int id, string perfil, bool ativo)
    {
        try { await _admin.AlterarUsuarioAsync(id, perfil, ativo); TempData["Ok"] = "Usuário atualizado."; }
        catch (SqlException ex) { TempData["Erro"] = Msg(ex, "Erro ao atualizar."); }
        return RedirectToAction(nameof(Usuarios));
    }

    [HttpPost, Authorize(Roles = "Gerente")]
    public async Task<IActionResult> SenhaUsuario(int id, string senha)
    {
        if (string.IsNullOrEmpty(senha) || senha.Length < 8 || senha.Length > 72) { TempData["Erro"] = "A senha deve ter de 8 a 72 caracteres."; return RedirectToAction(nameof(Usuarios)); }
        try { var (h, s) = PasswordHasher.Gerar(senha); await _admin.RedefinirSenhaUsuarioAsync(id, h, s); TempData["Ok"] = "Senha alterada."; }
        catch (SqlException ex) { TempData["Erro"] = Msg(ex, "Erro ao alterar a senha."); }
        return RedirectToAction(nameof(Usuarios));
    }

    // ---------- relatórios (gerente) ----------
    [Authorize(Roles = "Gerente")]
    public async Task<IActionResult> Relatorios(DateTime? inicio, DateTime? fim)
    {
        var h = Hoje;
        var ini = (inicio ?? new DateTime(h.Year, h.Month, 1)).Date;
        var f = (fim ?? ini.AddMonths(1)).Date;
        ViewBag.Inicio = ini; ViewBag.Fim = f;
        try
        {
            ViewBag.Receita = await _admin.ReceitaMensalAsync();
            return View(await _admin.RelatorioAsync(ini, f));
        }
        catch (SqlException ex) { TempData["Erro"] = Msg(ex, "Erro ao gerar o relatório."); ViewBag.Receita = new List<ReceitaMes>(); return View(new List<LinhaRelatorio>()); }
    }
}
