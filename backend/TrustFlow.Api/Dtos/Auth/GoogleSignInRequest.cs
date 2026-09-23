using System.ComponentModel.DataAnnotations;

namespace TrustFlow.Api.Dtos.Auth;

public class GoogleSignInRequest
{
    // The ID token issued by Google Identity Services in the browser.
    [Required]
    public string Credential { get; set; } = string.Empty;

    // Only read when this Google account has no TrustFlow account yet.
    // Existing accounts keep the role they already have.
    public string? Role { get; set; }
}
