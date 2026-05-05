using System;
using System.Collections.Generic;

namespace SmoothOperator.Domain.Models
{
    public class User
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? PasswordHash { get; set; }

        /// <summary>External IdP subject identifier (OIDC `sub` or SAML NameID) used to link this user to the configured SSO provider.</summary>
        public string? ExternalId { get; set; }

        /// <summary>Which SSO protocol linked this user (Oidc | Saml). Null for purely local accounts.</summary>
        public SsoProviderType? SsoProviderType { get; set; }

        public string? AvatarBase64 { get; set; }
        public string? AvatarMimeType { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ICollection<Role> Roles { get; set; } = new List<Role>();
        public ICollection<Connection> Connections { get; set; } = new List<Connection>();
        public ICollection<ConnectionGroup> ConnectionGroups { get; set; } = new List<ConnectionGroup>();
        public ICollection<UserGroup> Groups { get; set; } = new List<UserGroup>();
    }
}
