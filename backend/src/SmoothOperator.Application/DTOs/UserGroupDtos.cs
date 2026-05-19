using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SmoothOperator.Application.DTOs
{
    public class UserGroupDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid? OwnerUserId { get; set; }
        public string? OwnerName { get; set; }
        public DateTime CreatedAt { get; set; }
        public int MemberCount { get; set; }
        public int VaultCount { get; set; }
        public List<UserGroupMemberDto> Members { get; set; } = [];
    }

    public class UserGroupMemberDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class UserGroupVaultDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid? ParentGroupId { get; set; }
    }

    public class CreateUserGroupRequest
    {
        [Required(ErrorMessage = "Group name is required")]
        [StringLength(120, MinimumLength = 1, ErrorMessage = "Group name must be between 1 and 120 characters")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }

        public Guid? OwnerUserId { get; set; }
    }

    public class RenameUserGroupRequest
    {
        [Required(ErrorMessage = "Group name is required")]
        [StringLength(120, MinimumLength = 1, ErrorMessage = "Group name must be between 1 and 120 characters")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }

        public Guid? OwnerUserId { get; set; }
    }

    public class SetUserGroupMembersRequest
    {
        [Required(ErrorMessage = "UserIds list is required")]
        public List<Guid> UserIds { get; set; } = [];
    }
}
