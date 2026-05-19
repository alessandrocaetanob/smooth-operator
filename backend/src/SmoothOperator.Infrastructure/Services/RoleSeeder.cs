using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SmoothOperator.Infrastructure.Services
{
    public static class RoleSeeder
    {
        private static readonly Dictionary<string, string> RoleDescriptions =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [AppRoles.Owner] = "Root access. First account created during setup.",
                [AppRoles.Admin] = "Can create vaults, credentials, groups, and invite users.",
                [AppRoles.TeamAdmin] = "Can create and manage connections in assigned vaults.",
                [AppRoles.User] = "Can use connections in assigned vaults."
            };

        public static async Task SeedDefaultsAsync(AppDbContext context, ILogger logger)
        {
            var existingRoleNames = (await context.Roles.ToListAsync())
                .Select(r => r.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var roleName in AppRoles.Defaults)
            {
                if (existingRoleNames.Contains(roleName))
                {
                    continue;
                }

                context.Roles.Add(new Role
                {
                    Id = Guid.NewGuid(),
                    Name = roleName,
                    Description = RoleDescriptions[roleName]
                });
                if (logger.IsEnabled(LogLevel.Information))
                    logger.LogInformation("Created default role {RoleName}", roleName);
            }

            await context.SaveChangesAsync();

            var rolesByName = await context.Roles
                .ToDictionaryAsync(r => r.Name, StringComparer.OrdinalIgnoreCase);

            var defaultUserRole = rolesByName[AppRoles.User];
            var ownerRole = rolesByName[AppRoles.Owner];

            var usersWithoutRoles = await context.Users
                .Include(u => u.Roles)
                .Where(u => !u.Roles.Any())
                .ToListAsync();

            foreach (var user in usersWithoutRoles)
            {
                user.Roles.Add(defaultUserRole);
            }

            if (usersWithoutRoles.Count > 0 && logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Assigned default {RoleName} role to {Count} user(s)",
                    AppRoles.User, usersWithoutRoles.Count);
            }

            var hasOwner = await context.Users
                .Include(u => u.Roles)
                .AnyAsync(u => u.Roles.Any(r => r.Name == AppRoles.Owner));

            if (!hasOwner)
            {
                var bootstrapUser = await context.Users
                    .Include(u => u.Roles)
                    .OrderBy(u => u.CreatedAt)
                    .FirstOrDefaultAsync();

                if (bootstrapUser != null)
                {
                    // Single-role policy: replace any roles assigned earlier in this
                    // seeding run (e.g. the default User role just added above) so the
                    // bootstrap user ends up with exactly one role.
                    bootstrapUser.Roles.Clear();
                    bootstrapUser.Roles.Add(ownerRole);
                    logger.LogWarning("No owner found. Assigned {RoleName} role to {Email}.",
                        AppRoles.Owner, bootstrapUser.Email);
                }
            }

            await context.SaveChangesAsync();
        }
    }
}
