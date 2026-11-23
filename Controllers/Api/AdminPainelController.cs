using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PousadaMareMansa.Web.Data;
using PousadaMareMansa.Web.Models;
namespace PousadaMareMansa.Web.Controllers.Api;

/// <summary>Resumo do dia, mapa de ocupação, hóspedes, mensagens e relatórios para o app da equipe.</summary>
[Route("api/v1/admin")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Gerente,Recepcao")]
public class AdminPainelController : ApiBase
{
    private readonly AdminRepository _repo;
    public AdminPainelController(AdminRepository repo) => _repo = repo;
    private static DateTime Hoje => DateTime.UtcNow.AddHours(-3).Date;

    [HttpGet("painel")] public async Task<ActionResult<ResumoPainel>> Painel() => await _repo.ResumoAsync();

    /// <summary>Entradas e saídas do dia (padrão: hoje).</summary>
    [HttpGet("movimento")]
    public async Task<ActionResult<List<MovimentoItem>>> Movimento([FromQuery] DateTime? data) => await _repo.MovimentoAsync(data ?? Hoje);

    /// <summary>Situação de cada quarto em um dia (Livre, Reservado, Pendente, Hospedado, Saiu).</summary>
    [HttpGet("mapa")]
    public async Task<ActionResult<List<QuartoMapa>>> Mapa([FromQuery] DateTime? data) => await _repo.MapaAsync(data ?? Hoje);

    [HttpGet("hospedes")] public async Task<ActionResult<List<HospedeAdmin>>> Hospedes() => await _repo.ListarHospedesAsync();

    [HttpGet("mensagens")] public async Task<ActionResult<List<MensagemAdmin>>> Mensagens() => await _repo.ListarMensagensAsync();

    [HttpPost("mensagens/{id:int}/respondida")]
    public async Task<IActionResult> Respondida(int id, [FromQuery] bool valor = true) { await _repo.MarcarMensagemAsync(id, valor); return NoContent(); }

    /// <summary>Ocupação por suíte no período (somente Gerente).</summary>
    [HttpGet("relatorios/ocupacao"), Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Gerente")]
    public async Task<ActionResult<List<LinhaRelatorio>>> Ocupacao([FromQuery] DateTime inicio, [FromQuery] DateTime fim)
        => await _repo.RelatorioAsync(inicio, fim);

    /// <summary>Receita recebida por mês (somente Gerente).</summary>
    [HttpGet("relatorios/receita"), Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Gerente")]
    public async Task<ActionResult<List<ReceitaMes>>> Receita() => await _repo.ReceitaMensalAsync();
}
