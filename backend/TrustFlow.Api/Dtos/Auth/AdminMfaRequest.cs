using System.ComponentModel.DataAnnotations;

namespace TrustFlow.Api.Dtos.Auth;

public sealed class AdminMfaRequest
{
    [Required, StringLength(100)] public string ChallengeToken { get; set; } = string.Empty;
    [Required, RegularExpression("^[0-9]{6}$")] public string Code { get; set; } = string.Empty;
}
