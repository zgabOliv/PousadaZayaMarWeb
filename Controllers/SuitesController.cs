using Microsoft.AspNetCore.Mvc;
using PousadaMareMansa.Web.Data;
using PousadaMareMansa.Web.Models;
namespace PousadaMareMansa.Web.Controllers;

public class SuitesController : Controller
{
    private readonly PousadaRepository _repo;
    private readonly ContaRepository _conta;
    public SuitesController(PousadaRepository repo, ContaRepository conta) { _repo = repo; _conta = conta; }

    public async Task<IActionResult> Index() => View(await _repo.ListarTiposAsync());

    /// <param name="mes">Mês do calendário no formato yyyy-MM (padrão: mês atual).</param>
    public async Task<IActionResult> Detalhe(int id, string? mes)
    {
        var tipo = await _conta.DetalheTipoAsync(id);
        if (tipo is null) return NotFound();
        var m = DateTime.TryParseExact(mes, "yyyy-MM", null, System.Globalization.DateTimeStyles.None, out var d)
            ? new DateTime(d.Year, d.Month, 1) : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        // Limita a navegação a 12 meses à frente e nunca antes do mês atual.
        var atual = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        if (m < atual) m = atual;
        if (m > atual.AddMonths(12)) m = atual.AddMonths(12);
        return View(new DetalheSuiteViewModel
        {
            Tipo = tipo,
            Mes = m,
            Dias = await _conta.CalendarioAsync(id, m),
            Avaliacoes = await _conta.ListarAvaliacoesAsync(id, 10)
        });
    }
}
