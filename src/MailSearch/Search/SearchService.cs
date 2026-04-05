using MailSearch.Database;
using MailSearch.Models;

namespace MailSearch.Search;

public class SearchOptions
{
    public string Query { get; set; } = string.Empty;
    public int Limit { get; set; } = 20;
    public int Offset { get; set; } = 0;
}

/// <summary>
/// Full-text search over imported emails using SQLite FTS5.
///
/// The query supports FTS5 syntax:
///   - Simple terms:    "meeting"
///   - Phrases:         '"quarterly meeting"'
///   - Boolean ops:     "meeting AND Q4"
///   - Prefix search:   "meet*"
///   - Column filter:   "subject:meeting"
/// </summary>
public static class SearchService
{
    public static List<EmailSearchResult> Search(MailSearchRepository repo, SearchOptions options)
    {
        return repo.SearchEmails(options.Query, options.Limit, options.Offset);
    }
}
