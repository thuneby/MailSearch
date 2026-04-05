namespace MailSearch.Core.Models;

public class Attachment
{
    public int? Id { get; set; }
    public int EmailId { get; set; }
    public string Filename { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public long Size { get; set; }
    public string? StoragePath { get; set; }
    public byte[]? Data { get; set; }
}
