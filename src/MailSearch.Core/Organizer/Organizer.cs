using System.Collections.Generic;
using MailSearch.Core.Database;
using MailSearch.Core.Models;

namespace MailSearch.Core.Organizer;

/// <summary>
/// Provides tagging and listing operations for emails.
/// </summary>
public class Organizer(MailSearchRepository repo)
{
    /// <summary>Adds one or more tags to an email.</summary>
    public void Tag(int emailId, params string[] tagNames)
    {
        foreach (var name in tagNames)
            repo.AddTagToEmail(emailId, name.Trim());
    }

    /// <summary>Removes a tag from an email.</summary>
    public void Untag(int emailId, string tagName)
    {
        repo.RemoveTagFromEmail(emailId, tagName.Trim());
    }

    /// <summary>Returns all tags on a specific email.</summary>
    public List<Tag> GetTagsForEmail(int emailId) =>
        repo.GetTagsForEmail(emailId);

    /// <summary>Returns all tags in the database.</summary>
    public List<Tag> ListTags() =>
        repo.ListTags();

    /// <summary>Returns emails that have a given tag.</summary>
    public List<Email> ListEmailsByTag(string tagName, int limit = 50, int offset = 0) =>
        repo.ListEmails(limit, offset, tag: tagName);
}
