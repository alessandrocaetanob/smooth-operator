using System;
using System.Security.Cryptography;

namespace SmoothOperator.Application.Features.Webhooks
{
    /// <summary>Generates a fresh webhook signing secret.</summary>
    public static class WebhookSecretGenerator
    {
        private const string Prefix = "whsec_";
        private const int SecretBytes = 32; // 256-bit HMAC key

        /// <summary>Returns a URL-safe secret of the form <c>whsec_&lt;43 base64url chars&gt;</c>.</summary>
        public static string Generate()
        {
            var buf = new byte[SecretBytes];
            RandomNumberGenerator.Fill(buf);
            var encoded = Convert.ToBase64String(buf)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
            return Prefix + encoded;
        }
    }
}
