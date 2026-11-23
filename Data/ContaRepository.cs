using System.Data;
using Microsoft.Data.SqlClient;
using PousadaMareMansa.Web.Models;
namespace PousadaMareMansa.Web.Data;

/// <summary>Contas de hóspede, e-mails, reservas da conta, avaliações e calendário. Só stored procedures.</summary>
public class ContaRepository
{
    private readonly Db _db;
    public ContaRepository(Db db) => _db = db;

    private static string? S(SqlDataReader r, string col) => r[col] is string s ? s : null;

    public async Task<int> CadastrarAsync(string nome, string email, string telefone, byte[] hash, byte[] salt)
    {
        await using var c = _db.Site(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_CadastrarConta");
        cmd.Parameters.Add("@Nome", SqlDbType.NVarChar, 120).Value = nome.Trim();
        cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 160).Value = email.Trim().ToLowerInvariant();
        cmd.Parameters.Add("@Telefone", SqlDbType.VarChar, 20).Value = telefone.Trim();
        cmd.Parameters.Add("@Hash", SqlDbType.VarBinary, 64).Value = hash;
        cmd.Parameters.Add("@Salt", SqlDbType.VarBinary, 32).Value = salt;
        var id = new SqlParameter("@IdHospede", SqlDbType.Int) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(id);
        await cmd.ExecuteNonQueryAsync();
        return (int)id.Value;
    }

    public async Task<ContaDados?> ObterContaAsync(string email)
    {
        await using var c = _db.Site(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_ObterContaPorEmail");
        cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 160).Value = email.Trim().ToLowerInvariant();
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return new ContaDados
        {
            IdHospede = r.GetInt32(r.GetOrdinal("IdHospede")),
            Nome = r.GetString(r.GetOrdinal("Nome")),
            Email = r.GetString(r.GetOrdinal("Email")),
            Telefone = S(r, "Telefone"),
            SenhaHash = (byte[])r["SenhaHash"],
            Salt = (byte[])r["Salt"],
            EmailConfirmado = r.GetBoolean(r.GetOrdinal("EmailConfirmado")),
            BloqueadoAte = r["BloqueadoAte"] is DateTime d ? d : null
        };
    }

    public async Task<PerfilHospede?> PerfilAsync(int id)
    {
        await using var c = _db.Site(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_PerfilHospede");
        cmd.Parameters.Add("@IdHospede", SqlDbType.Int).Value = id;
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return new PerfilHospede
        {
            IdHospede = r.GetInt32(r.GetOrdinal("IdHospede")),
            Nome = r.GetString(r.GetOrdinal("Nome")),
            Email = r.GetString(r.GetOrdinal("Email")),
            Telefone = S(r, "Telefone"),
            EmailConfirmado = r.GetBoolean(r.GetOrdinal("EmailConfirmado"))
        };
    }

    public async Task RegistrarLoginAsync(int id, bool sucesso)
    {
        await using var c = _db.Site(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_RegistrarLogin");
        cmd.Parameters.Add("@IdHospede", SqlDbType.Int).Value = id;
        cmd.Parameters.Add("@Sucesso", SqlDbType.Bit).Value = sucesso;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task CriarTokenAsync(int id, string tipo, byte[] tokenHash, int minutos)
    {
        await using var c = _db.Site(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_CriarToken");
        cmd.Parameters.Add("@IdHospede", SqlDbType.Int).Value = id;
        cmd.Parameters.Add("@Tipo", SqlDbType.VarChar, 10).Value = tipo;
        cmd.Parameters.Add("@TokenHash", SqlDbType.VarBinary, 32).Value = tokenHash;
        cmd.Parameters.Add("@Minutos", SqlDbType.Int).Value = minutos;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ConfirmarEmailAsync(byte[] tokenHash)
    {
        await using var c = _db.Site(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_ConfirmarEmail");
        cmd.Parameters.Add("@TokenHash", SqlDbType.VarBinary, 32).Value = tokenHash;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RedefinirSenhaAsync(byte[] tokenHash, byte[] hash, byte[] salt)
    {
        await using var c = _db.Site(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_RedefinirSenha");
        cmd.Parameters.Add("@TokenHash", SqlDbType.VarBinary, 32).Value = tokenHash;
        cmd.Parameters.Add("@Hash", SqlDbType.VarBinary, 64).Value = hash;
        cmd.Parameters.Add("@Salt", SqlDbType.VarBinary, 32).Value = salt;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> RegistrarEmailAsync(string para, string assunto, string corpo)
    {
        await using var c = _db.Site(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_RegistrarEmail");
        cmd.Parameters.Add("@Para", SqlDbType.NVarChar, 160).Value = para;
        cmd.Parameters.Add("@Assunto", SqlDbType.NVarChar, 200).Value = assunto;
        cmd.Parameters.Add("@Corpo", SqlDbType.NVarChar, -1).Value = corpo;
        var id = new SqlParameter("@IdEmail", SqlDbType.Int) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(id);
        await cmd.ExecuteNonQueryAsync();
        return (int)id.Value;
    }

    public async Task MarcarEmailEnviadoAsync(int idEmail)
    {
        await using var c = _db.Site(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_MarcarEmailEnviado");
        cmd.Parameters.Add("@IdEmail", SqlDbType.Int).Value = idEmail;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<MinhaReserva>> MinhasReservasAsync(int id)
    {
        var lista = new List<MinhaReserva>();
        await using var c = _db.Site(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_MinhasReservas");
        cmd.Parameters.Add("@IdHospede", SqlDbType.Int).Value = id;
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            lista.Add(new MinhaReserva
            {
                Codigo = r.GetString(r.GetOrdinal("Codigo")),
                Tipo = r.GetString(r.GetOrdinal("Tipo")),
                DataEntrada = r.GetDateTime(r.GetOrdinal("DataEntrada")),
                DataSaida = r.GetDateTime(r.GetOrdinal("DataSaida")),
                Noites = r.GetInt32(r.GetOrdinal("Noites")),
                QtdHospedes = r.GetByte(r.GetOrdinal("QtdHospedes")),
                ValorTotal = r.GetDecimal(r.GetOrdinal("ValorTotal")),
                ValorPago = r.GetDecimal(r.GetOrdinal("ValorPago")),
                Status = r.GetString(r.GetOrdinal("Status")),
                Avaliada = r.GetBoolean(r.GetOrdinal("Avaliada")),
                PodeAvaliar = r.GetBoolean(r.GetOrdinal("PodeAvaliar"))
            });
        return lista;
    }

    /// <returns>true se o cancelamento foi gratuito (7+ dias antes da entrada).</returns>
    public async Task<bool> CancelarAsync(int id, string codigo)
    {
        await using var c = _db.Site(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_CancelarReservaConta");
        cmd.Parameters.Add("@IdHospede", SqlDbType.Int).Value = id;
        cmd.Parameters.Add("@Codigo", SqlDbType.VarChar, 10).Value = codigo.Trim().ToUpperInvariant();
        var g = new SqlParameter("@Gratuito", SqlDbType.Bit) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(g);
        await cmd.ExecuteNonQueryAsync();
        return (bool)g.Value;
    }

    public async Task AvaliarAsync(int id, string codigo, int nota, string? comentario)
    {
        await using var c = _db.Site(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_AvaliarReserva");
        cmd.Parameters.Add("@IdHospede", SqlDbType.Int).Value = id;
        cmd.Parameters.Add("@Codigo", SqlDbType.VarChar, 10).Value = codigo.Trim().ToUpperInvariant();
        cmd.Parameters.Add("@Nota", SqlDbType.TinyInt).Value = (byte)nota;
        cmd.Parameters.Add("@Comentario", SqlDbType.NVarChar, 500).Value = (object?)comentario ?? "";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<AvaliacaoPublica>> ListarAvaliacoesAsync(int? idTipo, int top = 6)
    {
        var lista = new List<AvaliacaoPublica>();
        await using var c = _db.Site(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_ListarAvaliacoes");
        cmd.Parameters.Add("@IdTipoQuarto", SqlDbType.Int).Value = (object?)idTipo ?? DBNull.Value;
        cmd.Parameters.Add("@Top", SqlDbType.Int).Value = top;
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            lista.Add(new AvaliacaoPublica
            {
                Nota = r.GetByte(r.GetOrdinal("Nota")),
                Comentario = S(r, "Comentario"),
                Autor = r.GetString(r.GetOrdinal("Autor")),
                DataAval = r.GetDateTime(r.GetOrdinal("DataAval")),
                Tipo = r.GetString(r.GetOrdinal("Tipo"))
            });
        return lista;
    }

    public async Task<ResumoAvaliacoes> ResumoAvaliacoesAsync(int? idTipo)
    {
        await using var c = _db.Site(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_ResumoAvaliacoes");
        cmd.Parameters.Add("@IdTipoQuarto", SqlDbType.Int).Value = (object?)idTipo ?? DBNull.Value;
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return new ResumoAvaliacoes();
        return new ResumoAvaliacoes { Media = r["Media"] is decimal m ? m : null, Total = r.GetInt32(r.GetOrdinal("Total")) };
    }

    public async Task<TipoQuarto?> DetalheTipoAsync(int id)
    {
        await using var c = _db.Site(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_DetalheTipoQuarto");
        cmd.Parameters.Add("@IdTipoQuarto", SqlDbType.Int).Value = id;
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? TipoQuartoReader.Ler(r) : null;
    }

    public async Task<List<DiaCalendario>> CalendarioAsync(int idTipo, DateTime mes)
    {
        var lista = new List<DiaCalendario>();
        await using var c = _db.Site(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_CalendarioTipo");
        cmd.Parameters.Add("@IdTipoQuarto", SqlDbType.Int).Value = idTipo;
        cmd.Parameters.Add("@Mes", SqlDbType.Date).Value = new DateTime(mes.Year, mes.Month, 1);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            lista.Add(new DiaCalendario { Dia = r.GetDateTime(0), Livres = r.GetInt32(1) });
        return lista;
    }
}
