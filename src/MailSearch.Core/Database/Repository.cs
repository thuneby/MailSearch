using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using MailSearch.Core.Models;

namespace MailSearch.Core.Database;

public class MailSearchRepository : IDisposable
{
    public static readonly string DefaultDbPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                     ".mailsearch", "mailsearch.db");

    private readonly SqliteConnection _db;

    public MailSearchRepository(string dbPath = "")
    {
        if (string.IsNullOrEmpty(dbPath))
            dbPath = DefaultDbPath;

        var dir = Path.GetDirectoryName(dbPath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        _db = new SqliteConnection($"Data Source={dbPath}");
        _db.Open();

        ExecuteNonQuery("PRAGMA foreign_keys = ON");
        ExecuteNonQuery("PRAGMA journal_mode = WAL");
        Schema.InitializeDatabase(_db);
    }

    public void Close() => _db.Close();
    public void Dispose() => _db.Dispose();

    // ── Emails ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts an email using INSERT OR IGNORE to skip duplicates.
    /// Returns the new row id, or 0 if a duplicate message_id was skipped.
    /// </summary>
    public int InsertEmail(Email email)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT OR IGNORE INTO emails
              (message_id, subject, from_name, from_email, to_json, cc_json, bcc_json,
               date, body_text, body_html, has_attachments, attachment_count,
               source_file, imported_at, folder_id, folder_path)
            VALUES
              ($messageId, $subject, $fromName, $fromEmail, $toJson, $ccJson, $bccJson,
               $date, $bodyText, $bodyHtml, $hasAttachments, $attachmentCount,
               $sourceFile, $importedAt, $folderId, $folderPath)";

        cmd.Parameters.AddWithValue("$messageId", (object?)email.MessageId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$subject", email.Subject);
        cmd.Parameters.AddWithValue("$fromName", (object?)email.From.Name ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$fromEmail", email.From.Email);
        cmd.Parameters.AddWithValue("$toJson", JsonSerializer.Serialize(email.To));
        cmd.Parameters.AddWithValue("$ccJson", JsonSerializer.Serialize(email.Cc));
        cmd.Parameters.AddWithValue("$bccJson", JsonSerializer.Serialize(email.Bcc));
        cmd.Parameters.AddWithValue("$date", (object?)(email.Date?.ToString("o")) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$bodyText", (object?)email.BodyText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$bodyHtml", (object?)email.BodyHtml ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$hasAttachments", email.HasAttachments ? 1 : 0);
        cmd.Parameters.AddWithValue("$attachmentCount", email.AttachmentCount);
        cmd.Parameters.AddWithValue("$sourceFile", email.SourceFile);
        cmd.Parameters.AddWithValue("$importedAt", email.ImportedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$folderId", (object?)email.FolderId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$folderPath", (object?)email.FolderPath ?? DBNull.Value);

        int changes = cmd.ExecuteNonQuery();
        if (changes == 0) return 0;

        using var idCmd = _db.CreateCommand();
        idCmd.CommandText = "SELECT last_insert_rowid()";
        return Convert.ToInt32(idCmd.ExecuteScalar());
    }

    public Email? GetEmailById(int id)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT * FROM emails WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadEmail(reader) : null;
    }

    public List<Email> ListEmails(int limit = 50, int offset = 0, int? folderId = null, string? tag = null)
    {
        using var cmd = _db.CreateCommand();

        if (tag != null)
        {
            cmd.CommandText = @"
                SELECT e.* FROM emails e
                JOIN email_tags et ON et.email_id = e.id
                JOIN tags t ON t.id = et.tag_id
                WHERE t.name = $tag
                ORDER BY e.date DESC, e.id DESC
                LIMIT $limit OFFSET $offset";
            cmd.Parameters.AddWithValue("$tag", tag);
        }
        else if (folderId.HasValue)
        {
            cmd.CommandText = @"
                SELECT * FROM emails WHERE folder_id = $folderId
                ORDER BY date DESC, id DESC LIMIT $limit OFFSET $offset";
            cmd.Parameters.AddWithValue("$folderId", folderId.Value);
        }
        else
        {
            cmd.CommandText = "SELECT * FROM emails ORDER BY date DESC, id DESC LIMIT $limit OFFSET $offset";
        }

        cmd.Parameters.AddWithValue("$limit", limit);
        cmd.Parameters.AddWithValue("$offset", offset);

        using var reader = cmd.ExecuteReader();
        var results = new List<Email>();
        while (reader.Read())
            results.Add(ReadEmail(reader));
        return results;
    }

    public int CountEmails()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM emails";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public bool DeleteEmail(int id)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM emails WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    // ── Attachments ──────────────────────────────────────────────────────────

    public int InsertAttachment(Attachment attachment)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO attachments (email_id, filename, mime_type, size, storage_path)
            VALUES ($emailId, $filename, $mimeType, $size, $storagePath)";
        cmd.Parameters.AddWithValue("$emailId", attachment.EmailId);
        cmd.Parameters.AddWithValue("$filename", attachment.Filename);
        cmd.Parameters.AddWithValue("$mimeType", (object?)attachment.MimeType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$size", attachment.Size);
        cmd.Parameters.AddWithValue("$storagePath", (object?)attachment.StoragePath ?? DBNull.Value);
        cmd.ExecuteNonQuery();

        using var idCmd = _db.CreateCommand();
        idCmd.CommandText = "SELECT last_insert_rowid()";
        return Convert.ToInt32(idCmd.ExecuteScalar());
    }

    public List<Attachment> GetAttachmentsByEmailId(int emailId)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT * FROM attachments WHERE email_id = $emailId";
        cmd.Parameters.AddWithValue("$emailId", emailId);
        using var reader = cmd.ExecuteReader();
        var results = new List<Attachment>();
        while (reader.Read())
            results.Add(ReadAttachment(reader));
        return results;
    }

    // ── Tags ─────────────────────────────────────────────────────────────────

    public int UpsertTag(string name)
    {
        using var selectCmd = _db.CreateCommand();
        selectCmd.CommandText = "SELECT id FROM tags WHERE name = $name";
        selectCmd.Parameters.AddWithValue("$name", name);
        var existing = selectCmd.ExecuteScalar();
        if (existing != null) return Convert.ToInt32(existing);

        using var insertCmd = _db.CreateCommand();
        insertCmd.CommandText = "INSERT INTO tags (name, created_at) VALUES ($name, $createdAt)";
        insertCmd.Parameters.AddWithValue("$name", name);
        insertCmd.Parameters.AddWithValue("$createdAt", DateTime.UtcNow.ToString("o"));
        insertCmd.ExecuteNonQuery();

        using var idCmd = _db.CreateCommand();
        idCmd.CommandText = "SELECT last_insert_rowid()";
        return Convert.ToInt32(idCmd.ExecuteScalar());
    }

    public void AddTagToEmail(int emailId, string tagName)
    {
        int tagId = UpsertTag(tagName);
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO email_tags (email_id, tag_id) VALUES ($emailId, $tagId)";
        cmd.Parameters.AddWithValue("$emailId", emailId);
        cmd.Parameters.AddWithValue("$tagId", tagId);
        cmd.ExecuteNonQuery();
    }

    public void RemoveTagFromEmail(int emailId, string tagName)
    {
        using var selectCmd = _db.CreateCommand();
        selectCmd.CommandText = "SELECT id FROM tags WHERE name = $name";
        selectCmd.Parameters.AddWithValue("$name", tagName);
        var tagId = selectCmd.ExecuteScalar();
        if (tagId == null) return;

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM email_tags WHERE email_id = $emailId AND tag_id = $tagId";
        cmd.Parameters.AddWithValue("$emailId", emailId);
        cmd.Parameters.AddWithValue("$tagId", tagId);
        cmd.ExecuteNonQuery();
    }

    public List<Tag> GetTagsForEmail(int emailId)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT t.* FROM tags t
            JOIN email_tags et ON et.tag_id = t.id
            WHERE et.email_id = $emailId";
        cmd.Parameters.AddWithValue("$emailId", emailId);
        using var reader = cmd.ExecuteReader();
        var results = new List<Tag>();
        while (reader.Read())
            results.Add(ReadTag(reader));
        return results;
    }

    public List<Tag> ListTags()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT * FROM tags ORDER BY name";
        using var reader = cmd.ExecuteReader();
        var results = new List<Tag>();
        while (reader.Read())
            results.Add(ReadTag(reader));
        return results;
    }

    // ── Full-text search ──────────────────────────────────────────────────────

    public List<EmailSearchResult> SearchEmails(string query, int limit = 50, int offset = 0)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT e.*, snippet(emails_fts, -1, '[', ']', '…', 20) AS snippet,
                   bm25(emails_fts) AS rank
            FROM emails_fts
            JOIN emails e ON e.id = emails_fts.rowid
            WHERE emails_fts MATCH $query
            ORDER BY rank
            LIMIT $limit OFFSET $offset";
        cmd.Parameters.AddWithValue("$query", query);
        cmd.Parameters.AddWithValue("$limit", limit);
        cmd.Parameters.AddWithValue("$offset", offset);

        using var reader = cmd.ExecuteReader();
        var results = new List<EmailSearchResult>();
        while (reader.Read())
        {
            var email = ReadEmail(reader);
            results.Add(new EmailSearchResult
            {
                Id = email.Id,
                MessageId = email.MessageId,
                Subject = email.Subject,
                From = email.From,
                To = email.To,
                Cc = email.Cc,
                Bcc = email.Bcc,
                Date = email.Date,
                BodyText = email.BodyText,
                BodyHtml = email.BodyHtml,
                HasAttachments = email.HasAttachments,
                AttachmentCount = email.AttachmentCount,
                SourceFile = email.SourceFile,
                ImportedAt = email.ImportedAt,
                FolderId = email.FolderId,
                FolderPath = email.FolderPath,
                Snippet = reader["snippet"] as string,
                Rank = reader["rank"] is double r ? r : null,
            });
        }
        return results;
    }

    // ── Row mapping helpers ───────────────────────────────────────────────────

    private static Email ReadEmail(SqliteDataReader reader)
    {
        var options = new JsonSerializerOptions();
        return new Email
        {
            Id = Convert.ToInt32(reader["id"]),
            MessageId = reader["message_id"] as string,
            Subject = reader["subject"] as string ?? string.Empty,
            From = new EmailAddress
            {
                Name = reader["from_name"] as string,
                Email = reader["from_email"] as string ?? string.Empty,
            },
            To = JsonSerializer.Deserialize<List<EmailAddress>>(reader["to_json"] as string ?? "[]", options) ?? [],
            Cc = JsonSerializer.Deserialize<List<EmailAddress>>(reader["cc_json"] as string ?? "[]", options) ?? [],
            Bcc = JsonSerializer.Deserialize<List<EmailAddress>>(reader["bcc_json"] as string ?? "[]", options) ?? [],
            Date = reader["date"] is string ds && ds.Length > 0 ? DateTime.Parse(ds, CultureInfo.InvariantCulture).ToUniversalTime() : null,
            BodyText = reader["body_text"] as string,
            BodyHtml = reader["body_html"] as string,
            HasAttachments = Convert.ToInt32(reader["has_attachments"]) == 1,
            AttachmentCount = Convert.ToInt32(reader["attachment_count"]),
            SourceFile = reader["source_file"] as string ?? string.Empty,
            ImportedAt = DateTime.Parse(reader["imported_at"] as string ?? DateTime.UtcNow.ToString("o"), CultureInfo.InvariantCulture).ToUniversalTime(),
            FolderId = reader["folder_id"] is long fi ? (int)fi : null,
            FolderPath = reader["folder_path"] as string,
        };
    }

    private static Attachment ReadAttachment(SqliteDataReader reader)
    {
        return new Attachment
        {
            Id = Convert.ToInt32(reader["id"]),
            EmailId = Convert.ToInt32(reader["email_id"]),
            Filename = reader["filename"] as string ?? string.Empty,
            MimeType = reader["mime_type"] as string,
            Size = Convert.ToInt64(reader["size"]),
            StoragePath = reader["storage_path"] as string,
        };
    }

    private static Tag ReadTag(SqliteDataReader reader)
    {
        return new Tag
        {
            Id = Convert.ToInt32(reader["id"]),
            Name = reader["name"] as string ?? string.Empty,
            CreatedAt = DateTime.Parse(reader["created_at"] as string ?? DateTime.UtcNow.ToString("o"), CultureInfo.InvariantCulture).ToUniversalTime(),
        };
    }

    private void ExecuteNonQuery(string sql)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
