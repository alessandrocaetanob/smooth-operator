using System;
using System.Collections.Generic;
using System.Linq;

namespace SmoothOperator.Infrastructure.Services
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

        /// <summary>
        /// Returns the canonical casing for a known role name, or the original input
        /// unchanged when the value is not a known role. Callers are responsible for
        /// guarding with <see cref="IsKnown"/> before relying on the result; this method
        /// intentionally does not throw so an unvalidated bad input cannot crash the
        /// request pipeline with a 500.
        /// </summary>
        public static string Normalize(string roleName)
            => Defaults.FirstOrDefault(r => string.Equals(r, roleName, StringComparison.OrdinalIgnoreCase))
               ?? roleName;
    }
}
