using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Tokens;

namespace TrustFlow.Api.Services.Files;

public sealed class PdfResumeValidator
{
    public const int MaximumFileSize = 5 * 1024 * 1024;
    public const int MaximumPageCount = 25;

    private static readonly HashSet<string> ForbiddenNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "JavaScript", "JS", "AA", "Launch", "EmbeddedFiles", "Filespec", "EF",
        "RichMedia", "Movie", "Sound", "XFA", "SubmitForm", "ImportData", "GoToE", "AcroForm"
    };

    public string? Validate(byte[] content)
        => Validate(content, MaximumFileSize, "resume");

    public string? Validate(byte[] content, int maximumFileSize, string fileLabel)
    {
        if (content.Length == 0 || content.Length > maximumFileSize)
            return $"The {fileLabel} is too large.";

        if (content.Length < 8 || !content.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            return "The uploaded file is not a PDF.";

        // Catch dangerous names even when they use escaped PDF name bytes.
        var normalized = NormalizePdfNames(content);
        if (ForbiddenNames.Any(name => normalized.Contains('/' + name, StringComparison.OrdinalIgnoreCase)))
            return "PDFs with scripts, forms, actions, media, or embedded files are not allowed.";

        try
        {
            using var document = PdfDocument.Open(content, new ParsingOptions { UseLenientParsing = false });
            if (document.IsEncrypted)
                return "Password-protected or encrypted PDFs are not allowed.";
            if (document.NumberOfPages is < 1 or > MaximumPageCount)
                return $"The resume must contain between 1 and {MaximumPageCount} pages.";

            var visitedReferences = new HashSet<IndirectReference>();
            var inspectedTokens = 0;
            if (ContainsForbiddenContent(
                    document.Structure.Catalog.CatalogDictionary,
                    document.Structure,
                    visitedReferences,
                    ref inspectedTokens,
                    depth: 0))
                return "PDFs with scripts, forms, actions, media, or embedded files are not allowed.";

            for (var pageNumber = 1; pageNumber <= document.NumberOfPages; pageNumber++)
                _ = document.GetPage(pageNumber).Letters.Count;
        }
        catch
        {
            return "The PDF is malformed or uses unsupported features.";
        }

        return null;
    }

    private static bool ContainsForbiddenContent(
        IToken token,
        Structure structure,
        HashSet<IndirectReference> visitedReferences,
        ref int inspectedTokens,
        int depth)
    {
        inspectedTokens++;
        if (inspectedTokens > 50_000 || depth > 100)
            throw new InvalidDataException("The PDF object graph is too complex.");

        switch (token)
        {
            case NameToken name:
                return ForbiddenNames.Contains(name.Data);

            case DictionaryToken dictionary:
                foreach (var pair in dictionary.Data)
                {
                    if (ForbiddenNames.Contains(pair.Key) ||
                        ContainsForbiddenContent(pair.Value, structure, visitedReferences,
                            ref inspectedTokens, depth + 1))
                        return true;
                }
                return false;

            case ArrayToken array:
                foreach (var item in array.Data)
                {
                    if (ContainsForbiddenContent(item, structure, visitedReferences,
                            ref inspectedTokens, depth + 1))
                        return true;
                }
                return false;

            case StreamToken stream:
                return ContainsForbiddenContent(stream.StreamDictionary, structure, visitedReferences,
                    ref inspectedTokens, depth + 1);

            case IndirectReferenceToken reference:
                if (!visitedReferences.Add(reference.Data))
                    return false;
                var resolved = structure.GetObject(reference.Data);
                return resolved is not null && ContainsForbiddenContent(resolved.Data, structure,
                    visitedReferences, ref inspectedTokens, depth + 1);

            default:
                return false;
        }
    }

    private static string NormalizePdfNames(byte[] content)
    {
        var text = Encoding.Latin1.GetString(content);
        var builder = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '#' && index + 2 < text.Length &&
                byte.TryParse(text.AsSpan(index + 1, 2), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out var decoded))
            {
                builder.Append((char)decoded);
                index += 2;
            }
            else
            {
                builder.Append(text[index]);
            }
        }
        return builder.ToString();
    }
}
