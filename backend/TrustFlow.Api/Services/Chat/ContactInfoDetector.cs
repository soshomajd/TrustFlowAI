using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TrustFlow.Api.Services.Chat;

// Chat exists so the two sides can sort out the work while the escrow protects
// them. A phone number, an email address, a link or a messenger handle moves
// the conversation, and soon the payment, somewhere the escrow cannot reach, so
// a message that carries one is refused rather than quietly edited.
//
// This is a best effort filter, not a wall: someone determined can spell a
// number out in a way no rule foresees. It catches the plain cases and the
// common disguises, and the reply says why so an honest person is not confused.
// It is also careful with ordinary work talk: "post it on Facebook", a CSS
// "@media" rule, a date or a 0x address must not be stopped.
public static class ContactInfoDetector
{
    public const string Email = "email address";
    public const string Phone = "phone number";
    public const string Link = "link";
    public const string Handle = "messenger or social account";

    // Places people go to talk privately. Naming one is only a problem when
    // it is offered as a way to reach someone.
    private const string Channels =
        "whatsapp|whats app|telegram|skype|discord|signal|viber|wechat|" +
        "bale|eitaa|rubika|soroush|gmail|yahoo|hotmail|outlook|icloud";

    // Social networks are ordinary subjects of work ("manage our Instagram"),
    // so only "add me on X" or "X username" counts.
    private const string Socials =
        "instagram|insta|snapchat|facebook|messenger|linkedin|twitter";

    private static readonly RegexOptions Options =
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;

    // name@host.tld, with the spaces and spelled out versions people use to
    // get past a filter: "name (at) mail . com".
    private static readonly Regex EmailPattern = new(
        @"[\w.+\-]+\s*(?:@|\(\s*at\s*\)|\[\s*at\s*\])\s*[\w\-]+(?:\s*(?:\.|\(\s*dot\s*\)|\[\s*dot\s*\]|\bdot\b)\s*[a-z]{2,})+",
        Options);

    // "name at gmail dot com", "gmail.com"
    private static readonly Regex SpelledEmailPattern = new(
        $@"\bat\s+(?:{Channels})\b\s*(?:\.|\bdot\b)\s*[a-z]{{2,}}|\b(?:gmail|yahoo|hotmail|outlook|icloud)\s*(?:\.|\bdot\b)\s*com\b",
        Options);

    // Only endings that are rarely a typo for a space after a full stop
    // ("done.in a day"), so "in", "to", "me", "us", "de" and "co" are not here.
    private static readonly Regex LinkPattern = new(
        @"https?\s*:\s*/\s*/|\bwww\s*\.|\b[a-z0-9\-]+\.(?:com|net|org|io|ir|app|dev|info|biz|xyz|link|chat|ai|tv|site|online|shop|store|page|uk|fr|ru|cn|gl)\b|\b(?:t\.me|wa\.me|bit\.ly|tinyurl\.com|linktr\.ee)\b",
        Options);

    private static readonly Regex AtHandlePattern = new(
        @"(?<![\w@])@([a-z0-9_.]{3,})",
        Options);

    private static readonly Regex HandleContextPattern = new(
        $@"\b(?:{Channels}|{Socials}|handle|username|account|profile)\b",
        Options);

    // "add me on telegram", "my whatsapp is", "call me", "instagram
    // username". "message me" alone is fine: that is what a chat is for.
    private static readonly Regex ReachMePattern = new(
        $@"\b(?:add|text|message|msg|dm|contact|reach|ping|call|phone|email|mail)\s+(?:me|us)\s+(?:on|at|via|through|by|in)\b|\b(?:call|phone|email|mail)\s+(?:me|us)\b|\b(?:my|our|ur|your)\s+(?:(?:{Channels})\b|number\b|phone\b|mobile\b|cell\b|e-?mail\b|mail\b)|\b(?:{Channels}|{Socials})\s*(?:id|username|handle|number|no\.?)\b",
        Options);

    private static readonly Regex DatePattern = new(
        @"\b\d{4}[-/.]\d{1,2}[-/.]\d{1,2}\b|\b\d{1,2}[-/.]\d{1,2}[-/.]\d{2,4}\b",
        Options);

    // Addresses and transaction hashes are long runs of digits and letters.
    private static readonly Regex HexPattern = new(
        @"\b0x[0-9a-f]{6,}\b",
        Options);

    // Digits with the separators people put inside phone numbers. A leading
    // plus or 00 means an international number, which is shorter to spot.
    private static readonly Regex PhonePattern = new(
        @"(?<!\d)(\+|00)?\d(?:[\s().\-]*\d){6,}",
        Options);

    private static readonly Regex NumberWordPattern = new(
        @"\b(?:(?:zero|oh|one|two|three|four|five|six|seven|eight|nine)(?:[\s\-,.]+|\b)){7,}",
        Options);

    private static readonly Regex NonNameCharacters = new(
        @"[^a-z0-9_.]",
        Options);

    // Returns the kinds of contact detail found, in a fixed order, or an
    // empty list when the text is clean.
    public static IReadOnlyList<string> Find(string text)
    {
        var plain = Normalize(text);
        var found = new List<string>();

        if (EmailPattern.IsMatch(plain) || SpelledEmailPattern.IsMatch(plain))
        {
            found.Add(Email);
        }

        if (HasPhoneNumber(plain))
        {
            found.Add(Phone);
        }

        var withoutEmails = EmailPattern.Replace(plain, " ");

        if (LinkPattern.IsMatch(withoutEmails))
        {
            found.Add(Link);
        }

        if (HasHandle(withoutEmails) || ReachMePattern.IsMatch(plain))
        {
            found.Add(Handle);
        }

        return found;
    }

    private static bool HasPhoneNumber(string plain)
    {
        // A date or a hash is a run of digits too, and nobody should be
        // stopped for naming one.
        var cleaned = HexPattern.Replace(DatePattern.Replace(plain, " "), " ");

        foreach (Match match in PhonePattern.Matches(cleaned))
        {
            var digits = match.Value.Count(char.IsAsciiDigit);
            var international = match.Groups[1].Success;

            if (digits >= (international ? 7 : 8))
            {
                return true;
            }
        }

        return NumberWordPattern.IsMatch(cleaned);
    }

    // "@name" is a handle when it looks like one (digits, an underscore or a
    // dot) or when the message is talking about accounts. A bare "@media" or
    // "@Injectable" in a technical sentence is code, not a person.
    private static bool HasHandle(string plain)
    {
        foreach (Match match in AtHandlePattern.Matches(plain))
        {
            var name = NonNameCharacters.Replace(match.Groups[1].Value, "");

            if (name.Any(character =>
                    character is '_' or '.' || char.IsAsciiDigit(character)))
            {
                return true;
            }

            if (HandleContextPattern.IsMatch(plain))
            {
                return true;
            }
        }

        return false;
    }

    // One plain shape to search: compatibility forms folded (full width
    // letters and digits), invisible characters removed, and the Persian and
    // Arabic digits turned into the digits the patterns look for.
    private static string Normalize(string text)
    {
        var folded = text.Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(folded.Length);

        foreach (var character in folded)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);

            if (category == UnicodeCategory.Format ||
                (category == UnicodeCategory.Control &&
                 character is not ('\n' or '\r' or '\t')))
            {
                continue;
            }

            builder.Append(character switch
            {
                >= '۰' and <= '۹' => (char)('0' + (character - '۰')),
                >= '٠' and <= '٩' => (char)('0' + (character - '٠')),
                _ => character
            });
        }

        return builder.ToString();
    }
}
