using System.ComponentModel.DataAnnotations;
namespace PousadaMareMansa.Web.Models;

public class LoginRequest
{
    [Required] public string Login { get; set; } = "";
    [Required] public string Senha { get; set; } = "";
}

public class LoginResponse
{
    public string Token { get; set; } = "";
    public DateTime ExpiraEm { get; set; }
    public string Perfil { get; set; } = "";
}

/// <summary>Identifica a reserva do hóspede: código + e-mail usado na reserva.</summary>
public class ReservaLookup
{
    [Required] public string Codigo { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
}

public class PagamentoRequest
{
    [Range(0.01, 100000)] public decimal Valor { get; set; }
    [Required, RegularExpression("^(Pix|Cartao|Dinheiro)$", ErrorMessage = "Forma deve ser Pix, Cartao ou Dinheiro.")]
    public string Forma { get; set; } = "";
}
