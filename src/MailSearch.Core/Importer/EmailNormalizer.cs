using System.Text.RegularExpressions;
using MailSearch.Models;

namespace MailSearch.Importer;

public static partial class EmailNormalizer
{
    /// <summary>
    /// Parses a comma- or semicolon-separated list of RFC 5322-style address strings.
    /// </summary>
    public static List<EmailAddress> ParseAddressList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];

        return raw
            .Split([';', ','])
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .Select(ParseAddress)
            .ToList();
    }

    /// <summary>
    /// Parses a single RFC 5322-style address, e.g. "Alice Smith &lt;alice@example.com&gt;" or "alice@example.com".
    /// </summary>
    public static EmailAddress ParseAddress(string raw)
    {
        raw = raw.Trim();
        var match = NamedEmailRegex().Match(raw);
        if (match.Success)
        {
            return new EmailAddress
            {
                Name = match.Groups["name"].Value.Trim(),
                Email = match.Groups["email"].Value.Trim(),
            };
        }
        return new EmailAddress { Email = raw };
    }

    /// <summary>
    /// Saves a raw attachment to disk (avoiding filename collisions) and returns an <see cref="Attachment"/> record.
    /// </summary>
    public static Attachment BuildAttachment(int emailId, string filename, string? mimeType, byte[] data, string attachmentDir)
    {
        if (!Directory.Exists(attachmentDir))
            Directory.CreateDirectory(attachmentDir);

        // Strip characters that are unsafe in file names
        var safeName = InvalidFileNameCharsRegex().Replace(filename, "_");
        var storagePath = Path.Combine(attachmentDir, safeName);

        // Avoid overwriting existing files
        if (File.Exists(storagePath))
        {
            var ext = Path.GetExtension(safeName);
            var baseName = Path.GetFileNameWithoutExtension(safeName);
            int counter = 1;
            do
            {
                storagePath = Path.Combine(attachmentDir, $"{baseName}_{counter}{ext}");
                counter++;
            }
            while (File.Exists(storagePath));
        }

        File.WriteAllBytes(storagePath, data);

        return new Attachment
        {
            EmailId = emailId,
            Filename = filename,
            MimeType = mimeType,
            Size = data.Length,
            StoragePath = storagePath,
        };
    }

    [GeneratedRegex(@"^(?<name>.+)\s*<(?<email>[^>]+)>$")]
    private static partial Regex NamedEmailRegex();

    [GeneratedRegex(@"[/\\:*?""<>|]")]
    private static partial Regex InvalidFileNameCharsRegex();
}
