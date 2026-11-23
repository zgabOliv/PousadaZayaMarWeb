using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PousadaMareMansa.Web.Data;
using PousadaMareMansa.Web.Models;
namespace PousadaMareMansa.Web.Controllers;

public class HomeController : Controller
{
    private readonly PousadaRepository _repo;
    private readonly ContaRepository _conta;
    public HomeController(PousadaRepository repo, ContaRepository conta) { _repo = repo; _conta = conta; }

    public async Task<IActionResult> Index() => View(new HomeViewModel
    {
        Tipos = await _repo.ListarTiposAsync(),
        Avaliacoes = await _conta.ListarAvaliacoesAsync(null, 3),
        Resumo = await _conta.ResumoAvaliacoesAsync(null)
    });

    public IActionResult Regras() => View();

    [HttpGet] public IActionResult Contato() => View(new ContatoModel());

    [HttpPost]
    public async Task<IActionResult> Contato(ContatoModel m)
    {
        if (!ModelState.IsValid) return View(m);
        try { await _repo.EnviarMensagemAsync(m); }
        catch (SqlException) { ModelState.AddModelError("", "Não foi possível enviar agora. Tente novamente em instantes."); return View(m); }
        TempData["Ok"] = "Mensagem enviada. Respondemos no mesmo dia.";
        return RedirectToAction(nameof(Contato));
    }

    public IActionResult Erro() => View();
}
