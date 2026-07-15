using System;
using System.ComponentModel.DataAnnotations;
using SmoothOperator.Domain.Enums;

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
        public List<string> Tags { get; set; } = [];

        public RecordingOverride RecordingOverride { get; set; } = RecordingOverride.Inherit;
        public bool? RecordingIncludeKeys { get; set; }

        /// <summary><c>null</c> = inherit the parent vault's <see cref="FileTransferPolicy"/> default.</summary>
        public FileTransferPolicy? FileTransferPolicyOverride { get; set; }

        /// <summary>Resolved policy (override, else vault default, else Disabled) — read-only, computed server-side.</summary>
        public FileTransferPolicy EffectiveFileTransferPolicy { get; set; } = FileTransferPolicy.Disabled;

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

        public List<string> Tags { get; set; } = [];

        public RecordingOverride RecordingOverride { get; set; } = RecordingOverride.Inherit;
        public bool? RecordingIncludeKeys { get; set; }

        /// <summary><c>null</c> = inherit the parent vault's file-transfer policy default.</summary>
        public FileTransferPolicy? FileTransferPolicyOverride { get; set; }
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

        public bool RecordingEnabled { get; set; }
        public bool RecordingIncludeKeys { get; set; }

        [Range(0, 3650)]
        public int? RecordingRetentionDays { get; set; }

        /// <summary>Default in-session file-transfer policy for connections in this vault.</summary>
        public FileTransferPolicy FileTransferPolicy { get; set; } = FileTransferPolicy.Disabled;
    }

    public class CreateConnectionGroupDto
    {
        [Required(ErrorMessage = "Vault name is required")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Vault name must be between 1 and 100 characters")]
        public string Name { get; set; } = string.Empty;

        public Guid? ParentGroupId { get; set; }

        public bool RecordingEnabled { get; set; }
        public bool RecordingIncludeKeys { get; set; }

        [Range(0, 3650)]
        public int? RecordingRetentionDays { get; set; }

        /// <summary>Default in-session file-transfer policy for connections in this vault.</summary>
        public FileTransferPolicy FileTransferPolicy { get; set; } = FileTransferPolicy.Disabled;
    }

    public class CredentialDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string CredentialType { get; set; } = "password";
        public string? PublicKey { get; set; }
        public string StorageMode { get; set; } = "Local";
        public Guid? SecretProviderId { get; set; }
        public string? SecretProviderName { get; set; }
        public string? ExternalSecretName { get; set; }
        public string? ExternalSecretVersion { get; set; }
    }

    public class VaultAssignmentsDto
    {
        public List<Guid> UserIds { get; set; } = [];
        public List<Guid> GroupIds { get; set; } = [];
    }

    public class CreateCredentialDto
    {
        [Required(ErrorMessage = "Credential name is required")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 100 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Username is required")]
        [StringLength(255, ErrorMessage = "Username cannot exceed 255 characters")]
        public string Username { get; set; } = string.Empty;

        // Secret is required only when StorageMode == Local.
        [StringLength(4096, MinimumLength = 1, ErrorMessage = "Secret must be between 1 and 4096 characters")]
        public string? Secret { get; set; }

        [Required(ErrorMessage = "CredentialType is required")]
        [RegularExpression("^(password|private_key)$", ErrorMessage = "CredentialType must be 'password' or 'private_key'")]
        public string CredentialType { get; set; } = "password";

        public string? PublicKey { get; set; }

        // External secret provider fields (used when StorageMode == External)
        public string StorageMode { get; set; } = "Local";
        public Guid? SecretProviderId { get; set; }
        public string? ExternalSecretName { get; set; }
        public string? ExternalSecretVersion { get; set; }

        /// <summary>
        /// When true in push flow, the secret will be written to Key Vault and only a reference is stored locally.
        /// When false in link flow, ExternalSecretName must reference an existing KV secret.
        /// </summary>
        public bool PushToVault { get; set; } = false;
    }

    public class ConnectionFileDto
    {
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }
}
