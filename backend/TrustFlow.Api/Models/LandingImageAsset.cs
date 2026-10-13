namespace TrustFlow.Api.Models;

public sealed class LandingImageAsset
{
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string AltText { get; set; } = "";
    public string SuggestedPlacement { get; set; } = "";
    public byte[] Content { get; set; } = [];
    public string ContentType { get; set; } = "image/webp";
    public byte[] OriginalContent { get; set; } = [];
    public string OriginalContentType { get; set; } = "image/png";
    public int PixelWidth { get; set; }
    public int PixelHeight { get; set; }
    public string Sha256 { get; set; } = "";
    public string GenerationPrompt { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
