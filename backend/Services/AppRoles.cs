using System;
using System.Collections.Generic;
using System.Linq;

namespace Backend.Services
{
    /// <summary>
    /// Built-in role catalog and helpers for the single-role policy.
    ///
    /// Smooth Operator uses a strict "one role per user" model: every user is assigned
    /// exactly one of the roles defined here. Assigning a new role to a user via
    /// <c>UsersController.SetRole</c> replaces any previously held role — there is no
    /// additive role accumulation. The seeder and bootstrap flow always create users
    /// with a single role, and <c>SetRole</c> calls <c>user.Roles.Clear()</c> before
    /// adding the new role to enforce this invariant.
    /// </summary>
    public static class AppRoles
    {
        public const string Owner = "Owner";
        public const string Admin = "Admin";
        public const string TeamAdmin = "TeamAdmin";
        public const string User = "User";

        public const string OwnerOrAdmin = Owner + "," + Admin;
        public const string OwnerAdminOrTeamAdmin = Owner + "," + Admin + "," + TeamAdmin;

        public static readonly IReadOnlyList<string> Defaults = new[]
        {
            Owner,
            Admin,
            TeamAdmin,
            User
        };

        public static bool IsKnown(string? roleName)
            => !string.IsNullOrWhiteSpace(roleName)
               && Defaults.Any(r => string.Equals(r, roleName, StringComparison.OrdinalIgnoreCase));

        public static string Normalize(string roleName)
            => Defaults.First(r => string.Equals(r, roleName, StringComparison.OrdinalIgnoreCase));
    }
}
