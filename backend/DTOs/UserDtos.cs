using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs
{
    public class UserListItemDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool LinkedToEntra { get; set; }
        public bool HasPassword { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<string> Roles { get; set; } = new();
        public List<Guid> VaultIds { get; set; } = new();
    }

    public class UpdateUserRequest
    {
        [Required]
        [StringLength(255, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;
    }

    public class SetUserActiveRequest
    {
        public bool IsActive { get; set; }
    }

    public class RoleCatalogItemDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class SetUserRoleRequest
    {
        [Required]
        [StringLength(64, MinimumLength = 1)]
        public string Role { get; set; } = string.Empty;
    }

    public class SetUserVaultAssignmentsRequest
    {
        public List<Guid> VaultIds { get; set; } = new();
    }
}
