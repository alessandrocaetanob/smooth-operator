using System;
using System.Collections.Generic;
using System.Linq;

namespace Backend.Services
{
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
