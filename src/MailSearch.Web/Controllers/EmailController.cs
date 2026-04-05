using Microsoft.AspNetCore.Mvc;
using MailSearch.Database;
using MailSearch.Search;
using MailSearch.Web.Models;
using OrganizerService = MailSearch.Organizer.Organizer;

namespace MailSearch.Web.Controllers;

public class EmailController(MailSearchRepository repo) : Controller
{
    private const int PageSize = 20;

    // GET /Email?page=1&tag=...
    public IActionResult Index(int page = 1, string? tag = null)
    {
        int offset = (page - 1) * PageSize;
        var emails = repo.ListEmails(PageSize, offset, tag: tag);
        var total = repo.CountEmails();

        var vm = new EmailListViewModel
        {
            Emails = emails,
            Tag = tag,
            Page = page,
            PageSize = PageSize,
            TotalCount = total,
            Tags = repo.ListTags(),
        };
        return View(vm);
    }

    // GET /Email/Search?q=...&page=1
    public IActionResult Search(string? q, int page = 1)
    {
        if (string.IsNullOrWhiteSpace(q))
            return View(new EmailSearchViewModel { Query = string.Empty });

        int offset = (page - 1) * PageSize;
        var results = repo.SearchEmails(q, PageSize, offset);

        var vm = new EmailSearchViewModel
        {
            Query = q,
            Results = results,
            Page = page,
            PageSize = PageSize,
        };
        return View(vm);
    }

    // GET /Email/Details/5
    public IActionResult Details(int id)
    {
        var email = repo.GetEmailById(id);
        if (email == null)
            return NotFound();

        var vm = new EmailDetailsViewModel
        {
            Email = email,
            Attachments = repo.GetAttachmentsByEmailId(id),
            Tags = repo.GetTagsForEmail(id),
        };
        return View(vm);
    }

    // POST /Email/AddTag
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddTag(int id, string tagName)
    {
        if (!string.IsNullOrWhiteSpace(tagName))
        {
            var organizer = new OrganizerService(repo);
            organizer.Tag(id, tagName.Trim());
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST /Email/RemoveTag
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RemoveTag(int id, string tagName)
    {
        var organizer = new OrganizerService(repo);
        organizer.Untag(id, tagName);
        return RedirectToAction(nameof(Details), new { id });
    }
}
