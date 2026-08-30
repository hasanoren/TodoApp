using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Configuration;
using TodoApp.Application.Interfaces;

namespace TodoApp.Infrastructure.Services;

public class EmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;

    public EmailSender(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendEmailAsync(
        string toEmail,
        string subject,
        string body)
    {
        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(
            _configuration["Smtp:FromName"],
            _configuration["Smtp:FromEmail"]));

        message.To.Add(new MailboxAddress("", toEmail));

        message.Subject = subject;

        // Email içeriği
        message.Body = new TextPart("html")
        {
            Text = body
        };

        using var client = new SmtpClient();

        await client.ConnectAsync(
            _configuration["Smtp:Host"],
            int.Parse(_configuration["Smtp:Port"]!),
            MailKit.Security.SecureSocketOptions.StartTls);

        await client.AuthenticateAsync(
            _configuration["Smtp:Username"],
            _configuration["Smtp:Password"]);

        await client.SendAsync(message);

        await client.DisconnectAsync(true);
    }
}