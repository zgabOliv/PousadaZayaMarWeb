using Microsoft.AspNetCore.Mvc;
using PousadaMareMansa.Web.Data;
using PousadaMareMansa.Web.Models;
namespace PousadaMareMansa.Web.Controllers.Api;

[Route("api/v1/quartos")]
public class QuartosController : ApiBase
{
    private readonly PousadaRepository _repo;
    private readonly ContaRepository _conta;
    public QuartosController(PousadaRepository repo, ContaRepository conta) { _repo = repo; _conta = conta; }

    /// <summary>Lista os tipos de quarto com preço e comodidades.</summary>
    [HttpGet]
    public async Task<ActionResult<List<TipoQuarto>>> Listar() => await _repo.ListarTiposAsync();

    /// <summary>Tipos de quarto livres no período, com o valor total da estadia.</summary>
    [HttpGet("disponibilidade")]
    public async Task<ActionResult<List<Disponibilidade>>> Disponibilidade(
        [FromQuery] DateTime entrada, [FromQuery] DateTime saida, [FromQuery] int hospedes = 2)
    {
        if (hospedes is < 1 or > 8) return BadRequest(new ProblemDetails { Status = 400, Title = "Hóspedes deve ser de 1 a 8." });
        return await _repo.BuscarAsync(entrada, saida, hospedes);
    }

    /// <summary>Detalhe de um tipo de suíte, com nota média.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TipoQuarto>> Detalhe(int id)
        => await _conta.DetalheTipoAsync(id) is { } t ? t : NotFound();

    /// <summary>Calendário do mês (yyyy-MM): suítes livres por noite.</summary>
    [HttpGet("{id:int}/calendario")]
    public async Task<ActionResult<List<DiaCalendario>>> Calendario(int id, [FromQuery] string? mes)
    {
        var m = DateTime.TryParseExact(mes, "yyyy-MM", null, System.Globalization.DateTimeStyles.None, out var d)
            ? new DateTime(d.Year, d.Month, 1) : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        return await _conta.CalendarioAsync(id, m);
    }

    /// <summary>Avaliações com comentário (só o primeiro nome do autor). Sem id: todas as suítes.</summary>
    [HttpGet("avaliacoes")]
    public async Task<ActionResult<List<AvaliacaoPublica>>> Avaliacoes([FromQuery] int? tipo, [FromQuery] int top = 10)
        => await _conta.ListarAvaliacoesAsync(tipo, Math.Clamp(top, 1, 50));
}
