using BankTask.Application.Interfaces.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace BankTask.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendAsync(
        string to,
        string subject,
        string htmlBody)
    {
        var smtpHost =
            _configuration["Email:SmtpHost"]
            ?? throw new InvalidOperationException(
                "SMTP host is not configured.");

        var smtpPort =
            _configuration.GetValue<int?>("Email:SmtpPort")
            ?? throw new InvalidOperationException(
                "SMTP port is not configured.");

        var username =
            _configuration["Email:Username"]
            ?? throw new InvalidOperationException(
                "Email username is not configured.");

        var password =
            _configuration["Email:Password"]
            ?? throw new InvalidOperationException(
                "Email password is not configured.");

        var from =
            _configuration["Email:From"]
            ?? throw new InvalidOperationException(
                "Email sender is not configured.");

        var message = new MimeMessage();

        message.From.Add(
            new MailboxAddress(
                "BankTask",
                from));

        message.To.Add(
            MailboxAddress.Parse(to));

        message.Subject = subject;

        message.Body = new BodyBuilder
        {
            HtmlBody = htmlBody
        }.ToMessageBody();

        using var smtpClient = new SmtpClient();

        await smtpClient.ConnectAsync(
            smtpHost,
            smtpPort,
            SecureSocketOptions.SslOnConnect);

        await smtpClient.AuthenticateAsync(
            username,
            password);

        await smtpClient.SendAsync(message);

        await smtpClient.DisconnectAsync(true);
    }

    public async Task SendWelcomeEmailAsync(
        string to,
        string userName)
    {
        var templatePath = Path.Combine(
            AppContext.BaseDirectory,
            "Templates",
            "WelcomeEmail.html");

        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException(
                "Welcome email template was not found.",
                templatePath);
        }

        var htmlBody =
            await File.ReadAllTextAsync(templatePath);

        htmlBody = htmlBody.Replace(
            "{{UserName}}",
            userName);

        await SendAsync(
            to,
            "Welcome to BankTask",
            htmlBody);
    }
}