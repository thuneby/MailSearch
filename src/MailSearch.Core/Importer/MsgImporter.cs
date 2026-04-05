using System.IO;
using MsgReader.Outlook;
using MailSearch.Database;
using MailSearch.Models;

namespace MailSearch.Importer;

public static class MsgImporter
{
    /// <summary>
    /// Imports a single .msg file into the MailSearch database.
    /// Attachments are extracted to <paramref name="attachmentDir"/>.
    /// </summary>
    public static ImportResult ImportMsg(string msgFilePath, MailSearchRepository repo, string attachmentDir)
    {
        var result = new ImportResult();

        try
        {
            using var msg = new Storage.Message(msgFilePath, FileAccess.Read);

            var fromAddr = new Models.EmailAddress
            {
                Email = msg.Sender?.Email ?? string.Empty,
                Name = string.IsNullOrEmpty(msg.Sender?.DisplayName) ? null : msg.Sender.DisplayName,
            };

            var toAddresses = ParseRecipients(msg.Recipients, RecipientType.To);
            var ccAddresses = ParseRecipients(msg.Recipients, RecipientType.Cc);
            var bccAddresses = ParseRecipients(msg.Recipients, RecipientType.Bcc);

            var rawAttachments = msg.Attachments?
                .OfType<Storage.Attachment>()
                .ToList() ?? [];
            int attachmentCount = rawAttachments.Count;

            var emailRecord = new Models.Email
            {
                Subject = msg.Subject ?? "(no subject)",
                From = fromAddr,
                To = toAddresses,
                Cc = ccAddresses,
                Bcc = bccAddresses,
                Date = msg.SentOn?.UtcDateTime,
                BodyText = msg.BodyText,
                BodyHtml = msg.BodyHtml,
                HasAttachments = attachmentCount > 0,
                AttachmentCount = attachmentCount,
                SourceFile = Path.GetFullPath(msgFilePath),
                ImportedAt = DateTime.UtcNow,
            };

            int emailId = repo.InsertEmail(emailRecord);
            if (emailId == 0)
            {
                result.Skipped++;
                return result;
            }

            foreach (var att in rawAttachments)
            {
                if (string.IsNullOrEmpty(att.FileName)) continue;
                try
                {
                    if (att.Data == null) continue;
                    var attachmentRecord = EmailNormalizer.BuildAttachment(
                        emailId, att.FileName, att.MimeType, att.Data, attachmentDir);
                    repo.InsertAttachment(attachmentRecord);
                }
                catch
                {
                    // Skip unreadable attachments
                }
            }

            result.Imported++;
        }
        catch
        {
            result.Errors++;
        }

        return result;
    }

    private static List<Models.EmailAddress> ParseRecipients(
        List<Storage.Recipient>? recipients, RecipientType type)
    {
        if (recipients == null) return [];

        return recipients
            .Where(r => r.Type == type)
            .Select(r => new Models.EmailAddress
            {
                Name = string.IsNullOrEmpty(r.DisplayName) ? null : r.DisplayName,
                Email = r.Email ?? r.DisplayName ?? string.Empty,
            })
            .ToList();
    }
}
