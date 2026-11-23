using PousadaMareMansa.Web.Data;
using PousadaMareMansa.Web.Models;
namespace PousadaMareMansa.Web.Services;

/// <summary>Fluxos de conta: cadastro, login com bloqueio, links de confirmação e de redefinição de senha.</summary>
public class ContaService
{
    private readonly ContaRepository _repo;
    private readonly IEmailSender _email;
    private readonly IConfiguration _cfg;
    public ContaService(ContaRepository repo, IEmailSender email, IConfiguration cfg) { _repo = repo; _email = email; _cfg = cfg; }

    /// <summary>Se Email:MostrarLinks=true (só para demonstração), os links aparecem na tela além de irem por e-mail.</summary>
    public bool MostrarLinks => _cfg.GetValue<bool>("Email:MostrarLinks");

    public async Task<string> EnviarConfirmacaoAsync(int id, string nome, string email, string baseUrl)
    {
        var (token, hash) = TokenService.Novo();
        await _repo.CriarTokenAsync(id, "confirmar", hash, 60 * 24);
        var link = $"{baseUrl}/Conta/Confirmar?token={token}";
        await _email.EnviarAsync(email, "Confirme seu e-mail - Zaya Mar Pousada",
            $"Olá, {nome}!\n\nConfirme seu e-mail para ver suas reservas e avaliar suas estadias:\n{link}\n\nO link vale por 24 horas.\n\nZaya Mar Pousada (projeto acadêmico, dados fictícios)");
        return link;
    }

    /// <returns>link gerado, ou null se o e-mail não tem conta (a resposta ao usuário é igual nos dois casos).</returns>
    public async Task<string?> EnviarRedefinicaoAsync(string email, string baseUrl)
    {
        var conta = await _repo.ObterContaAsync(email);
        if (conta is null) return null;
        var (token, hash) = TokenService.Novo();
        await _repo.CriarTokenAsync(conta.IdHospede, "redefinir", hash, 60);
        var link = $"{baseUrl}/Conta/Redefinir?token={token}";
        await _email.EnviarAsync(conta.Email, "Redefinir senha - Zaya Mar Pousada",
            $"Olá, {conta.Nome}!\n\nPara criar uma nova senha, abra:\n{link}\n\nO link vale por 1 hora. Se não foi você, ignore esta mensagem.\n\nZaya Mar Pousada");
        return link;
    }

    public async Task EnviarReservaAsync(string para, string nome, string codigo)
    {
        await _email.EnviarAsync(para, $"Reserva {codigo} recebida - Zaya Mar Pousada",
            $"Olá, {nome}!\n\nRecebemos sua reserva {codigo}. Ela será confirmada quando o pagamento for registrado.\nAcompanhe em Minhas reservas.\n\nZaya Mar Pousada");
    }

    /// <summary>Valida e-mail/senha. Mensagem única para não revelar se o e-mail existe.</summary>
    public async Task<(ContaDados? Conta, string? Erro)> AutenticarAsync(string email, string senha)
    {
        const string Generico = "E-mail ou senha incorretos.";
        var conta = await _repo.ObterContaAsync(email);
        if (conta is null) { PasswordHasher.Verificar(senha, new byte[32], new byte[32]); return (null, Generico); }
        if (conta.BloqueadoAte is { } ate && ate > DateTime.UtcNow)
            return (null, "Muitas tentativas erradas. Tente de novo em alguns minutos ou redefina a senha.");
        var ok = PasswordHasher.Verificar(senha, conta.SenhaHash, conta.Salt);
        await _repo.RegistrarLoginAsync(conta.IdHospede, ok);
        return ok ? (conta, null) : (null, Generico);
    }
}
