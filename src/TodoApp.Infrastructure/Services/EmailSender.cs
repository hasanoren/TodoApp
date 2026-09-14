using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Options;
using TodoApp.Application.Interfaces;
using TodoApp.Application.Settings;

namespace TodoApp.Infrastructure.Services;

public class EmailSender : IEmailSender
{
    private readonly SmtpSettings _smtpSettings;

    public EmailSender(IOptions<SmtpSettings> smtpOptions)
    {
        _smtpSettings = smtpOptions.Value;
    }

    public async Task SendEmailAsync(
        string toEmail,
        string subject,
        string body)
    {
        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(
            _smtpSettings.FromName,
            _smtpSettings.FromEmail));

        message.To.Add(new MailboxAddress("", toEmail));

        message.Subject = subject;

        // Email içeriği
        message.Body = new TextPart("html")
        {
            Text = body
        };

        using var client = new SmtpClient();

        await client.ConnectAsync(
            _smtpSettings.Host,
            _smtpSettings.Port,
            MailKit.Security.SecureSocketOptions.StartTls);

        await client.AuthenticateAsync(
            _smtpSettings.Username,
            _smtpSettings.Password);

        await client.SendAsync(message);

        await client.DisconnectAsync(true);
    }
}