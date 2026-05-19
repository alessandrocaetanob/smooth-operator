using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace SmoothOperator.Application.DTOs
{
    public class RegisterRequest
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Name is required")]
        [StringLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [StringLength(128, MinimumLength = 12, ErrorMessage = "Password must be 12-128 characters")]
        public string Password { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [StringLength(128)]
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public UserInfo User { get; set; } = new();
    }

    public class UserInfo
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool HasPassword { get; set; }
        /// <summary>True when the user has been linked to the configured SSO provider (OIDC or SAML).</summary>
        public bool SsoLinked { get; set; }
        /// <summary>"Oidc" | "Saml" when SsoLinked is true; null otherwise.</summary>
        public string? SsoProviderType { get; set; }
        public string? AvatarUrl { get; set; }
        public List<string> Roles { get; set; } = [];
    }

    public class UpdateProfileRequest
    {
        [Required(ErrorMessage = "Name is required")]
        [StringLength(255, MinimumLength = 1, ErrorMessage = "Name must be 1-255 characters")]
        public string Name { get; set; } = string.Empty;

        // Roughly ~2 MB base64 string ceiling (the actual decoded payload is
        // additionally validated server-side to <= 1 MB).
        [StringLength(2_800_000)]
        public string? AvatarBase64 { get; set; }

        [StringLength(64)]
        [RegularExpression("^image/(png|jpeg|jpg|webp)$", ErrorMessage = "Avatar mime type must be image/png, image/jpeg, or image/webp")]
        public string? AvatarMimeType { get; set; }
    }

    public class InviteUserRequest
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
        public string Email { get; set; } = string.Empty;

        [StringLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
        public string? Name { get; set; }

        /// <summary>
        /// Initial role for the invited user. Defaults to <c>User</c>. Cannot be
        /// <c>Owner</c>; the Owner role is reserved for the bootstrap account and
        /// promotions handled separately.
        /// </summary>
        [StringLength(64)]
        public string? Role { get; set; }
    }
}
