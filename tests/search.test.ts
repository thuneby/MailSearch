import os from 'os';
import path from 'path';
import fs from 'fs';
import { MailSearchRepository } from '../src/database/repository';
import { search } from '../src/search/search';
import { Email } from '../src/models/email';

function makeTempDb(): string {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'mailsearch-search-test-'));
  return path.join(dir, 'test.db');
}

function sampleEmail(overrides: Partial<Email> = {}): Email {
  return {
    subject: 'Test Email',
    from: { name: 'Sender', email: 'sender@example.com' },
    to: [{ email: 'recipient@example.com' }],
    cc: [],
    bcc: [],
    date: new Date(),
    bodyText: 'Generic email body',
    hasAttachments: false,
    attachmentCount: 0,
    sourceFile: '/test/file.msg',
    importedAt: new Date(),
    ...overrides,
  };
}

describe('search', () => {
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

  it('finds emails by subject word', () => {
    repo.insertEmail(sampleEmail({ subject: 'Project Update', bodyText: 'Latest news' }));
    repo.insertEmail(sampleEmail({ subject: 'Invoice Q3', bodyText: 'Finance stuff' }));

    const results = search(repo, { query: 'project' });
    expect(results).toHaveLength(1);
    expect(results[0].subject).toBe('Project Update');
  });

  it('finds emails by body word', () => {
    repo.insertEmail(sampleEmail({ subject: 'Hello', bodyText: 'Budget planning for next year' }));
    repo.insertEmail(sampleEmail({ subject: 'World', bodyText: 'Have a great day' }));

    const results = search(repo, { query: 'budget' });
    expect(results).toHaveLength(1);
    expect(results[0].subject).toBe('Hello');
  });

  it('returns empty array when nothing matches', () => {
    repo.insertEmail(sampleEmail({ subject: 'Hello' }));
    const results = search(repo, { query: 'xyzzy99999' });
    expect(results).toHaveLength(0);
  });

  it('respects the limit option', () => {
    for (let i = 0; i < 5; i++) {
      repo.insertEmail(sampleEmail({ subject: `Report ${i}`, bodyText: 'quarterly report content' }));
    }
    const results = search(repo, { query: 'quarterly', limit: 3 });
    expect(results).toHaveLength(3);
  });

  it('supports prefix search using FTS5 syntax', () => {
    repo.insertEmail(sampleEmail({ subject: 'Financial Report', bodyText: 'finances look good' }));
    const results = search(repo, { query: 'financ*' });
    expect(results.length).toBeGreaterThanOrEqual(1);
  });
});
