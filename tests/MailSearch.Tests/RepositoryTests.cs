using MailSearch.Core.Database;
using MailSearch.Core.Models;

namespace MailSearch.Tests;

public class RepositoryTests : IDisposable
{
    private readonly MailSearchRepository _repo;
    private readonly string _dbPath;

    public RepositoryTests()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mailsearch-test-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        _dbPath = Path.Combine(dir, "test.db");
        _repo = new MailSearchRepository(_dbPath);
    }

    public void Dispose()
    {
        _repo.Close();
        Directory.Delete(Path.GetDirectoryName(_dbPath)!, recursive: true);
    }

    private static Email SampleEmail(Action<Email>? configure = null)
    {
        var email = new Email
        {
            Subject = "Hello World",
            From = new EmailAddress { Name = "Alice", Email = "alice@example.com" },
            To = [new EmailAddress { Email = "bob@example.com" }],
            Cc = [],
            Bcc = [],
            Date = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            BodyText = "This is the email body text about quarterly results.",
            HasAttachments = false,
            AttachmentCount = 0,
            SourceFile = "/path/to/file.pst",
            ImportedAt = DateTime.UtcNow,
        };
        configure?.Invoke(email);
        return email;
    }

    // ── email CRUD ────────────────────────────────────────────────────────────

    [Fact]
    public void InsertEmail_ReturnsNewId()
    {
        var id = _repo.InsertEmail(SampleEmail());
        Assert.True(id > 0);
    }

    [Fact]
    public void GetEmailById_ReturnsCorrectEmail()
    {
        var id = _repo.InsertEmail(SampleEmail());
        var retrieved = _repo.GetEmailById(id);
        Assert.NotNull(retrieved);
        Assert.Equal("Hello World", retrieved.Subject);
        Assert.Equal("Alice", retrieved.From.Name);
        Assert.Equal("alice@example.com", retrieved.From.Email);
        Assert.Single(retrieved.To);
        Assert.Equal("bob@example.com", retrieved.To[0].Email);
    }

    [Fact]
    public void GetEmailById_ReturnsNullForMissingId()
    {
        Assert.Null(_repo.GetEmailById(9999));
    }

    [Fact]
    public void InsertEmail_IgnoresDuplicateMessageId()
    {
        var email = SampleEmail(e => e.MessageId = "unique-id-123@example.com");
        var id1 = _repo.InsertEmail(email);
        var id2 = _repo.InsertEmail(email);
        Assert.True(id1 > 0);
        Assert.Equal(0, id2);
    }

    [Fact]
    public void ListEmails_OrderedByDateDescending()
    {
        _repo.InsertEmail(SampleEmail(e => { e.Subject = "Older"; e.Date = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc); }));
        _repo.InsertEmail(SampleEmail(e => { e.Subject = "Newer"; e.Date = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc); }));

        var list = _repo.ListEmails();
        Assert.Equal("Newer", list[0].Subject);
        Assert.Equal("Older", list[1].Subject);
    }

    [Fact]
    public void CountEmails_ReturnsTotal()
    {
        _repo.InsertEmail(SampleEmail());
        _repo.InsertEmail(SampleEmail(e => e.Subject = "Second"));
        Assert.Equal(2, _repo.CountEmails());
    }

    [Fact]
    public void DeleteEmail_RemovesEmail()
    {
        var id = _repo.InsertEmail(SampleEmail());
        Assert.True(_repo.DeleteEmail(id));
        Assert.Null(_repo.GetEmailById(id));
    }

    [Fact]
    public void DeleteEmail_ReturnsFalseForMissingId()
    {
        Assert.False(_repo.DeleteEmail(9999));
    }

    // ── attachments ───────────────────────────────────────────────────────────

    [Fact]
    public void InsertAndGetAttachments()
    {
        var emailId = _repo.InsertEmail(SampleEmail(e => { e.HasAttachments = true; e.AttachmentCount = 1; }));
        var att = new Attachment
        {
            EmailId = emailId,
            Filename = "report.pdf",
            MimeType = "application/pdf",
            Size = 1024,
            StoragePath = "/tmp/report.pdf",
        };

        var attId = _repo.InsertAttachment(att);
        Assert.True(attId > 0);

        var attachments = _repo.GetAttachmentsByEmailId(emailId);
        Assert.Single(attachments);
        Assert.Equal("report.pdf", attachments[0].Filename);
        Assert.Equal("application/pdf", attachments[0].MimeType);
        Assert.Equal(1024, attachments[0].Size);
    }

    [Fact]
    public void GetAttachments_ReturnsEmptyWhenNone()
    {
        var emailId = _repo.InsertEmail(SampleEmail());
        Assert.Empty(_repo.GetAttachmentsByEmailId(emailId));
    }

    [Fact]
    public void DeleteEmail_CascadesToAttachments()
    {
        var emailId = _repo.InsertEmail(SampleEmail());
        _repo.InsertAttachment(new Attachment { EmailId = emailId, Filename = "doc.txt", Size = 100 });

        _repo.DeleteEmail(emailId);
        Assert.Empty(_repo.GetAttachmentsByEmailId(emailId));
    }

    // ── tags ──────────────────────────────────────────────────────────────────

    [Fact]
    public void AddAndGetTagsForEmail()
    {
        var emailId = _repo.InsertEmail(SampleEmail());
        _repo.AddTagToEmail(emailId, "important");
        _repo.AddTagToEmail(emailId, "work");

        var names = _repo.GetTagsForEmail(emailId).Select(t => t.Name).OrderBy(n => n).ToList();
        Assert.Equal(new[] { "important", "work" }, names);
    }

    [Fact]
    public void AddTag_DoesNotDuplicate()
    {
        var emailId = _repo.InsertEmail(SampleEmail());
        _repo.AddTagToEmail(emailId, "important");
        _repo.AddTagToEmail(emailId, "important");

        Assert.Single(_repo.GetTagsForEmail(emailId));
    }

    [Fact]
    public void RemoveTag_RemovesIt()
    {
        var emailId = _repo.InsertEmail(SampleEmail());
        _repo.AddTagToEmail(emailId, "work");
        _repo.RemoveTagFromEmail(emailId, "work");

        Assert.Empty(_repo.GetTagsForEmail(emailId));
    }

    [Fact]
    public void ListTags_ReturnsAllTagsAlphabetically()
    {
        var emailId = _repo.InsertEmail(SampleEmail());
        _repo.AddTagToEmail(emailId, "alpha");
        _repo.AddTagToEmail(emailId, "beta");

        var names = _repo.ListTags().Select(t => t.Name).ToList();
        Assert.Equal(new[] { "alpha", "beta" }, names);
    }

    [Fact]
    public void ListEmails_FiltersByTag()
    {
        var id1 = _repo.InsertEmail(SampleEmail(e => e.Subject = "Tagged"));
        _repo.InsertEmail(SampleEmail(e => e.Subject = "Not tagged"));
        _repo.AddTagToEmail(id1, "review");

        var emails = _repo.ListEmails(tag: "review");
        Assert.Single(emails);
        Assert.Equal(id1, emails[0].Id);
    }

    // ── full-text search ──────────────────────────────────────────────────────

    [Fact]
    public void SearchEmails_FindsByKeyword()
    {
        _repo.InsertEmail(SampleEmail(e => { e.Subject = "Quarterly results"; e.BodyText = "Revenue increased by 15% this quarter."; }));
        _repo.InsertEmail(SampleEmail(e => { e.Subject = "Team meeting tomorrow"; e.BodyText = "Please join the meeting at 10am."; }));
        _repo.InsertEmail(SampleEmail(e => { e.Subject = "Invoice #1234"; e.BodyText = "Attached is the invoice for services rendered."; }));

        var results = _repo.SearchEmails("meeting");
        Assert.Single(results);
        Assert.Equal("Team meeting tomorrow", results[0].Subject);
    }

    [Fact]
    public void SearchEmails_ReturnsEmptyWhenNoMatch()
    {
        _repo.InsertEmail(SampleEmail());
        var results = _repo.SearchEmails("nonexistentterm12345");
        Assert.Empty(results);
    }

    [Fact]
    public void SearchEmails_IncludesSnippet()
    {
        _repo.InsertEmail(SampleEmail(e => { e.Subject = "Team meeting tomorrow"; e.BodyText = "Please join the meeting at 10am."; }));
        var results = _repo.SearchEmails("meeting");
        Assert.NotNull(results[0].Snippet);
        Assert.Contains("[meeting]", results[0].Snippet);
    }

    [Fact]
    public void SearchEmails_RespectsLimitAndOffset()
    {
        _repo.InsertEmail(SampleEmail(e => { e.Subject = "Meeting 1"; e.BodyText = "the meeting agenda"; }));
        _repo.InsertEmail(SampleEmail(e => { e.Subject = "Meeting 2"; e.BodyText = "the meeting notes"; }));
        _repo.InsertEmail(SampleEmail(e => { e.Subject = "Meeting 3"; e.BodyText = "the meeting summary"; }));

        var all = _repo.SearchEmails("meeting");
        var limited = _repo.SearchEmails("meeting", limit: 2);
        Assert.Equal(2, limited.Count);

        if (all.Count > 2)
        {
            var offset = _repo.SearchEmails("meeting", limit: 2, offset: 2);
            Assert.All(offset, r => Assert.DoesNotContain(limited, l => l.Id == r.Id));
        }
    }
}
