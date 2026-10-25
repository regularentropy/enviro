using System.Net;
using System.Text.RegularExpressions;

namespace enviro.Static;

/// <summary>
/// Converts the Markdown body of a GitHub release into plain text, so the update dialog can
/// show readable notes in a read-only text box instead of raw markup.
/// Only the constructs GitHub's generated notes use are handled; the result is text, not HTML.
/// </summary>
internal static partial class ReleaseNotesFormatter
{
    /// <summary>Shown when the release carries no body at all.</summary>
    private const string NoNotes = "No release notes were provided.";

    /// <summary>
    /// Flattens a Markdown release body into readable plain text.
    /// </summary>
    /// <param name="markdown">The raw Markdown body returned by the GitHub API.</param>
    /// <returns>The same notes without Markdown syntax, or a placeholder when there are none.</returns>
    public static string ToPlainText(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return NoNotes;

        var text = markdown.Replace("\r\n", "\n").Replace('\r', '\n');

        // Order matters: images before links (an image starts with "!"), and code before emphasis.
        text = HtmlComment().Replace(text, string.Empty);
        text = MarkdownImage().Replace(text, "$1");
        text = MarkdownLink().Replace(text, FormatLink);
        text = Autolink().Replace(text, "$1");
        text = HtmlLineBreak().Replace(text, "\n");
        text = HtmlTag().Replace(text, string.Empty);
        text = MarkdownRule().Replace(text, string.Empty);
        text = MarkdownHeading().Replace(text, "$1");
        text = MarkdownQuote().Replace(text, string.Empty);
        text = MarkdownBullet().Replace(text, "$1\u2022 ");
        text = InlineCode().Replace(text, "$1");
        text = Bold().Replace(text, "$1");

        // Decoding last so escaped tags ("&lt;b&gt;") survive as literal text rather than being stripped.
        text = WebUtility.HtmlDecode(text);
        text = TrailingSpaces().Replace(text, "\n");
        text = ExtraBlankLines().Replace(text, "\n\n");

        return text.Trim();
    }

    /// <summary>Writes a link as "label (url)", or just the url when the label already is one.</summary>
    private static string FormatLink(Match match)
    {
        var label = match.Groups[1].Value;
        var url = match.Groups[2].Value;
        return string.Equals(label, url, StringComparison.OrdinalIgnoreCase) ? url : $"{label} ({url})";
    }

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex HtmlComment();

    [GeneratedRegex(@"!\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex MarkdownImage();

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)\s]+)(?:\s+""[^""]*"")?\)")]
    private static partial Regex MarkdownLink();

    [GeneratedRegex(@"<((?:https?|mailto):[^>\s]+)>", RegexOptions.IgnoreCase)]
    private static partial Regex Autolink();

    [GeneratedRegex(@"<br\s*/?>|</p\s*>|</div\s*>|</li\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlLineBreak();

    [GeneratedRegex(@"</?[a-zA-Z][^>\n]*>")]
    private static partial Regex HtmlTag();

    /// <summary>A "---" style rule; a line beginning with a bullet is matched by the bullet rule instead.</summary>
    [GeneratedRegex(@"^[ \t]{0,3}(?:(?:-[ \t]*){3,}|(?:\*[ \t]*){3,}|(?:_[ \t]*){3,})$", RegexOptions.Multiline)]
    private static partial Regex MarkdownRule();

    /// <summary>ATX headings; a trailing " ##" sequence is dropped but a "C#" in the title is kept.</summary>
    [GeneratedRegex(@"^[ \t]{0,3}#{1,6}[ \t]+(.+?)(?:[ \t]+#+)?[ \t]*$", RegexOptions.Multiline)]
    private static partial Regex MarkdownHeading();

    [GeneratedRegex(@"^[ \t]{0,3}>[ \t]?", RegexOptions.Multiline)]
    private static partial Regex MarkdownQuote();

    [GeneratedRegex(@"^([ \t]*)[-*+][ \t]+", RegexOptions.Multiline)]
    private static partial Regex MarkdownBullet();

    [GeneratedRegex(@"`([^`\n]+)`")]
    private static partial Regex InlineCode();

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex Bold();

    [GeneratedRegex(@"[ \t]+\n")]
    private static partial Regex TrailingSpaces();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExtraBlankLines();
}
