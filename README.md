# MailSearch

An independent application for reading Outlook files (`.pst`, `.ost`, `.msg`), storing email metadata, content, and attachments in a local SQLite database, and providing fast full-text search and organization capabilities.

## Features

- **Import** Outlook PST/OST archives and individual MSG files
- **Store** email metadata (subject, sender, recipients, date, folder path), body text, HTML body, and attachments
- **Search** emails with full-text search powered by SQLite FTS5 — supports keywords, phrases, prefix wildcards, and boolean operators
- **Organize** emails with custom tags
- **CLI interface** — all functionality is accessible from the command line
- **No Outlook required** — runs entirely independently

## Requirements

- Node.js ≥ 18
- npm ≥ 8

## Installation

```bash
# Clone the repository
git clone https://github.com/thuneby/MailSearch.git
cd MailSearch

# Install dependencies
npm install

# Build
npm run build

# (Optional) Install globally
npm install -g .
```

## Usage

All commands accept a `--db <path>` option to specify a custom database location.  
Default: `~/.mailsearch/mailsearch.db`

Attachments are extracted to `~/.mailsearch/attachments/` by default (override with `--attachments <dir>`).

### Import an Outlook PST/OST archive

```bash
mailsearch import /path/to/archive.pst
mailsearch import /path/to/archive.ost --attachments /data/attachments
```

### Import a single MSG file

```bash
mailsearch import /path/to/email.msg
```

### Search emails

Full-text search supports [SQLite FTS5](https://www.sqlite.org/fts5.html) syntax:

```bash
# Simple keyword
mailsearch search "budget"

# Phrase search
mailsearch search '"quarterly results"'

# Boolean AND
mailsearch search "meeting AND Q3"

# Prefix wildcard
mailsearch search "financ*"

# Limit / pagination
mailsearch search "invoice" --limit 10 --offset 20
```

### List emails

```bash
# All emails (most recent first)
mailsearch list

# Filter by tag
mailsearch list --tag important

# Pagination
mailsearch list --limit 50 --offset 100
```

### Show email details

```bash
mailsearch show 42
```

Displays subject, sender, recipients, date, folder path, tags, attachments (with saved paths), and body text.

### Tag an email

```bash
# Add a tag
mailsearch tag 42 important

# Remove a tag
mailsearch untag 42 important

# List all tags
mailsearch tags
```

### Delete an email

```bash
mailsearch delete 42
```

## Development

```bash
# Run in development mode (no build step needed)
npm run dev -- import /path/to/archive.pst

# Run tests
npm test

# Run tests with coverage
npm run test:coverage

# Lint
npm run lint
npm run lint:fix

# Build
npm run build
```

## Architecture

```
src/
  cli.ts                    CLI entry point (Commander.js)
  index.ts                  Public API exports
  models/
    email.ts                Email type definitions and row mapper
    attachment.ts           Attachment type definitions and row mapper
    tag.ts                  Tag type definitions and row mapper
  database/
    schema.ts               SQLite schema with FTS5 virtual table and update triggers
    repository.ts           CRUD operations, full-text search, tag management
  importer/
    email-normalizer.ts     Shared address parsing and attachment file writing
    pst-importer.ts         PST/OST file importer (pst-extractor)
    msg-importer.ts         MSG file importer (@kenjiuno/msgreader)
  search/
    search.ts               Full-text search wrapper
  organizer/
    organizer.ts            Tag-based email organization

tests/
  repository.test.ts        Database CRUD, tag, and FTS tests
  search.test.ts            Search module tests
  organizer.test.ts         Organizer/tag tests
  email-normalizer.test.ts  Address parsing tests
```

## Database

The SQLite database (`~/.mailsearch/mailsearch.db`) contains:

| Table | Contents |
|---|---|
| `emails` | Email metadata and body text |
| `attachments` | Attachment metadata and saved file paths |
| `tags` | Available tags |
| `email_tags` | Many-to-many email↔tag relationships |
| `emails_fts` | FTS5 virtual table for full-text search |

## License

MIT
