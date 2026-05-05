using System;

namespace SmoothOperator.Application.Exceptions
{
    /// <summary>
    /// Thrown when a secret provider operation fails (auth, not-found, access-denied, etc.).
    /// <see cref="ErrorCode"/> provides a stable machine-readable reason for audit logging.
    /// </summary>
    public class SecretProviderException : AppException
    {
        public string ErrorCode { get; }

        public SecretProviderException(string errorCode, string message)
            : base(message)
        {
            ErrorCode = errorCode;
        }

        public SecretProviderException(string errorCode, string message, Exception inner)
            : base(message, inner)
        {
            ErrorCode = errorCode;
        }
    }
}
