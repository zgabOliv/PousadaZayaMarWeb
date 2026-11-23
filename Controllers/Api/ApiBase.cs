using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
namespace PousadaMareMansa.Web.Controllers.Api;

/// <summary>Converte erros das procedures (THROW 50000+) em respostas HTTP padronizadas (ProblemDetails).</summary>
public class ApiErroFilter : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext ctx)
    {
        if (ctx.Exception is not SqlException ex) return;
        var (status, titulo) = ex.Number switch
        {
            50006 => (404, "Não encontrado"),
            50005 or 50010 => (409, "Conflito de disponibilidade"),
            >= 50000 => (400, "Regra de negócio"),
            _ => (500, "Erro interno")
        };
        ctx.Result = new ObjectResult(new ProblemDetails
        {
            Status = status,
            Title = titulo,
            Detail = ex.Number >= 50000 ? ex.Message : "Falha ao acessar o banco de dados."
        }) { StatusCode = status };
        ctx.ExceptionHandled = true;
    }
}

/// <summary>Base das APIs: validação automática (400), sem CSRF (usa JWT/sem cookie) e erros padronizados.</summary>
[ApiController, IgnoreAntiforgeryToken, ApiErroFilter]
[Produces("application/json")]
public abstract class ApiBase : ControllerBase { }
