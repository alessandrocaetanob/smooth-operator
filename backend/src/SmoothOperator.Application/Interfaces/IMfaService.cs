namespace SmoothOperator.Application.Interfaces
{
    public interface IMfaService
    {
        /// <summary>Returns true when the 6-digit TOTP code is valid for the given Base32 secret
        /// within the standard ±1 window (allows for up to 30 s clock drift).</summary>
        bool VerifyTotp(string secretBase32, string code);
    }
}
