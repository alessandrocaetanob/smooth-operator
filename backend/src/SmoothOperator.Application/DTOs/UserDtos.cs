using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SmoothOperator.Application.DTOs
{
    public class UserListItemDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool SsoLinked { get; set; }
        public string? SsoProviderType { get; set; }
        public bool HasPassword { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<string> Roles { get; set; } = [];
        public List<Guid> VaultIds { get; set; } = [];
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
        public List<Guid> VaultIds { get; set; } = [];
    }

    public class EffectiveVaultDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid? ParentGroupId { get; set; }
    }

    public class EffectiveGroupVaultsDto
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public List<EffectiveVaultDto> Vaults { get; set; } = [];
    }

    public class UserEffectiveVaultsDto
    {
        public Guid UserId { get; set; }
        public List<EffectiveVaultDto> Direct { get; set; } = [];
        public List<EffectiveGroupVaultsDto> ViaGroups { get; set; } = [];
        public List<EffectiveVaultDto> Merged { get; set; } = [];
    }

    public class EffectiveUserSourceDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public List<string> Roles { get; set; } = [];
        public bool DirectAssignment { get; set; }
        public List<Guid> ViaGroupIds { get; set; } = [];
    }

    public class VaultEffectiveUsersDto
    {
        public Guid VaultId { get; set; }
        public string VaultName { get; set; } = string.Empty;
        public List<EffectiveUserSourceDto> Users { get; set; } = [];
    }
}
