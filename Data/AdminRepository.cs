using System.Data;
using Microsoft.Data.SqlClient;
using PousadaMareMansa.Web.Models;
namespace PousadaMareMansa.Web.Data;

/// <summary>Acesso a dados da área administrativa (conexão com papel rl_gerente).</summary>
public class AdminRepository
{
    private readonly Db _db;
    public AdminRepository(Db db) => _db = db;

    public async Task<List<ReservaAdmin>> ListarReservasAsync()
    {
        var lista = new List<ReservaAdmin>();
        await using var c = _db.Admin(); await c.OpenAsync();
        await using var cmd = new SqlCommand(
            "SELECT Codigo, Hospede, Quarto, Tipo, DataEntrada, DataSaida, ValorTotal, ValorPago, Status, CheckInEm, CheckOutEm " +
            "FROM vw_ReservasDetalhadas ORDER BY DataEntrada DESC", c);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            lista.Add(new ReservaAdmin
            {
                Codigo = r.GetString(0), Hospede = r.GetString(1), Quarto = r.GetString(2), Tipo = r.GetString(3),
                DataEntrada = r.GetDateTime(4), DataSaida = r.GetDateTime(5),
                ValorTotal = r.GetDecimal(6), ValorPago = r.GetDecimal(7), Status = r.GetString(8),
                CheckInEm = r[9] as DateTime?, CheckOutEm = r[10] as DateTime?
            });
        return lista;
    }

    public async Task PagarAsync(string codigo, decimal valor, string forma)
    {
        await using var c = _db.Admin(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_RegistrarPagamento");
        cmd.Parameters.Add("@Codigo", SqlDbType.VarChar, 10).Value = codigo;
        var v = cmd.Parameters.Add("@Valor", SqlDbType.Decimal);
        v.Precision = 10; v.Scale = 2; v.Value = valor;
        cmd.Parameters.Add("@Forma", SqlDbType.VarChar, 10).Value = forma;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task CancelarAsync(string codigo)
    {
        await using var c = _db.Admin(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_CancelarReserva");
        cmd.Parameters.Add("@Codigo", SqlDbType.VarChar, 10).Value = codigo;
        cmd.Parameters.Add(new SqlParameter("@Gratuito", SqlDbType.Bit) { Direction = ParameterDirection.Output });
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task CriarAdminAsync(string login, byte[] hash, byte[] salt, string perfil)
    {
        await using var c = _db.Admin(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_CriarAdmin");
        cmd.Parameters.Add("@Login", SqlDbType.VarChar, 40).Value = login;
        cmd.Parameters.Add("@Hash", SqlDbType.VarBinary, 64).Value = hash;
        cmd.Parameters.Add("@Salt", SqlDbType.VarBinary, 32).Value = salt;
        cmd.Parameters.Add("@Perfil", SqlDbType.VarChar, 15).Value = perfil;
        await cmd.ExecuteNonQueryAsync();
    }

    // ---------- helpers ----------
    private static string? Str(SqlDataReader r, string c) => r[c] as string;
    private static DateTime? Dt(SqlDataReader r, string c) => r[c] as DateTime?;
    private static void Dec(SqlCommand cmd, string nome, decimal? v)
    {
        var p = cmd.Parameters.Add(nome, SqlDbType.Decimal); p.Precision = 10; p.Scale = 2;
        p.Value = v.HasValue ? v.Value : DBNull.Value;
    }

    // ---------- painel, mapa, movimento ----------
    public async Task<ResumoPainel> ResumoAsync()
    {
        await using var c = _db.Admin(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_PainelResumo");
        await using var r = await cmd.ExecuteReaderAsync();
        await r.ReadAsync();
        return new ResumoPainel
        {
            Hoje = r.GetDateTime(r.GetOrdinal("Hoje")), TotalQuartos = r.GetInt32(r.GetOrdinal("TotalQuartos")),
            QuartosOcupadosHoje = r.GetInt32(r.GetOrdinal("QuartosOcupadosHoje")), EntradasHoje = r.GetInt32(r.GetOrdinal("EntradasHoje")),
            CheckInsPendentes = r.GetInt32(r.GetOrdinal("CheckInsPendentes")), SaidasHoje = r.GetInt32(r.GetOrdinal("SaidasHoje")),
            Hospedados = r.GetInt32(r.GetOrdinal("Hospedados")), ReservasPendentes = r.GetInt32(r.GetOrdinal("ReservasPendentes")),
            MensagensNovas = r.GetInt32(r.GetOrdinal("MensagensNovas")), ReceitaMes = r.GetDecimal(r.GetOrdinal("ReceitaMes"))
        };
    }

    public async Task<List<QuartoMapa>> MapaAsync(DateTime data)
    {
        var l = new List<QuartoMapa>();
        await using var c = _db.Admin(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_MapaOcupacao");
        cmd.Parameters.Add("@Data", SqlDbType.Date).Value = data.Date;
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            l.Add(new QuartoMapa
            {
                Quarto = r.GetString(r.GetOrdinal("Quarto")), Tipo = r.GetString(r.GetOrdinal("Tipo")),
                Capacidade = r.GetByte(r.GetOrdinal("Capacidade")), Situacao = r.GetString(r.GetOrdinal("Situacao")),
                Codigo = Str(r, "Codigo"), Hospede = Str(r, "Hospede"),
                DataEntrada = Dt(r, "DataEntrada"), DataSaida = Dt(r, "DataSaida")
            });
        return l;
    }

    public async Task<List<MovimentoItem>> MovimentoAsync(DateTime data)
    {
        var l = new List<MovimentoItem>();
        await using var c = _db.Admin(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_MovimentoDia");
        cmd.Parameters.Add("@Data", SqlDbType.Date).Value = data.Date;
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            l.Add(new MovimentoItem
            {
                Movimento = r.GetString(0), Codigo = r.GetString(1), Hospede = r.GetString(2), Quarto = r.GetString(3),
                Tipo = r.GetString(4), DataEntrada = r.GetDateTime(5), DataSaida = r.GetDateTime(6), Status = r.GetString(7),
                ValorTotal = r.GetDecimal(8), ValorPago = r.GetDecimal(9), CheckInEm = Dt(r, "CheckInEm"), CheckOutEm = Dt(r, "CheckOutEm")
            });
        return l;
    }

    // ---------- reservas ----------
    public async Task<string> CriarReservaAsync(NovaReservaAdmin m)
    {
        await using var c = _db.Admin(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_AdminCriarReserva");
        cmd.Parameters.Add("@Nome", SqlDbType.NVarChar, 120).Value = m.Nome.Trim();
        cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 160).Value = m.Email.Trim().ToLowerInvariant();
        cmd.Parameters.Add("@Telefone", SqlDbType.VarChar, 20).Value = m.Telefone.Trim();
        cmd.Parameters.Add("@IdTipoQuarto", SqlDbType.Int).Value = m.IdTipoQuarto;
        cmd.Parameters.Add("@Entrada", SqlDbType.Date).Value = m.Entrada!.Value.Date;
        cmd.Parameters.Add("@Saida", SqlDbType.Date).Value = m.Saida!.Value.Date;
        cmd.Parameters.Add("@Hospedes", SqlDbType.TinyInt).Value = (byte)m.Hospedes;
        Dec(cmd, "@ValorPago", m.ValorPago);
        cmd.Parameters.Add("@Forma", SqlDbType.VarChar, 10).Value = string.IsNullOrEmpty(m.Forma) ? DBNull.Value : m.Forma;
        var cod = new SqlParameter("@Codigo", SqlDbType.VarChar, 10) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(cod);
        await cmd.ExecuteNonQueryAsync();
        return (string)cod.Value;
    }

    private async Task ExecCodigoAsync(string proc, string codigo)
    {
        await using var c = _db.Admin(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, proc);
        cmd.Parameters.Add("@Codigo", SqlDbType.VarChar, 10).Value = codigo.Trim().ToUpperInvariant();
        await cmd.ExecuteNonQueryAsync();
    }
    public Task CheckInAsync(string codigo) => ExecCodigoAsync("sp_CheckIn", codigo);
    public Task CheckOutAsync(string codigo) => ExecCodigoAsync("sp_CheckOut", codigo);

    // ---------- suítes e quartos ----------
    public async Task<List<QuartoAdmin>> ListarQuartosAsync()
    {
        var l = new List<QuartoAdmin>();
        await using var c = _db.Admin(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_AdminListarQuartos");
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            l.Add(new QuartoAdmin { IdQuarto = r.GetInt32(0), Numero = r.GetString(1), Ativo = r.GetBoolean(2), IdTipoQuarto = r.GetInt32(3), Tipo = r.GetString(4) });
        return l;
    }

    public async Task<List<TipoQuarto>> ListarTiposAsync()
    {
        var l = new List<TipoQuarto>();
        await using var c = _db.Admin(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_ListarTiposQuarto");
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) l.Add(TipoQuartoReader.Ler(r));
        return l;
    }

    public async Task SalvarTipoAsync(TipoEdicao m)
    {
        await using var c = _db.Admin(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_AdminSalvarTipo");
        cmd.Parameters.Add("@IdTipoQuarto", SqlDbType.Int).Value = (object?)m.IdTipoQuarto ?? DBNull.Value;
        cmd.Parameters.Add("@Nome", SqlDbType.NVarChar, 60).Value = m.Nome.Trim();
        cmd.Parameters.Add("@Capacidade", SqlDbType.TinyInt).Value = m.Capacidade;
        Dec(cmd, "@PrecoDiaria", m.PrecoDiaria);
        cmd.Parameters.Add("@Acessivel", SqlDbType.Bit).Value = m.Acessivel;
        cmd.Parameters.Add("@Descricao", SqlDbType.NVarChar, 300).Value = (object?)m.Descricao ?? DBNull.Value;
        cmd.Parameters.Add("@Imagem", SqlDbType.NVarChar, 200).Value = (object?)m.Imagem ?? DBNull.Value;
        cmd.Parameters.Add("@Comodidades", SqlDbType.NVarChar, 500).Value = (object?)m.Comodidades ?? DBNull.Value;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SalvarQuartoAsync(QuartoEdicao m)
    {
        await using var c = _db.Admin(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_AdminSalvarQuarto");
        cmd.Parameters.Add("@IdQuarto", SqlDbType.Int).Value = (object?)m.IdQuarto ?? DBNull.Value;
        cmd.Parameters.Add("@Numero", SqlDbType.VarChar, 10).Value = m.Numero.Trim();
        cmd.Parameters.Add("@IdTipoQuarto", SqlDbType.Int).Value = m.IdTipoQuarto;
        cmd.Parameters.Add("@Ativo", SqlDbType.Bit).Value = m.Ativo;
        await cmd.ExecuteNonQueryAsync();
    }

    // ---------- hóspedes e mensagens ----------
    public async Task<List<HospedeAdmin>> ListarHospedesAsync()
    {
        var l = new List<HospedeAdmin>();
        await using var c = _db.Admin(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_AdminListarHospedes");
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            l.Add(new HospedeAdmin
            {
                IdHospede = r.GetInt32(0), Nome = r.GetString(1), Email = r.GetString(2), Telefone = r.GetString(3),
                DataCadastro = r.GetDateTime(4), TemConta = r.GetBoolean(5), Reservas = r.GetInt32(6), UltimaEstadia = Dt(r, "UltimaEstadia")
            });
        return l;
    }

    public async Task<List<MensagemAdmin>> ListarMensagensAsync()
    {
        var l = new List<MensagemAdmin>();
        await using var c = _db.Admin(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_AdminListarMensagens");
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            l.Add(new MensagemAdmin { IdMensagem = r.GetInt32(0), Nome = r.GetString(1), Email = r.GetString(2), Texto = r.GetString(3), DataEnvio = r.GetDateTime(4), Respondida = r.GetBoolean(5) });
        return l;
    }

    public async Task MarcarMensagemAsync(int id, bool respondida)
    {
        await using var c = _db.Admin(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_AdminMarcarMensagem");
        cmd.Parameters.Add("@IdMensagem", SqlDbType.Int).Value = id;
        cmd.Parameters.Add("@Respondida", SqlDbType.Bit).Value = respondida;
        await cmd.ExecuteNonQueryAsync();
    }

    // ---------- usuários ----------
    public async Task<List<UsuarioLinha>> ListarUsuariosAsync()
    {
        var l = new List<UsuarioLinha>();
        await using var c = _db.Admin(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_AdminListarUsuarios");
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            l.Add(new UsuarioLinha { IdUsuario = r.GetInt32(0), Login = r.GetString(1), Perfil = r.GetString(2), Ativo = r.GetBoolean(3) });
        return l;
    }

    public async Task AlterarUsuarioAsync(int id, string perfil, bool ativo)
    {
        await using var c = _db.Admin(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_AdminAlterarUsuario");
        cmd.Parameters.Add("@IdUsuario", SqlDbType.Int).Value = id;
        cmd.Parameters.Add("@Perfil", SqlDbType.VarChar, 15).Value = perfil;
        cmd.Parameters.Add("@Ativo", SqlDbType.Bit).Value = ativo;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RedefinirSenhaUsuarioAsync(int id, byte[] hash, byte[] salt)
    {
        await using var c = _db.Admin(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_AdminRedefinirSenhaUsuario");
        cmd.Parameters.Add("@IdUsuario", SqlDbType.Int).Value = id;
        cmd.Parameters.Add("@Hash", SqlDbType.VarBinary, 64).Value = hash;
        cmd.Parameters.Add("@Salt", SqlDbType.VarBinary, 32).Value = salt;
        await cmd.ExecuteNonQueryAsync();
    }

    // ---------- relatórios ----------
    public async Task<List<LinhaRelatorio>> RelatorioAsync(DateTime inicio, DateTime fim)
    {
        var l = new List<LinhaRelatorio>();
        await using var c = _db.Admin(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_RelatorioPeriodo");
        cmd.Parameters.Add("@Inicio", SqlDbType.Date).Value = inicio.Date;
        cmd.Parameters.Add("@Fim", SqlDbType.Date).Value = fim.Date;
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            l.Add(new LinhaRelatorio
            {
                Tipo = r.GetString(0), Unidades = r.GetInt32(1), Reservas = r.GetInt32(2), DiariasOcupadas = r.GetInt32(3),
                DiariasDisponiveis = r.GetInt32(4), ValorReservado = r.GetDecimal(5)
            });
        return l;
    }

    public async Task<List<ReceitaMes>> ReceitaMensalAsync()
    {
        var l = new List<ReceitaMes>();
        await using var c = _db.Admin(); await c.OpenAsync();
        await using var cmd = Db.Proc(c, "sp_ReceitaMensal");
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            l.Add(new ReceitaMes { Ano = r.GetInt32(0), Mes = r.GetInt32(1), Receita = r.GetDecimal(2), Reservas = r.GetInt32(3) });
        return l;
    }
}
