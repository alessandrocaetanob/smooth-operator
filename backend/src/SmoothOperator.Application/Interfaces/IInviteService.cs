using System;
using System.Threading.Tasks;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Interfaces
{
    public interface IInviteService
    {
        const string TypeUserInvite = "user_invite";
        const string TypePasswordReset = "password_reset";

        /// <summary>Creates an invitation for the given user and returns the absolute URL the user must visit.</summary>
        Task<(Invitation invitation, string token, string url)> CreateAsync(
            Guid userId, string type, TimeSpan ttl, Guid? createdById);

        Task<Invitation?> ValidateAsync(string token);

        Task<Invitation?> RedeemAsync(string token);
    }
}
