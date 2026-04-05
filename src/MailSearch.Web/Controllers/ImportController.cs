using MailSearch.Core.Database;
using MailSearch.Core.Importer;
using MailSearch.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace MailSearch.Web.Controllers;

public class ImportController(MailSearchRepository repo, IConfiguration config, ILogger<ImportController> logger) : Controller
{
    private static readonly string[] AllowedExtensions = [".msg"];
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB

    private string GetAttachmentDir()
    {
        var dir = config["MailSearch:AttachmentDir"];
        if (string.IsNullOrWhiteSpace(dir))
            dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".mailsearch", "attachments");
        Directory.CreateDirectory(dir);
        return dir;
    }

    // GET /Import
    public IActionResult Index()
    {
        return View(new ImportViewModel());
    }

    // POST /Import
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return View(new ImportViewModel { Success = false, Message = "Please select a file to upload." });
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return View(new ImportViewModel
            {
                Success = false,
                Message = $"File is too large. Maximum allowed size is {MaxFileSizeBytes / (1024 * 1024)} MB.",
            });
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
        {
            return View(new ImportViewModel
            {
                Success = false,
                Message = $"Unsupported file type \"{ext}\". Only .msg files are supported.",
            });
        }

        var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".msg");
        try
        {
            await using (var stream = System.IO.File.Create(tempPath))
                await file.CopyToAsync(stream);

            var result = MsgImporter.ImportMsg(tempPath, repo, GetAttachmentDir());

            var vm = new ImportViewModel
            {
                Imported = result.Imported,
                Skipped = result.Skipped,
                Errors = result.Errors,
            };

            if (result.Imported > 0)
            {
                vm.Success = true;
                vm.Message = "Email imported successfully.";
            }
            else if (result.Skipped > 0)
            {
                vm.Success = false;
                vm.Message = "Email was already imported (duplicate).";
            }
            else
            {
                vm.Success = false;
                vm.IsError = true;
                vm.Message = "Import failed. The file may not be a valid Outlook message.";
            }

            return View(vm);
        }
        finally
        {
            try
            {
                if (System.IO.File.Exists(tempPath))
                    System.IO.File.Delete(tempPath);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete temporary file {TempPath}", tempPath);
            }
        }
    }
}
