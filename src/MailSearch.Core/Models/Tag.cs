using System;

namespace MailSearch.Core.Models;

public class Tag
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
