using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SkiaSharp;
using TrustFlow.Api.Data;
using TrustFlow.Api.Models;

if (args.Length != 2 || args[0] is not ("import" or "export"))
    throw new ArgumentException("Usage: TrustFlow.LandingAssetTool <import|export> <repository-root>");

var root = Path.GetFullPath(args[1]);
if (!File.Exists(Path.Combine(root, "backend", "TrustFlow.slnx")))
    throw new ArgumentException("The root must be the TrustFlow repository.");

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? throw new InvalidOperationException("Set ConnectionStrings__DefaultConnection locally; never pass credentials as arguments.");
var connection = new NpgsqlConnectionStringBuilder(connectionString);
if (connection.Host is not ("127.0.0.1" or "localhost") || connection.Port != 5433 || connection.Database != "trustflow_dev")
    throw new InvalidOperationException("This asset tool is restricted to local trustflow_dev on port 5433.");

await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);
var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, WriteIndented = true };
var sourceDirectory = Path.Combine(root, "output", "imagegen", "landing");

if (args[0] == "import")
{
    var manifest = JsonSerializer.Deserialize<Manifest>(await File.ReadAllTextAsync(Path.Combine(sourceDirectory, "manifest.json")), jsonOptions)
        ?? throw new InvalidOperationException("The asset manifest is missing.");
    if (manifest.Assets.Length != 10 || manifest.Assets.Select(item => item.Slug).Distinct().Count() != 10)
        throw new InvalidOperationException("Expected ten distinct landing illustrations.");

    var assets = new List<LandingImageAsset>();
    foreach (var item in manifest.Assets)
    {
        if (Path.GetFileName(item.OriginalFileName) != item.OriginalFileName ||
            string.IsNullOrWhiteSpace(item.Slug) || item.Slug.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
            throw new InvalidOperationException("Invalid asset filename or slug.");
        var original = await File.ReadAllBytesAsync(Path.Combine(sourceDirectory, item.OriginalFileName));
        using var bitmap = SKBitmap.Decode(original) ?? throw new InvalidOperationException($"Cannot decode {item.Slug}.");
        if (bitmap.Width != 1536 || bitmap.Height != 1024)
            throw new InvalidOperationException($"Unexpected dimensions for {item.Slug}.");
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, 88)
            ?? throw new InvalidOperationException($"Cannot encode {item.Slug}.");
        var content = encoded.ToArray();
        assets.Add(new LandingImageAsset
        {
            Slug = item.Slug, Title = item.Title, AltText = item.AltText,
            SuggestedPlacement = item.SuggestedPlacement, GenerationPrompt = item.Prompt,
            Content = content, OriginalContent = original, PixelWidth = bitmap.Width, PixelHeight = bitmap.Height,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(content)), CreatedAt = DateTimeOffset.UtcNow,
        });
    }

    // Applying this specific additive migration is an explicit local tooling action.
    var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
    if (pending.Any(name => !name.EndsWith("_AddLandingImageAssets", StringComparison.Ordinal)))
        throw new InvalidOperationException("Other migrations are pending. Apply those separately before importing assets.");
    await db.Database.MigrateAsync();
    await using var transaction = await db.Database.BeginTransactionAsync();
    foreach (var asset in assets)
    {
        var existing = await db.LandingImageAssets.FindAsync(asset.Slug);
        if (existing is null) db.LandingImageAssets.Add(asset);
        else if (existing.Sha256 != asset.Sha256 || !existing.OriginalContent.AsSpan().SequenceEqual(asset.OriginalContent))
            throw new InvalidOperationException($"Asset {asset.Slug} already exists with different content; no overwrite was made.");
    }
    await db.SaveChangesAsync();

    // Read fresh bytes from PostgreSQL and verify both formats before committing.
    db.ChangeTracker.Clear();
    foreach (var expected in assets)
    {
        var saved = await db.LandingImageAssets.AsNoTracking().SingleAsync(item => item.Slug == expected.Slug);
        if (!saved.Content.AsSpan().SequenceEqual(expected.Content) || !saved.OriginalContent.AsSpan().SequenceEqual(expected.OriginalContent) ||
            saved.Sha256 != Convert.ToHexStringLower(SHA256.HashData(saved.Content)))
            throw new InvalidOperationException($"Database read-back failed for {saved.Slug}.");
    }
    await transaction.CommitAsync();
    Console.WriteLine($"Imported and verified {assets.Count} images in local trustflow_dev.LandingImageAssets.");
}

var savedAssets = await db.LandingImageAssets.AsNoTracking().OrderBy(item => item.Slug).ToListAsync();
var publicDirectory = Path.Combine(root, "frontend", "trustflow-web", "public", "images", "landing");
Directory.CreateDirectory(publicDirectory);
foreach (var asset in savedAssets)
{
    if (asset.Slug.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-') ||
        asset.ContentType != "image/webp" || asset.Sha256 != Convert.ToHexStringLower(SHA256.HashData(asset.Content)))
        throw new InvalidOperationException($"Invalid stored asset: {asset.Slug}.");
    var file = Path.Combine(publicDirectory, asset.Slug + ".webp");
    if (File.Exists(file) && !((await File.ReadAllBytesAsync(file)).AsSpan().SequenceEqual(asset.Content)))
        throw new InvalidOperationException($"Refusing to replace different file: {file}.");
    await File.WriteAllBytesAsync(file, asset.Content);
    Console.WriteLine($"{asset.Slug}: {asset.PixelWidth}x{asset.PixelHeight}, PNG {asset.OriginalContent.Length:N0} bytes, WebP {asset.Content.Length:N0} bytes");
}
await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "web-assets.json"), JsonSerializer.Serialize(savedAssets.Select(asset => new
{
    slug = asset.Slug, title = asset.Title, altText = asset.AltText, suggestedPlacement = asset.SuggestedPlacement,
    src = "/images/landing/" + asset.Slug + ".webp", width = asset.PixelWidth, height = asset.PixelHeight,
    sha256 = asset.Sha256, bytes = asset.Content.Length,
}), jsonOptions));
Console.WriteLine($"Exported {savedAssets.Count} images from PostgreSQL to {publicDirectory}.");

internal sealed record Manifest(ManifestAsset[] Assets);
internal sealed record ManifestAsset(string Slug, string Title, string AltText, string SuggestedPlacement, string OriginalFileName, string Prompt);
