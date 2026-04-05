import os from 'os';
import path from 'path';
import fs from 'fs';
import { MailSearchRepository } from '../src/database/repository';
import { Organizer } from '../src/organizer/organizer';
import { Email } from '../src/models/email';

function makeTempDb(): string {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'mailsearch-org-test-'));
  return path.join(dir, 'test.db');
}

function sampleEmail(overrides: Partial<Email> = {}): Email {
  return {
    subject: 'Test Email',
    from: { email: 'test@example.com' },
    to: [],
    cc: [],
    bcc: [],
    bodyText: 'Test body',
    hasAttachments: false,
    attachmentCount: 0,
    sourceFile: '/test/file.msg',
    importedAt: new Date(),
    ...overrides,
  };
}

describe('Organizer', () => {
  let repo: MailSearchRepository;
  let organizer: Organizer;
  let dbPath: string;

  beforeEach(() => {
    dbPath = makeTempDb();
    repo = new MailSearchRepository(dbPath);
    organizer = new Organizer(repo);
  });

  afterEach(() => {
    repo.close();
    fs.rmSync(path.dirname(dbPath), { recursive: true, force: true });
  });

  it('adds multiple tags to an email at once', () => {
    const emailId = repo.insertEmail(sampleEmail());
    organizer.tag(emailId, 'urgent', 'follow-up');

    const tags = organizer.getTagsForEmail(emailId);
    expect(tags.map((t) => t.name).sort()).toEqual(['follow-up', 'urgent']);
  });

  it('removes a tag from an email', () => {
    const emailId = repo.insertEmail(sampleEmail());
    organizer.tag(emailId, 'temp');
    organizer.untag(emailId, 'temp');

    expect(organizer.getTagsForEmail(emailId)).toHaveLength(0);
  });

  it('lists all tags across emails', () => {
    const id1 = repo.insertEmail(sampleEmail({ subject: 'Email 1' }));
    const id2 = repo.insertEmail(sampleEmail({ subject: 'Email 2' }));
    organizer.tag(id1, 'alpha');
    organizer.tag(id2, 'beta');

    const all = organizer.listTags();
    expect(all.map((t) => t.name)).toEqual(['alpha', 'beta']);
  });

  it('lists emails by tag', () => {
    const id1 = repo.insertEmail(sampleEmail({ subject: 'Important Email' }));
    repo.insertEmail(sampleEmail({ subject: 'Regular Email' }));
    organizer.tag(id1, 'important');

    const emails = organizer.listEmailsByTag('important');
    expect(emails).toHaveLength(1);
    expect(emails[0].subject).toBe('Important Email');
  });

  it('returns empty list for non-existent tag', () => {
    expect(organizer.listEmailsByTag('nonexistent')).toEqual([]);
  });
});
