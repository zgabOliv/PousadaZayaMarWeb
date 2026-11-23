using Microsoft.Data.SqlClient;
using PousadaMareMansa.Web.Models;
namespace PousadaMareMansa.Web.Data;

public static class TipoQuartoReader
{
    public static TipoQuarto Ler(SqlDataReader r) => new()
    {
        IdTipoQuarto = r.GetInt32(r.GetOrdinal("IdTipoQuarto")),
        Nome = r.GetString(r.GetOrdinal("Nome")),
        Capacidade = r.GetByte(r.GetOrdinal("Capacidade")),
        PrecoDiaria = r.GetDecimal(r.GetOrdinal("PrecoDiaria")),
        Acessivel = r.GetBoolean(r.GetOrdinal("Acessivel")),
        Descricao = r["Descricao"] as string,
        Comodidades = r["Comodidades"] as string,
        Imagem = r["Imagem"] as string,
        Unidades = r.GetInt32(r.GetOrdinal("Unidades")),
        Media = r["Media"] is decimal m ? m : null,
        TotalAvaliacoes = r.GetInt32(r.GetOrdinal("TotalAvaliacoes"))
    };
}
