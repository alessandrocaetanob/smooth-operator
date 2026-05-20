using System;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Interfaces
{
    public interface ITokenService
    {
        string CreateToken(User user);
        TimeSpan TokenLifetime { get; }

        /// <summary>Short-lived (5 min) token returned to the frontend when MFA is required.
        /// Uses a different audience so it cannot be used for regular API calls.</summary>
        string CreateMfaChallengeToken(Guid userId);

        /// <summary>Validates an MFA challenge token and returns the userId, or null if invalid/expired.</summary>
        Guid? ValidateMfaChallengeToken(string token);
    }
}
