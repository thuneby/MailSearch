using Microsoft.Data.Sqlite;

namespace MailSearch.Database;

public static class Schema
{
    public static void InitializeDatabase(SqliteConnection db)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS emails (
              id              INTEGER PRIMARY KEY AUTOINCREMENT,
              message_id      TEXT UNIQUE,
              subject         TEXT NOT NULL DEFAULT '',
              from_name       TEXT,
              from_email      TEXT NOT NULL DEFAULT '',
              to_json         TEXT NOT NULL DEFAULT '[]',
              cc_json         TEXT NOT NULL DEFAULT '[]',
              bcc_json        TEXT NOT NULL DEFAULT '[]',
              date            TEXT,
              body_text       TEXT,
              body_html       TEXT,
              has_attachments INTEGER NOT NULL DEFAULT 0,
              attachment_count INTEGER NOT NULL DEFAULT 0,
              source_file     TEXT NOT NULL DEFAULT '',
              imported_at     TEXT NOT NULL,
              folder_id       INTEGER,
              folder_path     TEXT
            );

            CREATE TABLE IF NOT EXISTS attachments (
              id           INTEGER PRIMARY KEY AUTOINCREMENT,
              email_id     INTEGER NOT NULL REFERENCES emails(id) ON DELETE CASCADE,
              filename     TEXT NOT NULL DEFAULT '',
              mime_type    TEXT,
              size         INTEGER NOT NULL DEFAULT 0,
              storage_path TEXT
            );

            CREATE TABLE IF NOT EXISTS tags (
              id         INTEGER PRIMARY KEY AUTOINCREMENT,
              name       TEXT NOT NULL UNIQUE,
              created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS email_tags (
              email_id INTEGER NOT NULL REFERENCES emails(id) ON DELETE CASCADE,
              tag_id   INTEGER NOT NULL REFERENCES tags(id) ON DELETE CASCADE,
              PRIMARY KEY (email_id, tag_id)
            );

            CREATE VIRTUAL TABLE IF NOT EXISTS emails_fts USING fts5(
              subject,
              from_name,
              from_email,
              body_text,
              content='emails',
              content_rowid='id'
            );

            CREATE TRIGGER IF NOT EXISTS emails_ai AFTER INSERT ON emails BEGIN
              INSERT INTO emails_fts(rowid, subject, from_name, from_email, body_text)
                VALUES (new.id, new.subject, new.from_name, new.from_email, new.body_text);
            END;

            CREATE TRIGGER IF NOT EXISTS emails_ad AFTER DELETE ON emails BEGIN
              INSERT INTO emails_fts(emails_fts, rowid, subject, from_name, from_email, body_text)
                VALUES ('delete', old.id, old.subject, old.from_name, old.from_email, old.body_text);
            END;

            CREATE TRIGGER IF NOT EXISTS emails_au AFTER UPDATE ON emails BEGIN
              INSERT INTO emails_fts(emails_fts, rowid, subject, from_name, from_email, body_text)
                VALUES ('delete', old.id, old.subject, old.from_name, old.from_email, old.body_text);
              INSERT INTO emails_fts(rowid, subject, from_name, from_email, body_text)
                VALUES (new.id, new.subject, new.from_name, new.from_email, new.body_text);
            END;
        ";
        cmd.ExecuteNonQuery();
    }
}
