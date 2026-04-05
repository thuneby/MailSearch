import path from 'path';
import os from 'os';
import fs from 'fs';
import Database from 'better-sqlite3';
import { initializeDatabase } from './schema';
import { Email, EmailRow, EmailSearchResult, rowToEmail } from '../models/email';
import { Attachment, AttachmentRow, rowToAttachment } from '../models/attachment';
import { Tag, TagRow, rowToTag } from '../models/tag';

export const DEFAULT_DB_PATH = path.join(os.homedir(), '.mailsearch', 'mailsearch.db');

export class MailSearchRepository {
  private db: Database.Database;

  constructor(dbPath: string = DEFAULT_DB_PATH) {
    const dir = path.dirname(dbPath);
    if (!fs.existsSync(dir)) {
      fs.mkdirSync(dir, { recursive: true });
    }
    this.db = new Database(dbPath);
    this.db.pragma('foreign_keys = ON');
    this.db.pragma('journal_mode = WAL');
    initializeDatabase(this.db);
  }

  close(): void {
    this.db.close();
  }

  // ── Emails ──────────────────────────────────────────────────────────────

  insertEmail(email: Email): number {
    const stmt = this.db.prepare(`
      INSERT OR IGNORE INTO emails
        (message_id, subject, from_name, from_email, to_json, cc_json, bcc_json,
         date, body_text, body_html, has_attachments, attachment_count,
         source_file, imported_at, folder_id, folder_path)
      VALUES
        (@messageId, @subject, @fromName, @fromEmail, @toJson, @ccJson, @bccJson,
         @date, @bodyText, @bodyHtml, @hasAttachments, @attachmentCount,
         @sourceFile, @importedAt, @folderId, @folderPath)
    `);
    const result = stmt.run({
      messageId: email.messageId ?? null,
      subject: email.subject,
      fromName: email.from.name ?? null,
      fromEmail: email.from.email,
      toJson: JSON.stringify(email.to),
      ccJson: JSON.stringify(email.cc),
      bccJson: JSON.stringify(email.bcc),
      date: email.date ? email.date.toISOString() : null,
      bodyText: email.bodyText ?? null,
      bodyHtml: email.bodyHtml ?? null,
      hasAttachments: email.hasAttachments ? 1 : 0,
      attachmentCount: email.attachmentCount,
      sourceFile: email.sourceFile,
      importedAt: email.importedAt.toISOString(),
      folderId: email.folderId ?? null,
      folderPath: email.folderPath ?? null,
    });
    // result.changes === 0 means INSERT OR IGNORE skipped a duplicate
    return result.changes === 0 ? 0 : (result.lastInsertRowid as number);
  }

  getEmailById(id: number): Email | null {
    const row = this.db
      .prepare('SELECT * FROM emails WHERE id = ?')
      .get(id) as EmailRow | undefined;
    return row ? rowToEmail(row) : null;
  }

  listEmails(options: { limit?: number; offset?: number; folderId?: number; tag?: string } = {}): Email[] {
    const { limit = 50, offset = 0, folderId, tag } = options;
    let sql: string;
    let params: unknown[];

    if (tag) {
      sql = `
        SELECT e.* FROM emails e
        JOIN email_tags et ON et.email_id = e.id
        JOIN tags t ON t.id = et.tag_id
        WHERE t.name = ?
        ORDER BY e.date DESC, e.id DESC
        LIMIT ? OFFSET ?
      `;
      params = [tag, limit, offset];
    } else if (folderId !== undefined) {
      sql = `SELECT * FROM emails WHERE folder_id = ? ORDER BY date DESC, id DESC LIMIT ? OFFSET ?`;
      params = [folderId, limit, offset];
    } else {
      sql = `SELECT * FROM emails ORDER BY date DESC, id DESC LIMIT ? OFFSET ?`;
      params = [limit, offset];
    }

    const rows = this.db.prepare(sql).all(...params) as EmailRow[];
    return rows.map(rowToEmail);
  }

  countEmails(): number {
    const row = this.db.prepare('SELECT COUNT(*) as cnt FROM emails').get() as { cnt: number };
    return row.cnt;
  }

  deleteEmail(id: number): boolean {
    const result = this.db.prepare('DELETE FROM emails WHERE id = ?').run(id);
    return result.changes > 0;
  }

  // ── Attachments ──────────────────────────────────────────────────────────

  insertAttachment(attachment: Attachment): number {
    const stmt = this.db.prepare(`
      INSERT INTO attachments (email_id, filename, mime_type, size, storage_path)
      VALUES (@emailId, @filename, @mimeType, @size, @storagePath)
    `);
    const result = stmt.run({
      emailId: attachment.emailId,
      filename: attachment.filename,
      mimeType: attachment.mimeType ?? null,
      size: attachment.size,
      storagePath: attachment.storagePath ?? null,
    });
    return result.lastInsertRowid as number;
  }

  getAttachmentsByEmailId(emailId: number): Attachment[] {
    const rows = this.db
      .prepare('SELECT * FROM attachments WHERE email_id = ?')
      .all(emailId) as AttachmentRow[];
    return rows.map(rowToAttachment);
  }

  // ── Tags ─────────────────────────────────────────────────────────────────

  upsertTag(name: string): number {
    const existing = this.db
      .prepare('SELECT id FROM tags WHERE name = ?')
      .get(name) as { id: number } | undefined;
    if (existing) return existing.id;

    const result = this.db
      .prepare('INSERT INTO tags (name, created_at) VALUES (?, ?)')
      .run(name, new Date().toISOString());
    return result.lastInsertRowid as number;
  }

  addTagToEmail(emailId: number, tagName: string): void {
    const tagId = this.upsertTag(tagName);
    this.db
      .prepare('INSERT OR IGNORE INTO email_tags (email_id, tag_id) VALUES (?, ?)')
      .run(emailId, tagId);
  }

  removeTagFromEmail(emailId: number, tagName: string): void {
    const tag = this.db
      .prepare('SELECT id FROM tags WHERE name = ?')
      .get(tagName) as { id: number } | undefined;
    if (!tag) return;
    this.db
      .prepare('DELETE FROM email_tags WHERE email_id = ? AND tag_id = ?')
      .run(emailId, tag.id);
  }

  getTagsForEmail(emailId: number): Tag[] {
    const rows = this.db
      .prepare(
        `SELECT t.* FROM tags t
         JOIN email_tags et ON et.tag_id = t.id
         WHERE et.email_id = ?`
      )
      .all(emailId) as TagRow[];
    return rows.map(rowToTag);
  }

  listTags(): Tag[] {
    const rows = this.db.prepare('SELECT * FROM tags ORDER BY name').all() as TagRow[];
    return rows.map(rowToTag);
  }

  // ── Full-text search ──────────────────────────────────────────────────────

  searchEmails(query: string, options: { limit?: number; offset?: number } = {}): EmailSearchResult[] {
    const { limit = 50, offset = 0 } = options;
    const rows = this.db
      .prepare(
        `SELECT e.*, snippet(emails_fts, -1, '[', ']', '…', 20) AS snippet,
                bm25(emails_fts) AS rank
         FROM emails_fts
         JOIN emails e ON e.id = emails_fts.rowid
         WHERE emails_fts MATCH ?
         ORDER BY rank
         LIMIT ? OFFSET ?`
      )
      .all(query, limit, offset) as (EmailRow & { snippet: string; rank: number })[];

    return rows.map((row) => ({
      ...rowToEmail(row),
      snippet: row.snippet,
      rank: row.rank,
    }));
  }
}
