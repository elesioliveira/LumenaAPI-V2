
using MailKit.Net.Smtp;
using MimeKit;
using MailKit.Security;

public interface IEmailService
{
    Task EnviarEmailAsync(string email, string assunto, string corpo);
    Task EnviarEmailParaListaAsync(List<string> emails, string assunto, string corpo); // Novo método
}
public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // Método original para um único email
    public async Task EnviarEmailAsync(string email, string assunto, string corpo)
    {
        await EnviarEmailParaListaAsync(new List<string> { email }, assunto, corpo);
    }

    // NOVO MÉTODO para múltiplos emails
    public async Task EnviarEmailParaListaAsync(List<string> emails, string assunto, string corpo)
    {
        if (emails == null || !emails.Any())
            return;

        var smtpHost = _configuration["EmailSettings:SmtpHost"];
        var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"]!);
        var smtpUser = _configuration["EmailSettings:SmtpUser"];
        var smtpPass = _configuration["EmailSettings:SmtpPass"];
        var fromEmail = _configuration["EmailSettings:FromEmail"];
        var fromName = _configuration["EmailSettings:FromName"];

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));

        // Adiciona TODOS os emails destinatários
        foreach (var email in emails)
        {
            if (!string.IsNullOrEmpty(email))
                message.To.Add(new MailboxAddress("", email.Trim()));
        }

        message.Subject = assunto;
        message.Body = new TextPart("html") { Text = corpo };

        using (var client = new SmtpClient())
        {
            await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(smtpUser, smtpPass);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}