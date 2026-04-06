namespace MailSearch.Web.Models;

public class ImportViewModel
{
    public bool? Success { get; set; }
    public bool IsError { get; set; }
    public string? Message { get; set; }
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
}
