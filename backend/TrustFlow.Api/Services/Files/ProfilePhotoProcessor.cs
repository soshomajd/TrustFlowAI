using SkiaSharp;

namespace TrustFlow.Api.Services.Files;

public sealed class ProfilePhotoProcessor
{
    public const int MaximumFileSize = 5 * 1024 * 1024;
    // Bound simultaneous decodes as well as the size of each input.
    private readonly SemaphoreSlim decodeSlots = new(2);

    public async Task<byte[]> ProcessAsync(byte[] content, string contentType, double zoom, double x, double y, CancellationToken cancellationToken)
    {
        await decodeSlots.WaitAsync(cancellationToken);
        try { return Process(content, contentType, zoom, x, y); }
        finally { decodeSlots.Release(); }
    }

    private static byte[] Process(byte[] content, string contentType, double zoom, double x, double y)
    {
        using var data = SKData.CreateCopy(content);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("This photo could not be read safely.");
        var mime = codec.EncodedFormat switch
        {
            SKEncodedImageFormat.Jpeg => "image/jpeg", SKEncodedImageFormat.Png => "image/png",
            SKEncodedImageFormat.Webp => "image/webp", _ => ""
        };
        if (mime.Length == 0 || !string.Equals(mime, contentType, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The photo format does not match its file type.");
        var info = codec.Info;
        if (info.Width < 128 || info.Height < 128 || info.Width > 8000 || info.Height > 8000 || (long)info.Width * info.Height > 24_000_000)
            throw new InvalidDataException("Choose a photo at least 128 pixels wide and tall, up to 24 megapixels.");
        if (codec.FrameCount > 1) throw new InvalidDataException("Choose a still photo, not an animated image.");
        using var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new InvalidDataException("The photo is incomplete or damaged. Choose another image.");
        var origin = (int)codec.EncodedOrigin;
        var width = origin >= 5 ? info.Height : info.Width;
        var height = origin >= 5 ? info.Width : info.Height;
        var side = Math.Min(width, height) / zoom;
        var left = (width - side) * x / 100;
        var top = (height - side) * y / 100;
        using var output = SKSurface.Create(new SKImageInfo(512, 512, SKColorType.Rgba8888, SKAlphaType.Opaque));
        var canvas = output.Canvas;
        canvas.Clear(SKColors.White);
        canvas.Scale((float)(512 / side));
        canvas.Translate(-(float)left, -(float)top);
        // Match the EXIF orientation used by the browser's photo preview.
        switch (origin)
        {
            case 2: canvas.Translate(info.Width, 0); canvas.Scale(-1, 1); break;
            case 3: canvas.Translate(info.Width, info.Height); canvas.RotateDegrees(180); break;
            case 4: canvas.Translate(0, info.Height); canvas.Scale(1, -1); break;
            case 5: canvas.RotateDegrees(90); canvas.Scale(1, -1); break;
            case 6: canvas.Translate(info.Height, 0); canvas.RotateDegrees(90); break;
            case 7: canvas.Translate(info.Height, info.Width); canvas.RotateDegrees(90); canvas.Scale(-1, 1); break;
            case 8: canvas.Translate(0, info.Width); canvas.RotateDegrees(270); break;
        }
        using var image = SKImage.FromBitmap(bitmap);
        canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        // Encoding a new surface stores pixels only, never source metadata or trailing data.
        using var snapshot = output.Snapshot();
        using var encoded = snapshot.Encode(SKEncodedImageFormat.Jpeg, 88);
        return encoded.ToArray();
    }
}
