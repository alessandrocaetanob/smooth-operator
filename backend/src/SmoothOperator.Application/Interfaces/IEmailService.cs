using System.Threading.Tasks;

namespace SmoothOperator.Application.Interfaces
{
    public record EmailSendResult(bool Success, string? Error);

    public interface IEmailService
    {
        Task<bool> IsConfiguredAsync();
        Task<EmailSendResult> SendInviteAsync(string toEmail, string toName, string inviteUrl);
        Task<EmailSendResult> SendPasswordResetAsync(string toEmail, string toName, string resetUrl);
        Task<EmailSendResult> SendTestAsync(string toEmail);
    }
}
