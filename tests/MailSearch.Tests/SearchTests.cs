using MailSearch.Core.Database;
using MailSearch.Core.Models;
using MailSearch.Core.Search;

namespace MailSearch.Tests;

public class SearchTests : IDisposable
{
    private readonly MailSearchRepository _repo;
    private readonly string _dbPath;

    public SearchTests()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mailsearch-search-test-" + Guid.NewGuid());
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
            Subject = "Test Email",
            From = new EmailAddress { Name = "Sender", Email = "sender@example.com" },
            To = [new EmailAddress { Email = "recipient@example.com" }],
            Cc = [],
            Bcc = [],
            Date = DateTime.UtcNow,
            BodyText = "Generic email body",
            HasAttachments = false,
            AttachmentCount = 0,
            SourceFile = "/test/file.msg",
            ImportedAt = DateTime.UtcNow,
        };
        configure?.Invoke(email);
        return email;
    }

    [Fact]
    public void Search_FindsBySubjectWord()
    {
        _repo.InsertEmail(SampleEmail(e => { e.Subject = "Project Update"; e.BodyText = "Latest news"; }));
        _repo.InsertEmail(SampleEmail(e => { e.Subject = "Invoice Q3"; e.BodyText = "Finance stuff"; }));

        var results = SearchService.Search(_repo, new SearchOptions { Query = "project" });
        Assert.Single(results);
        Assert.Equal("Project Update", results[0].Subject);
    }

    [Fact]
    public void Search_FindsByBodyWord()
    {
        _repo.InsertEmail(SampleEmail(e => { e.Subject = "Hello"; e.BodyText = "Budget planning for next year"; }));
        _repo.InsertEmail(SampleEmail(e => { e.Subject = "World"; e.BodyText = "Have a great day"; }));

        var results = SearchService.Search(_repo, new SearchOptions { Query = "budget" });
        Assert.Single(results);
        Assert.Equal("Hello", results[0].Subject);
    }

    [Fact]
    public void Search_ReturnsEmptyWhenNoMatch()
    {
        _repo.InsertEmail(SampleEmail(e => e.Subject = "Hello"));
        var results = SearchService.Search(_repo, new SearchOptions { Query = "xyzzy99999" });
        Assert.Empty(results);
    }

    [Fact]
    public void Search_RespectsLimit()
    {
        for (var i = 0; i < 5; i++)
            _repo.InsertEmail(SampleEmail(e => { e.Subject = $"Report {Guid.NewGuid()}"; e.BodyText = "quarterly report content"; }));

        var results = SearchService.Search(_repo, new SearchOptions { Query = "quarterly", Limit = 3 });
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void Search_SupportsPrefixFts5Syntax()
    {
        _repo.InsertEmail(SampleEmail(e => { e.Subject = "Financial Report"; e.BodyText = "finances look good"; }));
        var results = SearchService.Search(_repo, new SearchOptions { Query = "financ*" });
        Assert.True(results.Count >= 1);
    }
}
