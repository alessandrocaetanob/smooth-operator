using System;
using System.Threading.Tasks;
using SmoothOperator.Infrastructure.Data;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace SmoothOperator.Infrastructure.Services
{
    public interface IEmailService
    {
        Task<bool> IsConfiguredAsync();
        Task<EmailSendResult> SendInviteAsync(string toEmail, string toName, string inviteUrl);
        Task<EmailSendResult> SendPasswordResetAsync(string toEmail, string toName, string resetUrl);
        Task<EmailSendResult> SendTestAsync(string toEmail);
    }

    public record EmailSendResult(bool Success, string? Error);

    public class EmailService : IEmailService
    {
        private readonly AppDbContext _context;
        private readonly IEncryptionService _encryption;
        private readonly ILogger<EmailService> _logger;

        public EmailService(AppDbContext context, IEncryptionService encryption, ILogger<EmailService> logger)
        {
            _context = context;
            _encryption = encryption;
            _logger = logger;
        }

        public async Task<bool> IsConfiguredAsync()
        {
            var s = await _context.SmtpSettings.AsNoTracking().FirstOrDefaultAsync();
            return s != null && s.Enabled && !string.IsNullOrWhiteSpace(s.Host) && !string.IsNullOrWhiteSpace(s.FromAddress);
        }

        public Task<EmailSendResult> SendInviteAsync(string toEmail, string toName, string inviteUrl)
        {
            var subject = "You're invited to Smooth Operator";
            var body = $@"<p>Hi {WebSafe(toName)},</p>
<p>You've been invited to access the Smooth Operator console. Use the link below to set your password and finish creating your account.</p>
<p><a href=""{inviteUrl}"">{inviteUrl}</a></p>
<p>This link will expire in 72 hours.</p>";
            return SendAsync(toEmail, toName, subject, body);
        }

        public Task<EmailSendResult> SendPasswordResetAsync(string toEmail, string toName, string resetUrl)
        {
            var subject = "Reset your Smooth Operator password";
            var body = $@"<p>Hi {WebSafe(toName)},</p>
<p>We received a request to reset your password. Use the link below to choose a new one.</p>
<p><a href=""{resetUrl}"">{resetUrl}</a></p>
<p>If you didn't request this, you can safely ignore this email.</p>";
            return SendAsync(toEmail, toName, subject, body);
        }

        public Task<EmailSendResult> SendTestAsync(string toEmail)
            => SendAsync(toEmail, toEmail, "Smooth Operator – SMTP test",
                "<p>This is a test email confirming that your SMTP relay is working.</p>");

        private async Task<EmailSendResult> SendAsync(string toEmail, string toName, string subject, string htmlBody)
        {
            var settings = await _context.SmtpSettings.AsNoTracking().FirstOrDefaultAsync();
            if (settings == null || !settings.Enabled || string.IsNullOrWhiteSpace(settings.Host))
            {
                return new EmailSendResult(false, "SMTP relay is not configured.");
            }

            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(settings.FromName ?? settings.FromAddress, settings.FromAddress));
                message.To.Add(new MailboxAddress(toName, toEmail));
                message.Subject = subject;

                var builder = new BodyBuilder { HtmlBody = htmlBody };
                message.Body = builder.ToMessageBody();

                using var client = new SmtpClient();
                var secure = settings.UseSsl
                    ? (settings.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls)
                    : SecureSocketOptions.None;

                await client.ConnectAsync(settings.Host, settings.Port, secure);

                if (!string.IsNullOrWhiteSpace(settings.Username) && !string.IsNullOrEmpty(settings.EncryptedPassword))
                {
                    var password = _encryption.Decrypt(settings.EncryptedPassword);
                    await client.AuthenticateAsync(settings.Username, password);
                }

                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                return new EmailSendResult(true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMTP send failed");
                return new EmailSendResult(false, ex.Message);
            }
        }

        private static string WebSafe(string s) => System.Net.WebUtility.HtmlEncode(s ?? string.Empty);
    }
}
