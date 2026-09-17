using System.Buffers.Binary;

namespace TrustFlow.Api.Services.Files;

public sealed class MilestoneDeliveryFileValidator(PdfResumeValidator pdfValidator)
{
    public const int MaximumFileCount = 5;
    public const int MaximumFileSize = 25 * 1024 * 1024;
    public const long MaximumTotalSize = 100L * 1024 * 1024;

    private const int MaximumDimension = 12_000;
    private const long MaximumPixels = 60_000_000;

    public string? Validate(string fileName, string contentType, byte[] content)
    {
        if (content.Length is <= 0 or > MaximumFileSize)
            return "Each delivery file must be no larger than 25 MB.";

        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName) || safeName.Length > 200 ||
            safeName.Any(char.IsControl) || !string.Equals(safeName, fileName, StringComparison.Ordinal))
            return "A delivery file has an invalid name.";

        var extension = Path.GetExtension(safeName).ToLowerInvariant();
        return (extension, contentType.ToLowerInvariant()) switch
        {
            (".pdf", "application/pdf") =>
                pdfValidator.Validate(content, MaximumFileSize, "delivery PDF"),
            (".png", "image/png") => ValidatePng(content),
            (".jpg" or ".jpeg", "image/jpeg") => ValidateJpeg(content),
            (".webp", "image/webp") => ValidateWebP(content),
            _ => "Only PDF, JPG, PNG, and WebP delivery files are allowed."
        };
    }

    private static string? ValidatePng(byte[] content)
    {
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (content.Length < 33 || !content.AsSpan(0, 8).SequenceEqual(signature) ||
            !content.AsSpan(12, 4).SequenceEqual("IHDR"u8))
            return "The uploaded PNG is malformed.";

        var width = BinaryPrimitives.ReadUInt32BigEndian(content.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(content.AsSpan(20, 4));
        return ValidateDimensions(width, height, "PNG");
    }

    private static string? ValidateJpeg(byte[] content)
    {
        if (content.Length < 4 || content[0] != 0xFF || content[1] != 0xD8 ||
            content[^2] != 0xFF || content[^1] != 0xD9)
            return "The uploaded JPEG is malformed.";

        var offset = 2;
        while (offset + 4 <= content.Length)
        {
            if (content[offset++] != 0xFF)
                return "The uploaded JPEG is malformed.";
            while (offset < content.Length && content[offset] == 0xFF) offset++;
            if (offset >= content.Length) break;
            var marker = content[offset++];
            if (marker is 0xD8 or 0xD9 || marker is >= 0xD0 and <= 0xD7) continue;
            if (offset + 2 > content.Length) break;
            var length = BinaryPrimitives.ReadUInt16BigEndian(content.AsSpan(offset, 2));
            if (length < 2 || offset + length > content.Length) break;
            if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF)
            {
                if (length < 7) break;
                var height = BinaryPrimitives.ReadUInt16BigEndian(content.AsSpan(offset + 3, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(content.AsSpan(offset + 5, 2));
                return ValidateDimensions(width, height, "JPEG");
            }
            offset += length;
        }
        return "The JPEG dimensions could not be verified.";
    }

    private static string? ValidateWebP(byte[] content)
    {
        if (content.Length < 30 || !content.AsSpan(0, 4).SequenceEqual("RIFF"u8) ||
            !content.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            return "The uploaded WebP is malformed.";

        var chunk = content.AsSpan(12, 4);
        uint width;
        uint height;
        if (chunk.SequenceEqual("VP8X"u8))
        {
            width = 1u + ReadUInt24LittleEndian(content.AsSpan(24, 3));
            height = 1u + ReadUInt24LittleEndian(content.AsSpan(27, 3));
        }
        else if (chunk.SequenceEqual("VP8L"u8) && content.Length >= 25 && content[20] == 0x2F)
        {
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(content.AsSpan(21, 4));
            width = (bits & 0x3FFF) + 1;
            height = ((bits >> 14) & 0x3FFF) + 1;
        }
        else if (chunk.SequenceEqual("VP8 "u8) && content.Length >= 30 &&
                 content.AsSpan(23, 3).SequenceEqual(new byte[] { 0x9D, 0x01, 0x2A }))
        {
            width = (uint)(BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(26, 2)) & 0x3FFF);
            height = (uint)(BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(28, 2)) & 0x3FFF);
        }
        else return "The WebP dimensions could not be verified.";

        return ValidateDimensions(width, height, "WebP");
    }

    private static uint ReadUInt24LittleEndian(ReadOnlySpan<byte> value) =>
        (uint)(value[0] | value[1] << 8 | value[2] << 16);

    private static string? ValidateDimensions(uint width, uint height, string kind) =>
        width == 0 || height == 0 || width > MaximumDimension || height > MaximumDimension ||
        (long)width * height > MaximumPixels
            ? $"The {kind} dimensions are too large or invalid."
            : null;
}
