namespace MailSearch.Models;

public class Email
{
    public int? Id { get; set; }
    public string? MessageId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public EmailAddress From { get; set; } = new();
    public List<EmailAddress> To { get; set; } = [];
    public List<EmailAddress> Cc { get; set; } = [];
    public List<EmailAddress> Bcc { get; set; } = [];
    public DateTime? Date { get; set; }
    public string? BodyText { get; set; }
    public string? BodyHtml { get; set; }
    public bool HasAttachments { get; set; }
    public int AttachmentCount { get; set; }
    public string SourceFile { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; }
    public int? FolderId { get; set; }
    public string? FolderPath { get; set; }
}
