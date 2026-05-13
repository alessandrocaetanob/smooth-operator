using System;
using System.Threading.Tasks;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace SmoothOperator.Infrastructure.Services
{
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
            var body = BuildEmailHtml(
                preheader: "Set up your account and get started with Smooth Operator.",
                bodyHtml: $@"
                  <p style=""margin:0 0 16px;font-size:16px;line-height:1.6;color:#1a1a2e;"">
                    Hi {WebSafe(toName)},
                  </p>
                  <p style=""margin:0 0 24px;font-size:15px;line-height:1.7;color:#374151;"">
                    You've been invited to access the <strong>Smooth Operator</strong> console.
                    Use the button below to set your password and finish creating your account.
                  </p>
                  {CtaButton(inviteUrl, "Set up your account")}
                  <p style=""margin:24px 0 0;font-size:13px;line-height:1.6;color:#6b7280;"">
                    This link will expire in <strong>72 hours</strong>. If you weren't expecting this
                    invitation, you can safely ignore this email.
                  </p>"
            );
            return SendAsync(toEmail, toName, subject, body);
        }

        public Task<EmailSendResult> SendPasswordResetAsync(string toEmail, string toName, string resetUrl)
        {
            var subject = "Reset your Smooth Operator password";
            var body = BuildEmailHtml(
                preheader: "We received a request to reset your Smooth Operator password.",
                bodyHtml: $@"
                  <p style=""margin:0 0 16px;font-size:16px;line-height:1.6;color:#1a1a2e;"">
                    Hi {WebSafe(toName)},
                  </p>
                  <p style=""margin:0 0 24px;font-size:15px;line-height:1.7;color:#374151;"">
                    We received a request to reset your password. Click the button below to
                    choose a new one.
                  </p>
                  {CtaButton(resetUrl, "Reset my password")}
                  <p style=""margin:24px 0 0;font-size:13px;line-height:1.6;color:#6b7280;"">
                    If you didn't request a password reset, you can safely ignore this email —
                    your password won't be changed.
                  </p>"
            );
            return SendAsync(toEmail, toName, subject, body);
        }

        public Task<EmailSendResult> SendTestAsync(string toEmail)
        {
            var body = BuildEmailHtml(
                preheader: "Your SMTP relay is working correctly.",
                bodyHtml: @"
                  <p style=""margin:0 0 16px;font-size:16px;line-height:1.6;color:#1a1a2e;font-weight:600;"">
                    SMTP relay confirmed ✓
                  </p>
                  <p style=""margin:0;font-size:15px;line-height:1.7;color:#374151;"">
                    This is a test email confirming that <strong>Smooth Operator</strong> can
                    deliver messages via your configured SMTP relay. No further action is needed.
                  </p>"
            );
            return SendAsync(toEmail, toEmail, "Smooth Operator – SMTP test", body);
        }

        private static string BuildEmailHtml(string preheader, string bodyHtml)
        {
            return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8""/>
  <meta name=""viewport"" content=""width=device-width,initial-scale=1""/>
  <meta http-equiv=""X-UA-Compatible"" content=""IE=edge""/>
  <title>Smooth Operator</title>
</head>
<body style=""margin:0;padding:0;background:#f4f6fb;font-family:Arial,Helvetica,sans-serif;"">

  <!-- Preheader (hidden preview text) -->
  <span style=""display:none;max-height:0;overflow:hidden;mso-hide:all;"">
    {WebSafe(preheader)}&nbsp;&#847;&nbsp;&#847;&nbsp;&#847;&nbsp;&#847;&nbsp;&#847;
  </span>

  <!-- Outer wrapper -->
  <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%""
         style=""background:#f4f6fb;padding:40px 16px;"">
    <tr>
      <td align=""center"">

        <!-- Card -->
        <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0""
               style=""width:100%;max-width:560px;border-radius:12px;overflow:hidden;
                       box-shadow:0 2px 16px rgba(0,0,0,0.08);"">

          <!-- Header -->
          <tr>
            <td style=""background:#0054D6;padding:28px 40px;"">
              <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"">
                <tr>
                  <td>
                    <!-- Wordmark -->
                    <span style=""font-family:Arial,Helvetica,sans-serif;font-size:20px;
                                  font-weight:700;color:#ffffff;letter-spacing:-0.3px;"">
                      Smooth Operator
                    </span>
                  </td>
                  <td align=""right"">
                    <!-- Geometric accent mark -->
                    <span style=""display:inline-block;width:28px;height:28px;
                                  border-radius:50%;background:rgba(255,255,255,0.18);""></span>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- Body -->
          <tr>
            <td style=""background:#ffffff;padding:40px 40px 32px;"">
              {bodyHtml}
            </td>
          </tr>

          <!-- Footer -->
          <tr>
            <td style=""background:#f9fafb;border-top:1px solid #e5e7eb;
                        padding:20px 40px;border-radius:0 0 12px 12px;"">
              <p style=""margin:0;font-size:12px;line-height:1.6;color:#9ca3af;
                          font-family:Arial,Helvetica,sans-serif;"">
                You received this email from <strong style=""color:#6b7280;"">Smooth Operator</strong>.
                This is an automated message — please do not reply directly to this email.
              </p>
            </td>
          </tr>

        </table>
        <!-- /Card -->

      </td>
    </tr>
  </table>

</body>
</html>";
        }

        private static string CtaButton(string url, string label)
        {
            return $@"
              <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0""
                     style=""margin:8px 0 8px;"">
                <tr>
                  <td style=""border-radius:8px;background:#0054D6;"">
                    <a href=""{WebSafe(url)}""
                       style=""display:inline-block;padding:14px 32px;font-size:15px;
                               font-weight:700;color:#ffffff;text-decoration:none;
                               border-radius:8px;font-family:Arial,Helvetica,sans-serif;
                               letter-spacing:0.1px;"">
                      {WebSafe(label)}
                    </a>
                  </td>
                </tr>
              </table>";
        }

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
                var sslOptions = settings.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
                var secure = settings.UseSsl ? sslOptions : SecureSocketOptions.None;

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
