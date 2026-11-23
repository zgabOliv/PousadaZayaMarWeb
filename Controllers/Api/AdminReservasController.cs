using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PousadaMareMansa.Web.Data;
using PousadaMareMansa.Web.Models;
namespace PousadaMareMansa.Web.Controllers.Api;

/// <summary>Área administrativa da API: exige token JWT com perfil Gerente ou Recepcao.</summary>
[Route("api/v1/admin/reservas")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Gerente,Recepcao")]
public class AdminReservasController : ApiBase
{
    private readonly AdminRepository _repo;
    public AdminReservasController(AdminRepository repo) => _repo = repo;

    /// <summary>Lista todas as reservas com valores total e pago.</summary>
    [HttpGet]
    public async Task<ActionResult<List<ReservaAdmin>>> Listar() => await _repo.ListarReservasAsync();

    /// <summary>Registra um pagamento. A trigger confirma a reserva quando o total pago cobre o valor.</summary>
    [HttpPost("{codigo}/pagamentos")]
    public async Task<IActionResult> Pagar(string codigo, PagamentoRequest p)
    {
        await _repo.PagarAsync(codigo.ToUpperInvariant(), p.Valor, p.Forma);
        return StatusCode(201, new { mensagem = "Pagamento registrado." });
    }

    /// <summary>Cancela uma reserva (somente Gerente).</summary>
    [HttpPost("{codigo}/cancelamento")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Gerente")]
    public async Task<IActionResult> Cancelar(string codigo)
    {
        await _repo.CancelarAsync(codigo.ToUpperInvariant());
        return NoContent();
    }

    /// <summary>Cria uma reserva pela recepção (balcão/telefone), com pagamento opcional na hora (RF22).</summary>
    [HttpPost]
    public async Task<IActionResult> Criar(NovaReservaAdmin m)
    {
        if (m.Entrada is null || m.Saida is null || m.Saida <= m.Entrada)
            return BadRequest(new ProblemDetails { Status = 400, Title = "Período inválido.", Detail = "A saída deve ser depois da entrada." });
        var codigo = await _repo.CriarReservaAsync(m);
        return StatusCode(201, new { codigo });
    }

    /// <summary>Check-in: exige reserva confirmada e a data de entrada já chegada.</summary>
    [HttpPost("{codigo}/checkin")]
    public async Task<IActionResult> CheckIn(string codigo) { await _repo.CheckInAsync(codigo); return NoContent(); }

    /// <summary>Check-out: exige check-in feito e saldo quitado; a reserva passa a Concluida.</summary>
    [HttpPost("{codigo}/checkout")]
    public async Task<IActionResult> CheckOut(string codigo) { await _repo.CheckOutAsync(codigo); return NoContent(); }
}
