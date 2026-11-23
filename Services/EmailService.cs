using System.Net;
using System.Net.Mail;
using PousadaMareMansa.Web.Data;
namespace PousadaMareMansa.Web.Services;

public interface IEmailSender
{
    /// <summary>Sempre registra na tabela EmailSaida; envia por SMTP só se Smtp:Host estiver configurado.</summary>
    Task EnviarAsync(string para, string assunto, string corpo);
}

public class EmailService : IEmailSender
{
    private readonly ContaRepository _repo;
    private readonly IConfiguration _cfg;
    private readonly ILogger<EmailService> _log;
    public EmailService(ContaRepository repo, IConfiguration cfg, ILogger<EmailService> log) { _repo = repo; _cfg = cfg; _log = log; }

    public async Task EnviarAsync(string para, string assunto, string corpo)
    {
        var id = await _repo.RegistrarEmailAsync(para, assunto, corpo);
        var host = _cfg["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host)) return;
        try
        {
            var de = _cfg["Smtp:From"] ?? _cfg["Smtp:User"] ?? "nao-responda@zayamar.example";
            using var smtp = new SmtpClient(host, _cfg.GetValue("Smtp:Port", 587)) { EnableSsl = _cfg.GetValue("Smtp:Ssl", true) };
            if (!string.IsNullOrEmpty(_cfg["Smtp:User"]))
                smtp.Credentials = new NetworkCredential(_cfg["Smtp:User"], _cfg["Smtp:Password"]);
            using var msg = new MailMessage(new MailAddress(de, "Zaya Mar Pousada"), new MailAddress(para)) { Subject = assunto, Body = corpo };
            await smtp.SendMailAsync(msg);
            await _repo.MarcarEmailEnviadoAsync(id);
        }
        catch (Exception ex) { _log.LogWarning(ex, "Falha ao enviar e-mail para {Para}", para); }
    }
}
