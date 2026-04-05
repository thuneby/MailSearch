import os from 'os';
import path from 'path';
import fs from 'fs';
import { MailSearchRepository } from '../src/database/repository';
import { Email } from '../src/models/email';
import { Attachment } from '../src/models/attachment';

function makeTempDb(): string {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'mailsearch-test-'));
  return path.join(dir, 'test.db');
}

function sampleEmail(overrides: Partial<Email> = {}): Email {
  return {
    subject: 'Hello World',
    from: { name: 'Alice', email: 'alice@example.com' },
    to: [{ email: 'bob@example.com' }],
    cc: [],
    bcc: [],
    date: new Date('2024-01-15T10:00:00Z'),
    bodyText: 'This is the email body text about quarterly results.',
    hasAttachments: false,
    attachmentCount: 0,
    sourceFile: '/path/to/file.pst',
    importedAt: new Date(),
    ...overrides,
  };
}

describe('MailSearchRepository', () => {
  let repo: MailSearchRepository;
  let dbPath: string;

  beforeEach(() => {
    dbPath = makeTempDb();
    repo = new MailSearchRepository(dbPath);
  });

  afterEach(() => {
    repo.close();
    fs.rmSync(path.dirname(dbPath), { recursive: true, force: true });
  });

  describe('email CRUD', () => {
    it('inserts an email and retrieves it by id', () => {
      const id = repo.insertEmail(sampleEmail());
      expect(id).toBeGreaterThan(0);

      const retrieved = repo.getEmailById(id);
      expect(retrieved).not.toBeNull();
      expect(retrieved!.subject).toBe('Hello World');
      expect(retrieved!.from.name).toBe('Alice');
      expect(retrieved!.from.email).toBe('alice@example.com');
      expect(retrieved!.to).toEqual([{ email: 'bob@example.com' }]);
    });

    it('returns null for a non-existent email id', () => {
      const result = repo.getEmailById(9999);
      expect(result).toBeNull();
    });

    it('ignores duplicate message_id on insert', () => {
      const email = sampleEmail({ messageId: 'unique-id-123@example.com' });
      const id1 = repo.insertEmail(email);
      const id2 = repo.insertEmail(email); // duplicate
      expect(id1).toBeGreaterThan(0);
      expect(id2).toBe(0); // INSERT OR IGNORE returns 0 changes
    });

    it('lists emails ordered by date descending', () => {
      repo.insertEmail(sampleEmail({ subject: 'Older', date: new Date('2024-01-01') }));
      repo.insertEmail(sampleEmail({ subject: 'Newer', date: new Date('2024-06-01') }));

      const list = repo.listEmails();
      expect(list[0].subject).toBe('Newer');
      expect(list[1].subject).toBe('Older');
    });

    it('counts total emails', () => {
      repo.insertEmail(sampleEmail());
      repo.insertEmail(sampleEmail({ subject: 'Second' }));
      expect(repo.countEmails()).toBe(2);
    });

    it('deletes an email', () => {
      const id = repo.insertEmail(sampleEmail());
      expect(repo.deleteEmail(id)).toBe(true);
      expect(repo.getEmailById(id)).toBeNull();
    });

    it('returns false when deleting a non-existent email', () => {
      expect(repo.deleteEmail(9999)).toBe(false);
    });
  });

  describe('attachments', () => {
    it('inserts and retrieves attachments for an email', () => {
      const emailId = repo.insertEmail(sampleEmail({ hasAttachments: true, attachmentCount: 1 }));
      const attachment: Attachment = {
        emailId,
        filename: 'report.pdf',
        mimeType: 'application/pdf',
        size: 1024,
        storagePath: '/tmp/report.pdf',
      };

      const attId = repo.insertAttachment(attachment);
      expect(attId).toBeGreaterThan(0);

      const attachments = repo.getAttachmentsByEmailId(emailId);
      expect(attachments).toHaveLength(1);
      expect(attachments[0].filename).toBe('report.pdf');
      expect(attachments[0].mimeType).toBe('application/pdf');
      expect(attachments[0].size).toBe(1024);
    });

    it('returns empty array when email has no attachments', () => {
      const emailId = repo.insertEmail(sampleEmail());
      expect(repo.getAttachmentsByEmailId(emailId)).toEqual([]);
    });

    it('cascades delete to attachments when email is deleted', () => {
      const emailId = repo.insertEmail(sampleEmail());
      repo.insertAttachment({ emailId, filename: 'doc.txt', size: 100 });

      repo.deleteEmail(emailId);
      expect(repo.getAttachmentsByEmailId(emailId)).toEqual([]);
    });
  });

  describe('tags', () => {
    it('adds and retrieves tags for an email', () => {
      const emailId = repo.insertEmail(sampleEmail());
      repo.addTagToEmail(emailId, 'important');
      repo.addTagToEmail(emailId, 'work');

      const tags = repo.getTagsForEmail(emailId);
      const names = tags.map((t) => t.name).sort();
      expect(names).toEqual(['important', 'work']);
    });

    it('does not duplicate tags on repeated add', () => {
      const emailId = repo.insertEmail(sampleEmail());
      repo.addTagToEmail(emailId, 'important');
      repo.addTagToEmail(emailId, 'important');

      expect(repo.getTagsForEmail(emailId)).toHaveLength(1);
    });

    it('removes a tag from an email', () => {
      const emailId = repo.insertEmail(sampleEmail());
      repo.addTagToEmail(emailId, 'work');
      repo.removeTagFromEmail(emailId, 'work');

      expect(repo.getTagsForEmail(emailId)).toHaveLength(0);
    });

    it('lists all tags', () => {
      const emailId = repo.insertEmail(sampleEmail());
      repo.addTagToEmail(emailId, 'alpha');
      repo.addTagToEmail(emailId, 'beta');

      const all = repo.listTags();
      expect(all.map((t) => t.name)).toEqual(['alpha', 'beta']);
    });

    it('filters emails by tag', () => {
      const id1 = repo.insertEmail(sampleEmail({ subject: 'Tagged' }));
      const id2 = repo.insertEmail(sampleEmail({ subject: 'Not tagged' }));
      repo.addTagToEmail(id1, 'review');

      const emails = repo.listEmails({ tag: 'review' });
      expect(emails).toHaveLength(1);
      expect(emails[0].id).toBe(id1);

      void id2; // suppress unused variable warning
    });
  });

  describe('full-text search', () => {
    beforeEach(() => {
      repo.insertEmail(sampleEmail({
        subject: 'Quarterly results',
        bodyText: 'Revenue increased by 15% this quarter.',
      }));
      repo.insertEmail(sampleEmail({
        subject: 'Team meeting tomorrow',
        bodyText: 'Please join the meeting at 10am.',
      }));
      repo.insertEmail(sampleEmail({
        subject: 'Invoice #1234',
        bodyText: 'Attached is the invoice for services rendered.',
      }));
    });

    it('finds emails matching a keyword', () => {
      const results = repo.searchEmails('meeting');
      expect(results).toHaveLength(1);
      expect(results[0].subject).toBe('Team meeting tomorrow');
    });

    it('finds emails by subject keyword', () => {
      const results = repo.searchEmails('invoice');
      expect(results).toHaveLength(1);
      expect(results[0].subject).toBe('Invoice #1234');
    });

    it('finds emails by body keyword', () => {
      const results = repo.searchEmails('quarter');
      expect(results.length).toBeGreaterThanOrEqual(1);
      expect(results[0].subject).toBe('Quarterly results');
    });

    it('returns empty array for no matches', () => {
      const results = repo.searchEmails('nonexistentterm12345');
      expect(results).toHaveLength(0);
    });

    it('includes snippet in results', () => {
      const results = repo.searchEmails('meeting');
      expect(results[0].snippet).toBeDefined();
      expect(results[0].snippet).toContain('[meeting]');
    });

    it('respects limit and offset', () => {
      const all = repo.searchEmails('the');
      const limited = repo.searchEmails('the', { limit: 1 });
      expect(limited).toHaveLength(1);

      if (all.length > 1) {
        const offset = repo.searchEmails('the', { limit: 1, offset: 1 });
        expect(offset[0].id).not.toBe(limited[0].id);
      }
    });
  });
});
