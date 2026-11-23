using Microsoft.Extensions.Diagnostics.HealthChecks;
using PousadaMareMansa.Web.Data;
namespace PousadaMareMansa.Web.Services;

/// <summary>Verifica se o banco responde. Usado pelo monitoramento em /health/ready.</summary>
public class BancoHealthCheck : IHealthCheck
{
    private readonly Db _db;
    public BancoHealthCheck(Db db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await using var c = _db.Site();
            await c.OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync(ct);
            return HealthCheckResult.Healthy("Banco acessível.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Banco indisponível.", ex);
        }
    }
}
