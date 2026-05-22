using System;
using System.Security.Cryptography;
using System.Text;

namespace SmoothOperator.Application.Features.Webhooks
{
    /// <summary>
    /// Computes the <c>X-SmoothOperator-Signature</c> header value. Receivers
    /// verify a request by recomputing <c>HMAC-SHA256("{timestamp}.{body}", secret)</c>
    /// — binding the timestamp into the signed message blocks replay attacks.
    /// </summary>
    public static class WebhookSignature
    {
        /// <summary>Returns <c>sha256=&lt;lowercase-hex&gt;</c> for the given secret, timestamp and body.</summary>
        public static string Compute(string secret, string timestamp, string body)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{body}"));
            return "sha256=" + Convert.ToHexStringLower(hash);
        }
    }
}
