using System.Text;
using SkiaSharp;
using UglyToad.PdfPig;

namespace TrustFlow.Api.Services.Files;

// Only newly encoded, deliberately reduced review pixels leave this service.
// The original container, metadata, PDF objects and external links never do.
public sealed class DeliveryReviewPreview
{
    public const int MaximumDimension = 960;
    private readonly SemaphoreSlim slots = new(2);

    public static bool Supports(string contentType) => contentType is
        "application/pdf" or "image/png" or "image/jpeg" or "image/webp";

    public async Task<byte[]> CreateAsync(byte[] content, string contentType, CancellationToken cancellationToken)
    {
        await slots.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (contentType == "application/pdf") return PdfCard(content);
            if (contentType is "image/png" or "image/jpeg" or "image/webp")
            {
                try { return Image(content, contentType); }
                catch (Exception exception) when (exception is InvalidDataException or ArgumentException or InvalidOperationException)
                { return Card("Image review preview", "This image cannot be previewed safely. The original remains protected for payment or human review."); }
            }
            return Card("Review preview unavailable", "This file type has no safe review preview. The original remains protected for payment or human review.");
        }
        finally { slots.Release(); }
    }

    private static byte[] Image(byte[] content, string contentType)
    {
        using var data = SKData.CreateCopy(content);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("Unreadable image.");
        var expected = codec.EncodedFormat switch
        {
            SKEncodedImageFormat.Png => "image/png", SKEncodedImageFormat.Jpeg => "image/jpeg",
            SKEncodedImageFormat.Webp => "image/webp", _ => ""
        };
        var info = codec.Info;
        if (expected != contentType || info.Width <= 0 || info.Height <= 0 || info.Width > 12_000 ||
            info.Height > 12_000 || (long)info.Width * info.Height > 60_000_000 || codec.FrameCount > 1)
            throw new InvalidDataException("Unsafe image dimensions or format.");
        // Native scaled decoding saves memory for formats which support it.
        var scale = Math.Min(1f, 1200f / Math.Max(info.Width, info.Height));
        var dimensions = codec.GetScaledDimensions(scale);
        var decodedInfo = new SKImageInfo(dimensions.Width, dimensions.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(decodedInfo);
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new InvalidDataException("Incomplete image.");
        var orientation = (int)codec.EncodedOrigin;
        var rotated = orientation >= 5;
        var width = rotated ? bitmap.Height : bitmap.Width;
        var height = rotated ? bitmap.Width : bitmap.Height;
        var ratio = Math.Min(1d, (double)MaximumDimension / Math.Max(width, height));
        var outputWidth = Math.Max(1, (int)Math.Round(width * ratio));
        var outputHeight = Math.Max(1, (int)Math.Round(height * ratio));
        using var surface = SKSurface.Create(new SKImageInfo(outputWidth, outputHeight, SKColorType.Rgba8888, SKAlphaType.Opaque));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        canvas.Save();
        canvas.Scale((float)ratio);
        switch (orientation)
        {
            case 2: canvas.Translate(bitmap.Width, 0); canvas.Scale(-1, 1); break;
            case 3: canvas.Translate(bitmap.Width, bitmap.Height); canvas.RotateDegrees(180); break;
            case 4: canvas.Translate(0, bitmap.Height); canvas.Scale(1, -1); break;
            case 5: canvas.RotateDegrees(90); canvas.Scale(1, -1); break;
            case 6: canvas.Translate(bitmap.Height, 0); canvas.RotateDegrees(90); break;
            case 7: canvas.Translate(bitmap.Height, bitmap.Width); canvas.RotateDegrees(90); canvas.Scale(-1, 1); break;
            case 8: canvas.Translate(0, bitmap.Width); canvas.RotateDegrees(270); break;
        }
        using var image = SKImage.FromBitmap(bitmap);
        canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        canvas.Restore();
        Watermark(canvas, outputWidth, outputHeight);
        return Encode(surface);
    }

    private static byte[] PdfCard(byte[] content)
    {
        try
        {
            using var document = PdfDocument.Open(content, new ParsingOptions { UseLenientParsing = false });
            if (document.IsEncrypted || document.NumberOfPages < 1) throw new InvalidDataException("Unreadable PDF.");
            // This is an excerpt for review, not a rendering of the full PDF.
            var text = document.GetPage(1).Text;
            var cleaned = new StringBuilder(800);
            foreach (var character in text)
            {
                if (cleaned.Length >= 800) break;
                if (!char.IsControl(character) || char.IsWhiteSpace(character))
                    cleaned.Append(char.IsWhiteSpace(character) ? ' ' : character);
            }
            var excerpt = cleaned.ToString().Trim();
            return Card("PDF review excerpt - first page only", excerpt.Length == 0
                ? "This PDF has no readable first-page text. Full page artwork is not shown. The original remains available to the human reviewer."
                : excerpt + " ... [limited first-page excerpt; original PDF protected]");
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        { return Card("PDF review preview", "A safe text excerpt could not be generated. The original remains protected for payment or human review."); }
    }

    private static byte[] Card(string title, string text)
    {
        const int width = 900, height = 640;
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(243, 246, 250));
        using var paint = new SKPaint { Color = new SKColor(25, 38, 58), IsAntialias = true };
        using var titleFont = new SKFont(SKTypeface.Default, 24);
        using var font = new SKFont(SKTypeface.Default, 19);
        canvas.DrawText(title, 36, 48, SKTextAlign.Left, titleFont, paint);
        var y = 94f;
        var line = new StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length + word.Length > 76)
            {
                canvas.DrawText(line.ToString(), 36, y, SKTextAlign.Left, font, paint);
                y += 32;
                line.Clear();
                if (y > height - 90) break;
            }
            if (line.Length > 0) line.Append(' ');
            line.Append(word.Length > 76 ? word[..76] : word);
        }
        if (line.Length > 0 && y <= height - 90) canvas.DrawText(line.ToString(), 36, y, SKTextAlign.Left, font, paint);
        Watermark(canvas, width, height);
        return Encode(surface);
    }

    private static void Watermark(SKCanvas canvas, int width, int height)
    {
        // Intersecting bands remain visible on light and dark artwork.
        using var band = new SKPaint { Color = new SKColor(255, 255, 255, 160), IsAntialias = true };
        using var ink = new SKPaint { Color = new SKColor(25, 40, 65, 190), IsAntialias = true };
        var textSize = Math.Clamp(Math.Min(width, height) / 10f, 6f, 24f);
        using var font = new SKFont(SKTypeface.Default, textSize);
        var spacing = Math.Max(24f, Math.Min(width, height) / 3f);
        for (var y = -height * 1f; y <= height * 2; y += spacing)
        {
            canvas.Save();
            canvas.Translate(width / 2f, y);
            canvas.RotateDegrees(-22);
            canvas.DrawRect(-width * 2, -textSize - 4, width * 4, textSize + 10, band);
            for (var x = -width * 2f; x < width * 2; x += Math.Max(70, textSize * 14))
                canvas.DrawText("TRUSTFLOW REVIEW ONLY", x, 0, SKTextAlign.Left, font, ink);
            canvas.Restore();
        }
    }

    private static byte[] Encode(SKSurface surface)
    {
        using var snapshot = surface.Snapshot();
        using var encoded = snapshot.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }
}
