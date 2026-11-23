using System.ComponentModel.DataAnnotations;
namespace PousadaMareMansa.Web.Models;

public class CadastroModel
{
    [Required(ErrorMessage = "Informe seu nome."), StringLength(120, MinimumLength = 3, ErrorMessage = "O nome deve ter ao menos 3 letras.")] public string Nome { get; set; } = "";
    [Required(ErrorMessage = "Informe seu e-mail."), EmailAddress(ErrorMessage = "E-mail inválido."), StringLength(160)] public string Email { get; set; } = "";
    [Required(ErrorMessage = "Informe seu WhatsApp."), StringLength(20), Display(Name = "WhatsApp")] public string Telefone { get; set; } = "";
    [Required(ErrorMessage = "Crie uma senha."), StringLength(72, MinimumLength = 8, ErrorMessage = "A senha deve ter de 8 a 72 caracteres."), DataType(DataType.Password)] public string Senha { get; set; } = "";
    [Required(ErrorMessage = "Repita a senha."), Compare(nameof(Senha), ErrorMessage = "As senhas não conferem."), DataType(DataType.Password), Display(Name = "Repita a senha")] public string Senha2 { get; set; } = "";
}

public class EntrarModel
{
    [Required(ErrorMessage = "Informe seu e-mail."), EmailAddress(ErrorMessage = "E-mail inválido.")] public string Email { get; set; } = "";
    [Required(ErrorMessage = "Informe a senha."), DataType(DataType.Password)] public string Senha { get; set; } = "";
    public string? ReturnUrl { get; set; }
}

public class EsqueciModel
{
    [Required(ErrorMessage = "Informe seu e-mail."), EmailAddress(ErrorMessage = "E-mail inválido.")] public string Email { get; set; } = "";
}

public class RedefinirModel
{
    [Required] public string Token { get; set; } = "";
    [Required(ErrorMessage = "Crie uma senha."), StringLength(72, MinimumLength = 8, ErrorMessage = "A senha deve ter de 8 a 72 caracteres."), DataType(DataType.Password)] public string Senha { get; set; } = "";
    [Required(ErrorMessage = "Repita a senha."), Compare(nameof(Senha), ErrorMessage = "As senhas não conferem."), DataType(DataType.Password), Display(Name = "Repita a senha")] public string Senha2 { get; set; } = "";
}

public class AvaliarModel
{
    [Required] public string Codigo { get; set; } = "";
    [Range(1, 5, ErrorMessage = "Escolha uma nota de 1 a 5.")] public int Nota { get; set; }
    [StringLength(500, ErrorMessage = "O comentário pode ter até 500 caracteres.")] public string? Comentario { get; set; }
}

public class ContaDados
{
    public int IdHospede { get; set; }
    public string Nome { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Telefone { get; set; }
    public byte[] SenhaHash { get; set; } = Array.Empty<byte>();
    public byte[] Salt { get; set; } = Array.Empty<byte>();
    public bool EmailConfirmado { get; set; }
    public DateTime? BloqueadoAte { get; set; }
}

public class PerfilHospede
{
    public int IdHospede { get; set; }
    public string Nome { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Telefone { get; set; }
    public bool EmailConfirmado { get; set; }
}

public class MinhaReserva
{
    public string Codigo { get; set; } = "";
    public string Tipo { get; set; } = "";
    public DateTime DataEntrada { get; set; }
    public DateTime DataSaida { get; set; }
    public int Noites { get; set; }
    public int QtdHospedes { get; set; }
    public decimal ValorTotal { get; set; }
    public decimal ValorPago { get; set; }
    public string Status { get; set; } = "";
    public bool Avaliada { get; set; }
    public bool PodeAvaliar { get; set; }
    public bool Cancelavel => Status is "Pendente" or "Confirmada";
}

public class AvaliacaoPublica
{
    public int Nota { get; set; }
    public string? Comentario { get; set; }
    public string Autor { get; set; } = "";
    public DateTime DataAval { get; set; }
    public string Tipo { get; set; } = "";
}

public class ResumoAvaliacoes
{
    public decimal? Media { get; set; }
    public int Total { get; set; }
}

public class DiaCalendario
{
    public DateTime Dia { get; set; }
    public int Livres { get; set; }
}

public class DetalheSuiteViewModel
{
    public TipoQuarto Tipo { get; set; } = new();
    public DateTime Mes { get; set; }
    public List<DiaCalendario> Dias { get; set; } = new();
    public List<AvaliacaoPublica> Avaliacoes { get; set; } = new();
}

public class HomeViewModel
{
    public List<TipoQuarto> Tipos { get; set; } = new();
    public List<AvaliacaoPublica> Avaliacoes { get; set; } = new();
    public ResumoAvaliacoes Resumo { get; set; } = new();
}

// ---- DTOs da API do hóspede ----
public class CadastroRequest
{
    [Required, StringLength(120, MinimumLength = 3)] public string Nome { get; set; } = "";
    [Required, EmailAddress, StringLength(160)] public string Email { get; set; } = "";
    [Required, StringLength(20)] public string Telefone { get; set; } = "";
    [Required, StringLength(72, MinimumLength = 8)] public string Senha { get; set; } = "";
}
public class EntrarRequest
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Senha { get; set; } = "";
}
public class EmailRequest { [Required, EmailAddress] public string Email { get; set; } = ""; }
public class AvaliacaoRequest
{
    [Range(1, 5)] public int Nota { get; set; }
    [StringLength(500)] public string? Comentario { get; set; }
}
public class HospedeLoginResponse
{
    public string Token { get; set; } = "";
    public DateTime ExpiraEm { get; set; }
    public PerfilHospede Perfil { get; set; } = new();
}
