using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs
{
    public class InvitePreviewDto
    {
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    public class RedeemInviteRequest
    {
        [Required]
        [StringLength(128, MinimumLength = 8, ErrorMessage = "Password must be 8-128 characters.")]
        public string Password { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Name { get; set; }
    }

    public class ForgotPasswordRequest
    {
        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string Email { get; set; } = string.Empty;
    }
}
