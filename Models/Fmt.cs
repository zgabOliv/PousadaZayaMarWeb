using System.Globalization;
namespace PousadaMareMansa.Web.Models;

/// <summary>Formatação independente da cultura do servidor (Azure costuma rodar em cultura invariável).</summary>
public static class Fmt
{
    public static string Brl(decimal v)
    {
        var s = Math.Abs(v).ToString("N2", CultureInfo.InvariantCulture);          // 1,234.50
        s = s.Replace(',', '#').Replace('.', ',').Replace('#', '.');                // 1.234,50
        return (v < 0 ? "-R$ " : "R$ ") + s;
    }
    public static string Data(DateTime d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    public static string Estrelas(decimal nota)
    {
        var n = (int)Math.Round(nota, MidpointRounding.AwayFromZero);
        n = Math.Clamp(n, 0, 5);
        return new string('★', n) + new string('☆', 5 - n);
    }
    private static readonly string[] Meses = { "janeiro","fevereiro","março","abril","maio","junho","julho","agosto","setembro","outubro","novembro","dezembro" };
    public static string Mes(DateTime d) => $"{Meses[d.Month - 1]} de {d.Year}";

    /// <summary>Lê "145,56", "145.56", "1.234,56" e "R$ 300,00" sem depender da cultura do servidor.</summary>
    public static bool TentarDecimal(string? texto, out decimal valor)
    {
        valor = 0;
        if (string.IsNullOrWhiteSpace(texto)) return false;
        var t = texto.Replace("R$", "").Trim();
        var v = t.LastIndexOf(','); var p = t.LastIndexOf('.');
        if (v >= 0 && p >= 0) t = v > p ? t.Replace(".", "").Replace(',', '.') : t.Replace(",", "");
        else if (v >= 0) t = t.Replace(',', '.');
        return decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out valor);
    }
}
