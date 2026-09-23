using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Models;
using TrustFlow.Api.Services.Files;

namespace TrustFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public sealed class ProfilePhotosController(AppDbContext db, IMalwareScanner scanner,
    ProfilePhotoProcessor processor) : ControllerBase
{
    [HttpGet("{userId:guid}/photo")]
    public async Task<IActionResult> Get(Guid userId, CancellationToken cancellationToken)
    {
        var photo = await db.ProfilePhotos.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return Ok(new { image = photo is null ? null : "data:image/jpeg;base64," + Convert.ToBase64String(photo.Content) });
    }

    [Authorize(Roles = AppRoles.Client + "," + AppRoles.Freelancer)]
    [EnableRateLimiting(RateLimitPolicies.ProfilePhotoUpload)]
    [HttpPost("me/photo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ProfilePhotoProcessor.MaximumFileSize + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = ProfilePhotoProcessor.MaximumFileSize + 64 * 1024)]
    public async Task<IActionResult> Upload([FromForm] PhotoUpload request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var file = request.File;
        if (file.Length is <= 0 or > ProfilePhotoProcessor.MaximumFileSize)
            return BadRequest(new { message = "Choose a JPG, PNG or WebP photo up to 5 MB." });
        var validType = (Path.GetExtension(file.FileName).ToLowerInvariant(), file.ContentType.ToLowerInvariant()) switch
        {
            (".jpg" or ".jpeg", "image/jpeg") or (".png", "image/png") or (".webp", "image/webp") => true,
            _ => false
        };
        if (!validType) return BadRequest(new { message = "Only JPG, PNG and WebP photos are allowed." });
        await using var input = new MemoryStream();
        await file.CopyToAsync(input, cancellationToken);
        var bytes = input.ToArray();
        var scan = await scanner.ScanAsync(bytes, cancellationToken);
        if (!scan.IsAvailable) return StatusCode(503, new { message = "Photo scanning is unavailable. Your current photo is unchanged. Try again later." });
        if (!scan.IsClean) return BadRequest(new { message = "The photo did not pass the security scan." });
        byte[] normalized;
        try { normalized = await processor.ProcessAsync(bytes, file.ContentType, request.Zoom, request.X, request.Y, cancellationToken); }
        catch (InvalidDataException exception) { return BadRequest(new { message = exception.Message }); }
        var photo = await db.ProfilePhotos.SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (photo is null) { photo = new ProfilePhoto { UserId = userId }; db.ProfilePhotos.Add(photo); }
        photo.Content = normalized;
        photo.UpdatedAt = DateTimeOffset.UtcNow;
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { return Conflict(new { message = "Your photo changed in another window. Try again." }); }
        Response.Headers.CacheControl = "private, no-store";
        return Ok(new { image = "data:image/jpeg;base64," + Convert.ToBase64String(normalized) });
    }

    [Authorize(Roles = AppRoles.Client + "," + AppRoles.Freelancer)]
    [HttpDelete("me/photo")]
    public async Task<IActionResult> Delete(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        await db.ProfilePhotos.Where(p => p.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        return NoContent();
    }

    public sealed class PhotoUpload
    {
        [Required] public IFormFile File { get; init; } = null!;
        [Range(1, 3)] public double Zoom { get; init; } = 1;
        [Range(0, 100)] public double X { get; init; } = 50;
        [Range(0, 100)] public double Y { get; init; } = 50;
    }
}
