using System;
using System.ComponentModel.DataAnnotations;

namespace SmoothOperator.Application.DTOs
{
    public class ConnectionDto
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "Connection name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Protocol is required")]
        [RegularExpression("^(rdp|ssh|vnc)$", ErrorMessage = "Protocol must be one of: rdp, ssh, vnc")]
        public string Protocol { get; set; } = "rdp";

        [Required(ErrorMessage = "Host ID is required")]
        public Guid HostId { get; set; }

        public Guid? CredentialId { get; set; }
        public Guid? ConnectionGroupId { get; set; }
        public string Settings { get; set; } = "{}";
        public List<string> Tags { get; set; } = new();

        // Navigation properties (optional for responses)
        public HostDto? Host { get; set; }
        public ConnectionGroupDto? ConnectionGroup { get; set; }
    }

    public class CreateConnectionDto
    {
        [Required(ErrorMessage = "Connection name is required")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 100 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Protocol is required")]
        [RegularExpression("^(rdp|ssh|vnc)$", ErrorMessage = "Protocol must be one of: rdp, ssh, vnc")]
        public string Protocol { get; set; } = "rdp";

        [Required(ErrorMessage = "Host ID is required")]
        public Guid HostId { get; set; }

        public Guid? CredentialId { get; set; }
        public Guid? ConnectionGroupId { get; set; }

        [RegularExpression(@"^\{.*\}$", ErrorMessage = "Settings must be a valid JSON object")]
        public string Settings { get; set; } = "{}";

        public List<string> Tags { get; set; } = new();
    }

    public class HostDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    public class CreateHostDto
    {
        [Required(ErrorMessage = "Host name is required")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 100 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Address is required")]
        [StringLength(255, ErrorMessage = "Address cannot exceed 255 characters")]
        public string Address { get; set; } = string.Empty;
    }

    public class ConnectionGroupDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid? ParentGroupId { get; set; }
        public int? UserCount { get; set; }
        public int? GroupCount { get; set; }
    }

    public class CreateConnectionGroupDto
    {
        [Required(ErrorMessage = "Vault name is required")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Vault name must be between 1 and 100 characters")]
        public string Name { get; set; } = string.Empty;

        public Guid? ParentGroupId { get; set; }
    }

    public class CredentialDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string CredentialType { get; set; } = "password";
        public string? PublicKey { get; set; }
    }

    public class VaultAssignmentsDto
    {
        public List<Guid> UserIds { get; set; } = new();
        public List<Guid> GroupIds { get; set; } = new();
    }

    public class CreateCredentialDto
    {
        [Required(ErrorMessage = "Credential name is required")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 100 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Username is required")]
        [StringLength(255, ErrorMessage = "Username cannot exceed 255 characters")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Secret is required")]
        [StringLength(4096, MinimumLength = 1, ErrorMessage = "Secret must be between 1 and 4096 characters")]
        public string Secret { get; set; } = string.Empty;

        [Required(ErrorMessage = "CredentialType is required")]
        [RegularExpression("^(password|private_key)$", ErrorMessage = "CredentialType must be 'password' or 'private_key'")]
        public string CredentialType { get; set; } = "password";

        public string? PublicKey { get; set; }
    }

    public class ConnectionFileDto
    {
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }
}
