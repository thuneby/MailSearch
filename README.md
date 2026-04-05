# MailSearch

A C# .NET 8 application for reading Outlook files (.msg, .pst, .ost), storing email metadata and content in a SQLite database with full-text search (FTS5), and organising emails with tags via a CLI.

## Features

- Import `.msg` Outlook message files
- Import `.pst`/`.ost` files (requires a third-party PST reader — see [PST support](#pst-support))
- SQLite storage with full-text search (FTS5)
- Tagging and filtering emails
- CLI commands: `import`, `search`, `list`, `show`, `tag`, `untag`, `tags`, `delete`

## Build & Run

```bash
dotnet build
dotnet run --project src/MailSearch -- --help
```

## Test

```bash
dotnet test
```

## Usage

```bash
# Import a .msg file
mailsearch import email.msg

# Search emails
mailsearch search "quarterly report"

# List emails
mailsearch list --limit 50

# Show details of email #3
mailsearch show 3

# Tag an email
mailsearch tag 3 important

# Remove a tag
mailsearch untag 3 important

# List all tags
mailsearch tags

# Delete an email
mailsearch delete 3
```

## PST support

PST/OST import requires a third-party reader library. To add support:

1. Add a NuGet package that can read PST files (e.g. [XstReader.Api](https://github.com/iluvadev/XstReader), Aspose.Email, or Independentsoft.Email.Mapi).
2. Implement the `IPstReader` interface from `MailSearch.Importer`.
3. Call `PstImporter.ImportPstAsync(filePath, repo, attachmentDir, yourReader)`.

## Project structure

```
src/MailSearch/
  Program.cs                  CLI entry point (System.CommandLine)
  Database/
    Repository.cs             MailSearchRepository (SQLite via Microsoft.Data.Sqlite)
    Schema.cs                 Schema initialisation & FTS5 triggers
  Importer/
    EmailNormalizer.cs        Address parsing & attachment file writing
    MsgImporter.cs            .msg import (MsgReader)
    PstImporter.cs            .pst/.ost import abstraction (IPstReader)
    ImportResult.cs           Import statistics
  Models/
    Email.cs, EmailAddress.cs, EmailSearchResult.cs, Attachment.cs, Tag.cs
  Organizer/
    Organizer.cs              Tagging operations
  Search/
    SearchService.cs          FTS5 search wrapper
tests/MailSearch.Tests/       xUnit test suite
```
