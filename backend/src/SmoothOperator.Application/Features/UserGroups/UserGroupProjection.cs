using System;
using System.Linq;
using System.Linq.Expressions;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Features.UserGroups
{
    /// <summary>
    /// Shared EF Core projection from <see cref="UserGroup"/> to <see cref="UserGroupDto"/>.
    /// Centralised so the read shape stays consistent across the user-group handlers.
    /// </summary>
    internal static class UserGroupProjection
    {
        public static readonly Expression<Func<UserGroup, UserGroupDto>> ToDto = g => new UserGroupDto
        {
            Id = g.Id,
            Name = g.Name,
            Description = g.Description,
            OwnerUserId = g.OwnerUserId,
            OwnerName = g.Owner != null ? g.Owner.Name : null,
            CreatedAt = g.CreatedAt,
            MemberCount = g.Members.Count,
            VaultCount = g.Vaults.Count,
            Members = g.Members
                .OrderBy(m => m.Name)
                .Select(m => new UserGroupMemberDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    Email = m.Email,
                    IsActive = m.IsActive
                })
                .ToList()
        };
    }
}
