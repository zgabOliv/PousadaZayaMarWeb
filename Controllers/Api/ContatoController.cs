using Microsoft.AspNetCore.Mvc;
using PousadaMareMansa.Web.Data;
using PousadaMareMansa.Web.Models;
namespace PousadaMareMansa.Web.Controllers.Api;

[Route("api/v1/contato")]
public class ContatoController : ApiBase
{
    private readonly PousadaRepository _repo;
    public ContatoController(PousadaRepository repo) => _repo = repo;

    /// <summary>Registra uma mensagem de contato.</summary>
    [HttpPost]
    public async Task<IActionResult> Enviar(ContatoModel m)
    {
        await _repo.EnviarMensagemAsync(m);
        return StatusCode(201, new { mensagem = "Mensagem registrada." });
    }
}
