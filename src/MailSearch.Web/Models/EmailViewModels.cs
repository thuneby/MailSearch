using MailSearch.Core.Models;

namespace MailSearch.Web.Models;

public class EmailListViewModel
{
    public List<Email> Emails { get; set; } = [];
    public List<Tag> Tags { get; set; } = [];
    public string? Tag { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public class EmailSearchViewModel
{
    public string Query { get; set; } = string.Empty;
    public List<EmailSearchResult> Results { get; set; } = [];
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public bool HasNextPage => Results.Count == PageSize;
}

public class EmailDetailsViewModel
{
    public Email Email { get; set; } = new();
    public List<Attachment> Attachments { get; set; } = [];
    public List<Tag> Tags { get; set; } = [];
}
