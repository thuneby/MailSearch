namespace MailSearch.Models;

public class EmailSearchResult : Email
{
    public string? Snippet { get; set; }
    public double? Rank { get; set; }
}
