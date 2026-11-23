using Microsoft.AspNetCore.Mvc;
using PousadaMareMansa.Web.Data;
using PousadaMareMansa.Web.Models;
namespace PousadaMareMansa.Web.Controllers.Api;

[Route("api/v1/reservas")]
public class ReservasController : ApiBase
{
    private readonly PousadaRepository _repo;
    public ReservasController(PousadaRepository repo) => _repo = repo;

    /// <summary>Cria uma reserva. Retorna 201 com o código; 409 se não houver quarto livre.</summary>
    [HttpPost]
    public async Task<IActionResult> Criar(ReservaInput m)
    {
        var codigo = await _repo.CriarReservaAsync(m);
        return StatusCode(201, new { codigo });
    }

    /// <summary>Consulta uma reserva por código e e-mail (POST para o e-mail não ir na URL).</summary>
    [HttpPost("consulta")]
    public async Task<ActionResult<ReservaInfo>> Consultar(ReservaLookup r)
    {
        var info = await _repo.ConsultarAsync(r.Codigo, r.Email);
        return info is null ? NotFound(new ProblemDetails { Status = 404, Title = "Reserva não encontrada." }) : info;
    }

    /// <summary>Cancela a reserva do hóspede. "gratuito" é true se faltavam 7+ dias para a entrada.</summary>
    [HttpPost("cancelamento")]
    public async Task<IActionResult> Cancelar(ReservaLookup r)
    {
        var gratuito = await _repo.CancelarAsync(r.Codigo, r.Email);
        return Ok(new { gratuito });
    }
}
