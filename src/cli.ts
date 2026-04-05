#!/usr/bin/env node
import path from 'path';
import os from 'os';
import { Command } from 'commander';
import chalk from 'chalk';
import { MailSearchRepository, DEFAULT_DB_PATH } from './database/repository';
import { importPst } from './importer/pst-importer';
import { importMsg } from './importer/msg-importer';
import { search } from './search/search';
import { Organizer } from './organizer/organizer';
import { Email } from './models/email';

const DEFAULT_ATTACHMENT_DIR = path.join(os.homedir(), '.mailsearch', 'attachments');

const program = new Command();

program
  .name('mailsearch')
  .description('Read Outlook files, store metadata/content/attachments, and search emails')
  .version('1.0.0');

// ── import ────────────────────────────────────────────────────────────────────

program
  .command('import <file>')
  .description('Import a .pst or .msg file into the database')
  .option('-d, --db <path>', 'Path to SQLite database', DEFAULT_DB_PATH)
  .option('-a, --attachments <dir>', 'Directory to store attachments', DEFAULT_ATTACHMENT_DIR)
  .action(async (file: string, options: { db: string; attachments: string }) => {
    const repo = new MailSearchRepository(options.db);
    const ext = path.extname(file).toLowerCase();

    console.log(chalk.blue(`Importing ${file}…`));

    try {
      if (ext === '.pst' || ext === '.ost') {
        const result = await importPst(file, repo, options.attachments, (n) => {
          process.stdout.write(`\r  Emails imported: ${n}`);
        });
        process.stdout.write('\n');
        console.log(chalk.green(`✔ Imported: ${result.imported}  Skipped: ${result.skipped}  Errors: ${result.errors}`));
      } else if (ext === '.msg') {
        const result = importMsg(file, repo, options.attachments);
        if (result.imported > 0) {
          console.log(chalk.green(`✔ Email imported successfully`));
        } else if (result.skipped > 0) {
          console.log(chalk.yellow(`⚠ Email was already imported (duplicate)`));
        } else {
          console.log(chalk.red(`✖ Import failed`));
        }
      } else {
        console.error(chalk.red(`Unsupported file type: ${ext}. Supported types: .pst, .ost, .msg`));
        process.exit(1);
      }
    } finally {
      repo.close();
    }
  });

// ── search ────────────────────────────────────────────────────────────────────

program
  .command('search <query>')
  .description('Search emails using full-text search (supports FTS5 syntax)')
  .option('-d, --db <path>', 'Path to SQLite database', DEFAULT_DB_PATH)
  .option('-n, --limit <n>', 'Maximum results to return', '20')
  .option('--offset <n>', 'Result offset for pagination', '0')
  .action((query: string, options: { db: string; limit: string; offset: string }) => {
    const repo = new MailSearchRepository(options.db);
    try {
      const results = search(repo, {
        query,
        limit: parseInt(options.limit, 10),
        offset: parseInt(options.offset, 10),
      });

      if (results.length === 0) {
        console.log(chalk.yellow('No results found.'));
        return;
      }

      console.log(chalk.blue(`Found ${results.length} result(s) for "${query}":\n`));
      for (const email of results) {
        printEmailSummary(email);
        if (email.snippet) {
          console.log(`  ${chalk.gray('…' + email.snippet + '…')}`);
        }
        console.log();
      }
    } finally {
      repo.close();
    }
  });

// ── list ──────────────────────────────────────────────────────────────────────

program
  .command('list')
  .description('List imported emails')
  .option('-d, --db <path>', 'Path to SQLite database', DEFAULT_DB_PATH)
  .option('-n, --limit <n>', 'Maximum results to return', '20')
  .option('--offset <n>', 'Result offset for pagination', '0')
  .option('-t, --tag <name>', 'Filter by tag')
  .action((options: { db: string; limit: string; offset: string; tag?: string }) => {
    const repo = new MailSearchRepository(options.db);
    try {
      const emails = repo.listEmails({
        limit: parseInt(options.limit, 10),
        offset: parseInt(options.offset, 10),
        tag: options.tag,
      });
      const total = repo.countEmails();

      if (emails.length === 0) {
        console.log(chalk.yellow('No emails found.'));
        return;
      }

      console.log(chalk.blue(`Showing ${emails.length} of ${total} email(s):\n`));
      for (const email of emails) {
        printEmailSummary(email);
        console.log();
      }
    } finally {
      repo.close();
    }
  });

// ── show ──────────────────────────────────────────────────────────────────────

program
  .command('show <id>')
  .description('Show full details of an email')
  .option('-d, --db <path>', 'Path to SQLite database', DEFAULT_DB_PATH)
  .action((id: string, options: { db: string }) => {
    const repo = new MailSearchRepository(options.db);
    try {
      const email = repo.getEmailById(parseInt(id, 10));
      if (!email) {
        console.error(chalk.red(`Email #${id} not found`));
        process.exit(1);
      }

      console.log(chalk.bold(`Subject: `) + email.subject);
      console.log(chalk.bold(`From:    `) + formatAddress(email.from));
      console.log(chalk.bold(`To:      `) + email.to.map(formatAddress).join(', '));
      if (email.cc.length) console.log(chalk.bold(`CC:      `) + email.cc.map(formatAddress).join(', '));
      if (email.bcc.length) console.log(chalk.bold(`BCC:     `) + email.bcc.map(formatAddress).join(', '));
      if (email.date) console.log(chalk.bold(`Date:    `) + email.date.toLocaleString());
      if (email.folderPath) console.log(chalk.bold(`Folder:  `) + email.folderPath);

      const tags = repo.getTagsForEmail(email.id!);
      if (tags.length) {
        console.log(chalk.bold(`Tags:    `) + tags.map((t) => chalk.cyan(t.name)).join(', '));
      }

      const attachments = repo.getAttachmentsByEmailId(email.id!);
      if (attachments.length) {
        console.log(chalk.bold(`\nAttachments (${attachments.length}):`));
        for (const att of attachments) {
          console.log(`  [${att.id}] ${att.filename} (${formatSize(att.size)})`);
          if (att.storagePath) console.log(`       → ${att.storagePath}`);
        }
      }

      if (email.bodyText) {
        console.log(chalk.bold(`\nBody:\n`) + email.bodyText.slice(0, 2000));
        if (email.bodyText.length > 2000) console.log(chalk.gray('… (truncated)'));
      }
    } finally {
      repo.close();
    }
  });

// ── tag ───────────────────────────────────────────────────────────────────────

program
  .command('tag <id> <tag>')
  .description('Add a tag to an email')
  .option('-d, --db <path>', 'Path to SQLite database', DEFAULT_DB_PATH)
  .action((id: string, tag: string, options: { db: string }) => {
    const repo = new MailSearchRepository(options.db);
    try {
      const organizer = new Organizer(repo);
      organizer.tag(parseInt(id, 10), tag);
      console.log(chalk.green(`✔ Tag "${tag}" added to email #${id}`));
    } finally {
      repo.close();
    }
  });

// ── untag ─────────────────────────────────────────────────────────────────────

program
  .command('untag <id> <tag>')
  .description('Remove a tag from an email')
  .option('-d, --db <path>', 'Path to SQLite database', DEFAULT_DB_PATH)
  .action((id: string, tag: string, options: { db: string }) => {
    const repo = new MailSearchRepository(options.db);
    try {
      const organizer = new Organizer(repo);
      organizer.untag(parseInt(id, 10), tag);
      console.log(chalk.green(`✔ Tag "${tag}" removed from email #${id}`));
    } finally {
      repo.close();
    }
  });

// ── tags ──────────────────────────────────────────────────────────────────────

program
  .command('tags')
  .description('List all tags')
  .option('-d, --db <path>', 'Path to SQLite database', DEFAULT_DB_PATH)
  .action((options: { db: string }) => {
    const repo = new MailSearchRepository(options.db);
    try {
      const tags = repo.listTags();
      if (tags.length === 0) {
        console.log(chalk.yellow('No tags defined yet.'));
        return;
      }
      console.log(chalk.blue(`Tags (${tags.length}):`));
      for (const tag of tags) {
        console.log(`  ${chalk.cyan(tag.name)}`);
      }
    } finally {
      repo.close();
    }
  });

// ── delete ────────────────────────────────────────────────────────────────────

program
  .command('delete <id>')
  .description('Delete an email from the database')
  .option('-d, --db <path>', 'Path to SQLite database', DEFAULT_DB_PATH)
  .action((id: string, options: { db: string }) => {
    const repo = new MailSearchRepository(options.db);
    try {
      const deleted = repo.deleteEmail(parseInt(id, 10));
      if (deleted) {
        console.log(chalk.green(`✔ Email #${id} deleted`));
      } else {
        console.error(chalk.red(`Email #${id} not found`));
        process.exit(1);
      }
    } finally {
      repo.close();
    }
  });

// ── Helpers ───────────────────────────────────────────────────────────────────

function printEmailSummary(email: Email & { snippet?: string }): void {
  const dateStr = email.date ? email.date.toLocaleDateString() : 'unknown date';
  const fromStr = formatAddress(email.from);
  const clip = (s: string, n: number) => s.length > n ? s.slice(0, n) + '…' : s;
  console.log(
    `${chalk.gray(`#${email.id}`)}  ${chalk.bold(clip(email.subject, 60))}` +
    `\n  ${chalk.cyan(fromStr)}  ${chalk.gray(dateStr)}` +
    (email.hasAttachments ? `  ${chalk.yellow(`📎 ${email.attachmentCount}`)}` : '')
  );
}

function formatAddress(addr: { name?: string; email: string }): string {
  return addr.name ? `${addr.name} <${addr.email}>` : addr.email;
}

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

program.parse(process.argv);
