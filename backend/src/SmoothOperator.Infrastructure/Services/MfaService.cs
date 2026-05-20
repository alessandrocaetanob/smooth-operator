using OtpNet;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Infrastructure.Services
{
    public class MfaService : IMfaService
    {
        public bool VerifyTotp(string secretBase32, string code)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Length != 6)
                return false;

            var secretBytes = Base32Encoding.ToBytes(secretBase32);
            var totp = new Totp(secretBytes);
            return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
        }
    }
}
