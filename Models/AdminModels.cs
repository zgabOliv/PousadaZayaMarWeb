using System.ComponentModel.DataAnnotations;
namespace PousadaMareMansa.Web.Models;

public class ResumoPainel
{
    public DateTime Hoje { get; set; }
    public int TotalQuartos { get; set; }
    public int QuartosOcupadosHoje { get; set; }
    public int EntradasHoje { get; set; }
    public int CheckInsPendentes { get; set; }
    public int SaidasHoje { get; set; }
    public int Hospedados { get; set; }
    public int ReservasPendentes { get; set; }
    public int MensagensNovas { get; set; }
    public decimal ReceitaMes { get; set; }
    public int QuartosLivresHoje => TotalQuartos - QuartosOcupadosHoje;
}

public class QuartoMapa
{
    public string Quarto { get; set; } = "";
    public string Tipo { get; set; } = "";
    public int Capacidade { get; set; }
    public string Situacao { get; set; } = "";   // Livre, Reservado, Pendente, Hospedado, Saiu
    public string? Codigo { get; set; }
    public string? Hospede { get; set; }
    public DateTime? DataEntrada { get; set; }
    public DateTime? DataSaida { get; set; }
}

public class MovimentoItem
{
    public string Movimento { get; set; } = "";  // Entrada ou Saida
    public string Codigo { get; set; } = "";
    public string Hospede { get; set; } = "";
    public string Quarto { get; set; } = "";
    public string Tipo { get; set; } = "";
    public DateTime DataEntrada { get; set; }
    public DateTime DataSaida { get; set; }
    public string Status { get; set; } = "";
    public decimal ValorTotal { get; set; }
    public decimal ValorPago { get; set; }
    public DateTime? CheckInEm { get; set; }
    public DateTime? CheckOutEm { get; set; }
}

public class QuartoAdmin
{
    public int IdQuarto { get; set; }
    public string Numero { get; set; } = "";
    public bool Ativo { get; set; }
    public int IdTipoQuarto { get; set; }
    public string Tipo { get; set; } = "";
}

public class HospedeAdmin
{
    public int IdHospede { get; set; }
    public string Nome { get; set; } = "";
    public string Email { get; set; } = "";
    public string Telefone { get; set; } = "";
    public DateTime DataCadastro { get; set; }
    public bool TemConta { get; set; }
    public int Reservas { get; set; }
    public DateTime? UltimaEstadia { get; set; }
}

public class MensagemAdmin
{
    public int IdMensagem { get; set; }
    public string Nome { get; set; } = "";
    public string Email { get; set; } = "";
    public string Texto { get; set; } = "";
    public DateTime DataEnvio { get; set; }
    public bool Respondida { get; set; }
}

public class UsuarioLinha
{
    public int IdUsuario { get; set; }
    public string Login { get; set; } = "";
    public string Perfil { get; set; } = "";
    public bool Ativo { get; set; }
}

public class LinhaRelatorio
{
    public string Tipo { get; set; } = "";
    public int Unidades { get; set; }
    public int Reservas { get; set; }
    public int DiariasOcupadas { get; set; }
    public int DiariasDisponiveis { get; set; }
    public decimal ValorReservado { get; set; }
    public decimal TaxaOcupacao => DiariasDisponiveis == 0 ? 0 : Math.Round(100m * DiariasOcupadas / DiariasDisponiveis, 1);
}

public class ReceitaMes
{
    public int Ano { get; set; }
    public int Mes { get; set; }
    public decimal Receita { get; set; }
    public int Reservas { get; set; }
}

/// <summary>Reserva feita pela recepção (painel ou app), com pagamento opcional na hora (RF22).</summary>
public class NovaReservaAdmin
{
    [Range(1, int.MaxValue, ErrorMessage = "Escolha a suíte.")] public int IdTipoQuarto { get; set; }
    [Required(ErrorMessage = "Informe a entrada."), DataType(DataType.Date)] public DateTime? Entrada { get; set; }
    [Required(ErrorMessage = "Informe a saída."), DataType(DataType.Date)] public DateTime? Saida { get; set; }
    [Range(1, 8, ErrorMessage = "Informe de 1 a 8 hóspedes.")] public int Hospedes { get; set; } = 2;
    [Required(ErrorMessage = "Informe o nome."), StringLength(120, MinimumLength = 3, ErrorMessage = "O nome deve ter ao menos 3 letras.")] public string Nome { get; set; } = "";
    [Required(ErrorMessage = "Informe o e-mail."), EmailAddress(ErrorMessage = "E-mail inválido."), StringLength(160)] public string Email { get; set; } = "";
    [Required(ErrorMessage = "Informe o telefone."), StringLength(20)] public string Telefone { get; set; } = "";
    [Range(0, 100000, ErrorMessage = "Valor inválido.")] public decimal? ValorPago { get; set; }
    [RegularExpression("^(Pix|Cartao|Dinheiro)?$", ErrorMessage = "Forma deve ser Pix, Cartao ou Dinheiro.")] public string? Forma { get; set; }
}

public class TipoEdicao
{
    public int? IdTipoQuarto { get; set; }
    [Required(ErrorMessage = "Informe o nome."), StringLength(60)] public string Nome { get; set; } = "";
    [Range(1, 8, ErrorMessage = "Capacidade de 1 a 8.")] public byte Capacidade { get; set; } = 2;
    [Range(0.01, 100000, ErrorMessage = "Informe a diária.")] public decimal PrecoDiaria { get; set; }
    public bool Acessivel { get; set; }
    [StringLength(300)] public string? Descricao { get; set; }
    [StringLength(200)] public string? Imagem { get; set; }
    [StringLength(500)] public string? Comodidades { get; set; }
}

public class QuartoEdicao
{
    public int? IdQuarto { get; set; }
    [Required(ErrorMessage = "Informe o número."), StringLength(10)] public string Numero { get; set; } = "";
    [Range(1, int.MaxValue, ErrorMessage = "Escolha o tipo.")] public int IdTipoQuarto { get; set; }
    public bool Ativo { get; set; } = true;
}

public class NovoUsuario
{
    [Required(ErrorMessage = "Informe o login."), StringLength(40, MinimumLength = 3, ErrorMessage = "O login deve ter de 3 a 40 caracteres."),
     RegularExpression("^[A-Za-z0-9._-]+$", ErrorMessage = "Use só letras, números, ponto, hífen e sublinhado.")] public string Login { get; set; } = "";
    [Required(ErrorMessage = "Informe a senha."), StringLength(72, MinimumLength = 8, ErrorMessage = "A senha deve ter de 8 a 72 caracteres."), DataType(DataType.Password)] public string Senha { get; set; } = "";
    [Required, RegularExpression("^(Gerente|Recepcao)$", ErrorMessage = "Perfil inválido.")] public string Perfil { get; set; } = "Recepcao";
}
