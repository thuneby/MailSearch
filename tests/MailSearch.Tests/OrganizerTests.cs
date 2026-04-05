using MailSearch.Core.Database;
using MailSearch.Core.Models;
using MailSearch.Core.Organizer;
using OrganizerNs = MailSearch.Core.Organizer;

namespace MailSearch.Tests;

public class OrganizerTests : IDisposable
{
    private readonly MailSearchRepository _repo;
    private readonly Organizer _organizer;
    private readonly string _dbPath;

    public OrganizerTests()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mailsearch-org-test-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        _dbPath = Path.Combine(dir, "test.db");
        _repo = new MailSearchRepository(_dbPath);
        _organizer = new OrganizerNs.Organizer(_repo);
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
            Subject = "Test Email",
            From = new EmailAddress { Email = "test@example.com" },
            To = [],
            Cc = [],
            Bcc = [],
            BodyText = "Test body",
            HasAttachments = false,
            AttachmentCount = 0,
            SourceFile = "/test/file.msg",
            ImportedAt = DateTime.UtcNow,
        };
        configure?.Invoke(email);
        return email;
    }

    [Fact]
    public void Tag_AddsMultipleTagsAtOnce()
    {
        var emailId = _repo.InsertEmail(SampleEmail());
        _organizer.Tag(emailId, "urgent", "follow-up");

        var names = _organizer.GetTagsForEmail(emailId).Select(t => t.Name).OrderBy(n => n).ToList();
        Assert.Equal(new[] { "follow-up", "urgent" }, names);
    }

    [Fact]
    public void Untag_RemovesTag()
    {
        var emailId = _repo.InsertEmail(SampleEmail());
        _organizer.Tag(emailId, "temp");
        _organizer.Untag(emailId, "temp");

        Assert.Empty(_organizer.GetTagsForEmail(emailId));
    }

    [Fact]
    public void ListTags_ReturnsAllTagsAcrossEmails()
    {
        var id1 = _repo.InsertEmail(SampleEmail(e => e.Subject = "Email 1"));
        var id2 = _repo.InsertEmail(SampleEmail(e => e.Subject = "Email 2"));
        _organizer.Tag(id1, "alpha");
        _organizer.Tag(id2, "beta");

        var names = _organizer.ListTags().Select(t => t.Name).ToList();
        Assert.Equal(new[] { "alpha", "beta" }, names);
    }

    [Fact]
    public void ListEmailsByTag_ReturnsMatchingEmails()
    {
        var id1 = _repo.InsertEmail(SampleEmail(e => e.Subject = "Important Email"));
        _repo.InsertEmail(SampleEmail(e => e.Subject = "Regular Email"));
        _organizer.Tag(id1, "important");

        var emails = _organizer.ListEmailsByTag("important");
        Assert.Single(emails);
        Assert.Equal("Important Email", emails[0].Subject);
    }

    [Fact]
    public void ListEmailsByTag_ReturnsEmptyForNonExistentTag()
    {
        Assert.Empty(_organizer.ListEmailsByTag("nonexistent"));
    }
}
