using System.CommandLine;
using MailSearch.Core.Database;
using MailSearch.Core.Importer;
using MailSearch.Core.Models;
using MailSearch.Core.Search;
using MailSearch.Core.Organizer;

var defaultDbPath = MailSearchRepository.DefaultDbPath;
var defaultAttachmentDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".mailsearch", "attachments");

var rootCommand = new RootCommand("Read Outlook files, store metadata/content/attachments, and search emails");

// ── import ────────────────────────────────────────────────────────────────────

var importCommand = new Command("import", "Import a .pst or .msg file into the database");

var importFileArg = new Argument<FileInfo>("file") { Description = "Path to the .pst or .msg file" };
var importDbOpt = new Option<string>("--db", new[] { "-d" })
    { Description = "Path to SQLite database", DefaultValueFactory = _ => defaultDbPath };
var importAttOpt = new Option<string>("--attachments", new[] { "-a" })
    { Description = "Directory to store attachments", DefaultValueFactory = _ => defaultAttachmentDir };
importCommand.Add(importFileArg);
importCommand.Add(importDbOpt);
importCommand.Add(importAttOpt);
importCommand.SetAction(async (ParseResult pr) =>
{
    var file = pr.GetValue(importFileArg)!;
    var db = pr.GetValue(importDbOpt)!;
    var att = pr.GetValue(importAttOpt)!;
    var repo = new MailSearchRepository(db);
    var ext = file.Extension.ToLowerInvariant();

    Console.ForegroundColor = ConsoleColor.Blue;
    Console.WriteLine($"Importing {file.FullName}…");
    Console.ResetColor();

    try
    {
        if (ext == ".msg")
        {
            var result = MsgImporter.ImportMsg(file.FullName, repo, att);
            if (result.Imported > 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✔ Email imported successfully");
            }
            else if (result.Skipped > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠ Email was already imported (duplicate)");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✖ Import failed");
            }
            Console.ResetColor();
        }
        else if (ext == ".pst" || ext == ".ost")
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("PST/OST import requires a third-party PST reader library.");
            Console.WriteLine("Implement IPstReader from MailSearch.Importer and call PstImporter.ImportPstAsync.");
            Console.ResetColor();
            return 1;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Unsupported file type: {ext}. Supported types: .msg, .pst, .ost");
            Console.ResetColor();
            return 1;
        }
    }
    finally
    {
        repo.Close();
    }
    return 0;
});

// ── search ────────────────────────────────────────────────────────────────────

var searchCommand = new Command("search", "Search emails using full-text search (supports FTS5 syntax)");

var searchQueryArg = new Argument<string>("query") { Description = "Search query" };
var searchDbOpt = new Option<string>("--db", new[] { "-d" })
    { Description = "Path to SQLite database", DefaultValueFactory = _ => defaultDbPath };
var searchLimitOpt = new Option<int>("--limit", new[] { "-n" })
    { Description = "Maximum results to return", DefaultValueFactory = _ => 20 };
var searchOffsetOpt = new Option<int>("--offset")
    { Description = "Result offset for pagination", DefaultValueFactory = _ => 0 };
searchCommand.Add(searchQueryArg);
searchCommand.Add(searchDbOpt);
searchCommand.Add(searchLimitOpt);
searchCommand.Add(searchOffsetOpt);
searchCommand.SetAction((ParseResult pr) =>
{
    var query = pr.GetValue(searchQueryArg)!;
    var repo = new MailSearchRepository(pr.GetValue(searchDbOpt)!);
    try
    {
        var results = SearchService.Search(repo, new SearchOptions
        {
            Query = query,
            Limit = pr.GetValue(searchLimitOpt),
            Offset = pr.GetValue(searchOffsetOpt),
        });

        if (results.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("No results found.");
            Console.ResetColor();
            return 0;
        }
        var count = results?.Count ?? 0;

        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine($"Found {count} result(s) for \"{query}\":\n");
        Console.ResetColor();

        foreach (var email in results)
        {
            PrintEmailSummary(email);
            if (!string.IsNullOrEmpty(email.Snippet))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  …{email.Snippet?? ""}…");
                Console.ResetColor();
            }
            Console.WriteLine();
        }
    }
    finally
    {
        repo.Close();
    }
    return 0;
});

// ── list ──────────────────────────────────────────────────────────────────────

var listCommand = new Command("list", "List imported emails");

var listDbOpt = new Option<string>("--db", new[] { "-d" })
    { Description = "Path to SQLite database", DefaultValueFactory = _ => defaultDbPath };
var listLimitOpt = new Option<int>("--limit", new[] { "-n" })
    { Description = "Maximum results to return", DefaultValueFactory = _ => 20 };
var listOffsetOpt = new Option<int>("--offset")
    { Description = "Result offset for pagination", DefaultValueFactory = _ => 0 };
var listTagOpt = new Option<string?>("--tag", new[] { "-t" })
    { Description = "Filter by tag" };
listCommand.Add(listDbOpt);
listCommand.Add(listLimitOpt);
listCommand.Add(listOffsetOpt);
listCommand.Add(listTagOpt);
listCommand.SetAction((ParseResult pr) =>
{
    var repo = new MailSearchRepository(pr.GetValue(listDbOpt)!);
    try
    {
        var emails = repo.ListEmails(
            pr.GetValue(listLimitOpt),
            pr.GetValue(listOffsetOpt),
            tag: pr.GetValue(listTagOpt));
        var total = repo.CountEmails();

        if (emails.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("No emails found.");
            Console.ResetColor();
            return 0;
        }

        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine($"Showing {emails.Count} of {total} email(s):\n");
        Console.ResetColor();

        foreach (var email in emails)
        {
            PrintEmailSummary(email);
            Console.WriteLine();
        }
    }
    finally
    {
        repo.Close();
    }
    return 0;
});

// ── show ──────────────────────────────────────────────────────────────────────

var showCommand = new Command("show", "Show full details of an email");

var showIdArg = new Argument<int>("id") { Description = "Email ID" };
var showDbOpt = new Option<string>("--db", new[] { "-d" })
    { Description = "Path to SQLite database", DefaultValueFactory = _ => defaultDbPath };
showCommand.Add(showIdArg);
showCommand.Add(showDbOpt);
showCommand.SetAction((ParseResult pr) =>
{
    var repo = new MailSearchRepository(pr.GetValue(showDbOpt)!);
    try
    {
        var id = pr.GetValue(showIdArg);
        var email = repo.GetEmailById(id);
        if (email == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Email #{id} not found");
            Console.ResetColor();
            return 1;
        }

        PrintBold("Subject: "); Console.WriteLine(email.Subject);
        PrintBold("From:    "); Console.WriteLine(FormatAddress(email.From));
        PrintBold("To:      "); Console.WriteLine(string.Join(", ", email.To.Select(FormatAddress)));
        if (email.Cc.Count > 0) { PrintBold("CC:      "); Console.WriteLine(string.Join(", ", email.Cc.Select(FormatAddress))); }
        if (email.Bcc.Count > 0) { PrintBold("BCC:     "); Console.WriteLine(string.Join(", ", email.Bcc.Select(FormatAddress))); }
        if (email.Date.HasValue) { PrintBold("Date:    "); Console.WriteLine(email.Date.Value.ToLocalTime()); }
        if (!string.IsNullOrEmpty(email.FolderPath)) { PrintBold("Folder:  "); Console.WriteLine(email.FolderPath); }

        var tags = repo.GetTagsForEmail(email.Id!.Value);
        if (tags.Count > 0)
        {
            PrintBold("Tags:    ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(string.Join(", ", tags.Select(t => t.Name)));
            Console.ResetColor();
        }

        var attachments = repo.GetAttachmentsByEmailId(email.Id!.Value);
        if (attachments.Count > 0)
        {
            Console.WriteLine();
            PrintBold($"Attachments ({attachments.Count}):");
            Console.WriteLine();
            foreach (var a in attachments)
            {
                Console.WriteLine($"  [{a.Id}] {a.Filename} ({FormatSize(a.Size)})");
                if (!string.IsNullOrEmpty(a.StoragePath))
                    Console.WriteLine($"       → {a.StoragePath}");
            }
        }

        if (!string.IsNullOrEmpty(email.BodyText))
        {
            Console.WriteLine();
            PrintBold("Body:\n");
            var text = email.BodyText.Length > 2000 ? email.BodyText[..2000] : email.BodyText;
            Console.WriteLine(text);
            if (email.BodyText.Length > 2000)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("… (truncated)");
                Console.ResetColor();
            }
        }
    }
    finally
    {
        repo.Close();
    }
    return 0;
});

// ── tag ───────────────────────────────────────────────────────────────────────

var tagCommand = new Command("tag", "Add a tag to an email");

var tagIdArg = new Argument<int>("id") { Description = "Email ID" };
var tagNameArg = new Argument<string>("tag") { Description = "Tag name" };
var tagDbOpt = new Option<string>("--db", new[] { "-d" })
    { Description = "Path to SQLite database", DefaultValueFactory = _ => defaultDbPath };
tagCommand.Add(tagIdArg);
tagCommand.Add(tagNameArg);
tagCommand.Add(tagDbOpt);
tagCommand.SetAction((ParseResult pr) =>
{
    var repo = new MailSearchRepository(pr.GetValue(tagDbOpt)!);
    try
    {
        var organizer = new Organizer(repo);
        var tagName = pr.GetValue(tagNameArg)!;
        organizer.Tag(pr.GetValue(tagIdArg), tagName);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✔ Tag \"{tagName}\" added to email #{pr.GetValue(tagIdArg)}");
        Console.ResetColor();
    }
    finally
    {
        repo.Close();
    }
    return 0;
});

// ── untag ─────────────────────────────────────────────────────────────────────

var untagCommand = new Command("untag", "Remove a tag from an email");

var untagIdArg = new Argument<int>("id") { Description = "Email ID" };
var untagNameArg = new Argument<string>("tag") { Description = "Tag name" };
var untagDbOpt = new Option<string>("--db", new[] { "-d" })
    { Description = "Path to SQLite database", DefaultValueFactory = _ => defaultDbPath };
untagCommand.Add(untagIdArg);
untagCommand.Add(untagNameArg);
untagCommand.Add(untagDbOpt);
untagCommand.SetAction((ParseResult pr) =>
{
    var repo = new MailSearchRepository(pr.GetValue(untagDbOpt)!);
    try
    {
        var organizer = new Organizer(repo);
        var tagName = pr.GetValue(untagNameArg)!;
        organizer.Untag(pr.GetValue(untagIdArg), tagName);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✔ Tag \"{tagName}\" removed from email #{pr.GetValue(untagIdArg)}");
        Console.ResetColor();
    }
    finally
    {
        repo.Close();
    }
    return 0;
});

// ── tags ──────────────────────────────────────────────────────────────────────

var tagsCommand = new Command("tags", "List all tags");

var tagsDbOpt = new Option<string>("--db", new[] { "-d" })
    { Description = "Path to SQLite database", DefaultValueFactory = _ => defaultDbPath };
tagsCommand.Add(tagsDbOpt);
tagsCommand.SetAction((ParseResult pr) =>
{
    var repo = new MailSearchRepository(pr.GetValue(tagsDbOpt)!);
    try
    {
        var tags = repo.ListTags();
        if (tags.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("No tags defined yet.");
            Console.ResetColor();
            return 0;
        }
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine($"Tags ({tags.Count}):");
        Console.ResetColor();
        foreach (var tag in tags)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  {tag.Name}");
            Console.ResetColor();
        }
    }
    finally
    {
        repo.Close();
    }
    return 0;
});

// ── delete ────────────────────────────────────────────────────────────────────

var deleteCommand = new Command("delete", "Delete an email from the database");

var deleteIdArg = new Argument<int>("id") { Description = "Email ID" };
var deleteDbOpt = new Option<string>("--db", new[] { "-d" })
    { Description = "Path to SQLite database", DefaultValueFactory = _ => defaultDbPath };
deleteCommand.Add(deleteIdArg);
deleteCommand.Add(deleteDbOpt);
deleteCommand.SetAction((ParseResult pr) =>
{
    var repo = new MailSearchRepository(pr.GetValue(deleteDbOpt)!);
    try
    {
        var id = pr.GetValue(deleteIdArg);
        if (repo.DeleteEmail(id))
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✔ Email #{id} deleted");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Email #{id} not found");
            Console.ResetColor();
            return 1;
        }
    }
    finally
    {
        repo.Close();
    }
    return 0;
});

rootCommand.Add(importCommand);
rootCommand.Add(searchCommand);
rootCommand.Add(listCommand);
rootCommand.Add(showCommand);
rootCommand.Add(tagCommand);
rootCommand.Add(untagCommand);
rootCommand.Add(tagsCommand);
rootCommand.Add(deleteCommand);

return await rootCommand.Parse(args).InvokeAsync();

// ── Helpers ───────────────────────────────────────────────────────────────────

static void PrintEmailSummary(Email email)
{
    var dateStr = email.Date.HasValue ? email.Date.Value.ToLocalTime().ToShortDateString() : "unknown date";
    var fromStr = FormatAddress(email.From);
    var subject = email.Subject.Length > 60 ? email.Subject[..60] + "…" : email.Subject;

    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write($"#{email.Id}  ");
    Console.ResetColor();
    Console.Write(subject);
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write($"  {fromStr}  ");
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write(dateStr);
    if (email.HasAttachments)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"  📎 {email.AttachmentCount}");
    }
    Console.ResetColor();
    Console.WriteLine();
}

static string FormatAddress(EmailAddress addr) =>
    addr.Name != null ? $"{addr.Name} <{addr.Email}>" : addr.Email;

static string FormatSize(long bytes) =>
    bytes < 1024 ? $"{bytes} B"
    : bytes < 1024 * 1024 ? $"{bytes / 1024.0:F1} KB"
    : $"{bytes / (1024.0 * 1024.0):F1} MB";

static void PrintBold(string text)
{
    Console.ForegroundColor = ConsoleColor.White;
    Console.Write(text);
    Console.ResetColor();
}
