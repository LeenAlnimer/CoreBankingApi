using System.Net;
using BankTask.Application.Interfaces.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace BankTask.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _username;
    private readonly string _password;
    private readonly string _from;

    public EmailService(IConfiguration configuration)
    {
        _smtpHost =
            configuration["Email:SmtpHost"]
            ?? throw new InvalidOperationException(
                "SMTP host is not configured.");

        _smtpPort =
            configuration.GetValue<int?>("Email:SmtpPort")
            ?? throw new InvalidOperationException(
                "SMTP port is not configured.");

        _username =
            configuration["Email:Username"]
            ?? throw new InvalidOperationException(
                "Email username is not configured.");

        _password =
            configuration["Email:Password"]
            ?? throw new InvalidOperationException(
                "Email password is not configured.");

        _from =
            configuration["Email:From"]
            ?? throw new InvalidOperationException(
                "Email sender is not configured.");
    }

    public async Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        if (!MailboxAddress.TryParse(to, out var toAddress))
        {
            throw new FormatException(
                $"'{to}' is not a valid email address.");
        }

        var message = new MimeMessage();

        message.From.Add(
            new MailboxAddress(
                "BankTask",
                _from));

        message.To.Add(toAddress);

        message.Subject = subject;

        message.Body = new BodyBuilder
        {
            HtmlBody = htmlBody
        }.ToMessageBody();

        using var smtpClient = new SmtpClient();

        await smtpClient.ConnectAsync(
            _smtpHost,
            _smtpPort,
            SecureSocketOptions.SslOnConnect,
            cancellationToken);

        await smtpClient.AuthenticateAsync(
            _username,
            _password,
            cancellationToken);

        await smtpClient.SendAsync(
            message,
            cancellationToken);

        await smtpClient.DisconnectAsync(
            true,
            cancellationToken);
    }

    public async Task SendTemplatedEmailAsync(
        string to,
        string subject,
        string templateName,
        IReadOnlyDictionary<string, string> placeholders,
        CancellationToken cancellationToken = default)
    {
        var templatePath = Path.Combine(
            AppContext.BaseDirectory,
            "Templates",
            templateName);

        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException(
                $"Email template '{templateName}' was not found.",
                templatePath);
        }

        var htmlBody =
            await File.ReadAllTextAsync(
                templatePath,
                cancellationToken);

        foreach (var placeholder in placeholders)
        {
            htmlBody = htmlBody.Replace(
                placeholder.Key,
                WebUtility.HtmlEncode(placeholder.Value));
        }

        await SendAsync(
            to,
            subject,
            htmlBody,
            cancellationToken);
    }
}