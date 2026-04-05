using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MailSearch.Core.Database;
using MailSearch.Importer;


namespace MailSearch.Core.Importer;

/// <summary>
/// Imports PST/OST files into the MailSearch database.
///
/// PST/OST reading requires a third-party library. This implementation uses an
/// <see cref="IPstReader"/> abstraction so you can plug in any library.
///
/// Recommended free/open-source option:
///   XstReader.Api (https://github.com/iluvadev/XstReader) – add via NuGet and
///   implement <see cref="IPstReader"/> wrapping XstReader calls.
///
/// Commercial options: Aspose.Email, Independentsoft.Email.Mapi.
/// </summary>
public static class PstImporter
{
    /// <summary>
    /// Imports a PST/OST file using the supplied <paramref name="pstReader"/>.
    /// </summary>
    public static async Task<ImportResult> ImportPstAsync(
        string pstFilePath,
        MailSearchRepository repo,
        string attachmentDir,
        IPstReader pstReader,
        Action<int>? onProgress = null)
    {
        var result = new ImportResult();
        await pstReader.ReadMessagesAsync(pstFilePath, (msg) =>
        {
            try
            {
                var email = new Models.Email
                {
                    MessageId = msg.MessageId,
                    Subject = msg.Subject ?? "(no subject)",
                    From = new Models.EmailAddress
                    {
                        Name = msg.SenderName,
                        Email = msg.SenderEmail ?? string.Empty,
                    },
                    To = EmailNormalizer.ParseAddressList(msg.DisplayTo),
                    Cc = EmailNormalizer.ParseAddressList(msg.DisplayCc),
                    Bcc = EmailNormalizer.ParseAddressList(msg.DisplayBcc),
                    Date = msg.DeliveryTime,
                    BodyText = msg.BodyText,
                    BodyHtml = msg.BodyHtml,
                    HasAttachments = msg.HasAttachments,
                    AttachmentCount = msg.AttachmentCount,
                    SourceFile = Path.GetFullPath(pstFilePath),
                    ImportedAt = DateTime.UtcNow,
                    FolderPath = msg.FolderPath,
                };

                int emailId = repo.InsertEmail(email);
                if (emailId == 0)
                {
                    result.Skipped++;
                    return;
                }

                foreach (var att in msg.Attachments)
                {
                    if (string.IsNullOrEmpty(att.Filename) || att.Data == null) continue;
                    try
                    {
                        var attachmentRecord = EmailNormalizer.BuildAttachment(
                            emailId, att.Filename, att.MimeType, att.Data, attachmentDir);
                        repo.InsertAttachment(attachmentRecord);
                    }
                    catch
                    {
                        // Skip attachments that cannot be saved
                    }
                }

                result.Imported++;
                onProgress?.Invoke(result.Imported);
            }
            catch
            {
                result.Errors++;
            }
        });

        return result;
    }
}

// ── Abstractions for PST reading ─────────────────────────────────────────────

/// <summary>Implement this interface with your preferred PST/OST reading library.</summary>
public interface IPstReader
{
    Task ReadMessagesAsync(string pstFilePath, Action<IPstMessage> onMessage);
}

/// <summary>Represents a single email message from a PST/OST file.</summary>
public interface IPstMessage
{
    string? MessageId { get; }
    string? Subject { get; }
    string? SenderName { get; }
    string? SenderEmail { get; }
    string? DisplayTo { get; }
    string? DisplayCc { get; }
    string? DisplayBcc { get; }
    DateTime? DeliveryTime { get; }
    string? BodyText { get; }
    string? BodyHtml { get; }
    bool HasAttachments { get; }
    int AttachmentCount { get; }
    string? FolderPath { get; }
    IReadOnlyList<IPstAttachment> Attachments { get; }
}

/// <summary>Represents a single attachment from a PST/OST message.</summary>
public interface IPstAttachment
{
    string? Filename { get; }
    string? MimeType { get; }
    byte[]? Data { get; }
}
