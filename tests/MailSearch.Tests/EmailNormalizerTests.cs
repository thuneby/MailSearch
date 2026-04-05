using MailSearch.Importer;

namespace MailSearch.Tests;

public class EmailNormalizerTests
{
    // ── ParseAddress ──────────────────────────────────────────────────────────

    [Fact]
    public void ParseAddress_ParsesNameAndEmail()
    {
        var result = EmailNormalizer.ParseAddress("Alice Smith <alice@example.com>");
        Assert.Equal("Alice Smith", result.Name);
        Assert.Equal("alice@example.com", result.Email);
    }

    [Fact]
    public void ParseAddress_ParsesPlainEmail()
    {
        var result = EmailNormalizer.ParseAddress("bob@example.com");
        Assert.Null(result.Name);
        Assert.Equal("bob@example.com", result.Email);
    }

    [Fact]
    public void ParseAddress_TrimsSurroundingWhitespace()
    {
        var result = EmailNormalizer.ParseAddress("  carol@example.com  ");
        Assert.Null(result.Name);
        Assert.Equal("carol@example.com", result.Email);
    }

    [Fact]
    public void ParseAddress_HandlesExtraSpacesBeforeAngleBracket()
    {
        var result = EmailNormalizer.ParseAddress("Dave  <dave@example.com>");
        Assert.Equal("Dave", result.Name);
        Assert.Equal("dave@example.com", result.Email);
    }

    // ── ParseAddressList ──────────────────────────────────────────────────────

    [Fact]
    public void ParseAddressList_ReturnsEmptyForNull()
    {
        Assert.Empty(EmailNormalizer.ParseAddressList(null));
    }

    [Fact]
    public void ParseAddressList_ReturnsEmptyForEmptyString()
    {
        Assert.Empty(EmailNormalizer.ParseAddressList(string.Empty));
    }

    [Fact]
    public void ParseAddressList_ParsesCommaSeparatedList()
    {
        var result = EmailNormalizer.ParseAddressList("alice@example.com, bob@example.com");
        Assert.Equal(2, result.Count);
        Assert.Equal("alice@example.com", result[0].Email);
        Assert.Equal("bob@example.com", result[1].Email);
    }

    [Fact]
    public void ParseAddressList_ParsesSemicolonSeparatedList()
    {
        var result = EmailNormalizer.ParseAddressList("alice@example.com; bob@example.com");
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ParseAddressList_ParsesNamedAddresses()
    {
        var result = EmailNormalizer.ParseAddressList("Alice <alice@example.com>, Bob <bob@example.com>");
        Assert.Equal("Alice", result[0].Name);
        Assert.Equal("alice@example.com", result[0].Email);
        Assert.Equal("Bob", result[1].Name);
        Assert.Equal("bob@example.com", result[1].Email);
    }

    [Fact]
    public void ParseAddressList_FiltersOutEmptyParts()
    {
        var result = EmailNormalizer.ParseAddressList("alice@example.com,,bob@example.com");
        Assert.Equal(2, result.Count);
    }
}
