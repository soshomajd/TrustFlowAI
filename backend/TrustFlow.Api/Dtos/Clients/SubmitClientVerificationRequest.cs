using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace TrustFlow.Api.Dtos.Clients;

public sealed class SubmitClientVerificationRequest
{
    [Required, MaxLength(150)]
    public string LegalName { get; init; } = string.Empty;

    [Required, RegularExpression("^[A-Za-z]{2}$")]
    public string CountryCode { get; init; } = string.Empty;

    [MaxLength(100)]
    public string? RegistrationNumber { get; init; }

    [FromForm(Name = "file")]
    public IFormFile? File { get; init; }
}
