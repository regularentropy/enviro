using System.Drawing;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace enviro.Static;

/// <summary>
/// Renders the Markdown body of a GitHub release as RTF, so the update dialog can show readable
/// notes - bold headings, real bullets, clickable links - instead of raw markup or unformatted text.
/// Only the constructs GitHub's generated notes use are handled; the result is RTF, not HTML.
/// </summary>
internal static partial class ReleaseNotesFormatter
{
    /// <summary>Shown when the release carries no body at all.</summary>
    private const string NoNotes = "No release notes were provided.";

    /// <summary>
    /// Upper bound on the notes that are rendered. The body arrives from the network, so the cost of
    /// rendering it is bounded before the pass starts instead of after it has become a hang.
    /// </summary>
    private const int MaxBodyLength = 32 * 1024;

    /// <summary>
    /// Renders a Markdown release body as a complete RTF document.
    /// </summary>
    /// <param name="markdown">The raw Markdown body returned by the GitHub API.</param>
    /// <param name="fontName">The face of the box the notes are shown in.</param>
    /// <param name="sizePoints">Body size in points, taken from that box so the notes scale with it.</param>
    /// <param name="ink">The body text colour.</param>
    /// <param name="link">The colour of a link.</param>
    /// <returns>A complete RTF document, or the placeholder document when there are no notes.</returns>
    public static string ToRtf(string? markdown, string fontName, float sizePoints, Color ink, Color link)
    {
        var body = new StringBuilder();

        var text = string.IsNullOrWhiteSpace(markdown) ? NoNotes : Sanitise(markdown);
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');

        // A comment spans lines, so it goes before the body is split into them.
        text = HtmlComment().Replace(text, string.Empty);

        // Starts at 1 so the first blank line cannot open the box with a gap.
        var blanks = 1;
        foreach (var paragraph in text.Split('\n'))
        {
            var line = MarkdownQuote().Replace(paragraph, string.Empty);

            if (string.IsNullOrWhiteSpace(line))
            {
                // One blank line between blocks, never more.
                if (++blanks > 1)
                    continue;

                body.Append("\\pard\\par ");
                continue;
            }

            if (MarkdownRule().IsMatch(line))
                continue;

            blanks = 0;

            var heading = MarkdownHeading().Match(line);
            if (heading.Success)
            {
                // The top two levels read as titles, the rest as bold lead-ins.
                var level = heading.Groups["hashes"].Value.Length;
                var size = HalfPoints(sizePoints * (level <= 2 ? 1.4f : 1.15f));
                body.Append("\\pard\\sb120\\sa60\\b\\fs").Append(size).Append(' ')
                    .Append(Inline(heading.Groups["text"].Value, link))
                    .Append("\\b0\\fs").Append(HalfPoints(sizePoints)).Append("\\par ");
                continue;
            }

            var bullet = MarkdownBullet().Match(line);
            if (bullet.Success)
            {
                body.Append("\\pard\\li360\\fi-360\\bullet\\tab ")
                    .Append(Inline(line[bullet.Length..], link))
                    .Append("\\par ");
                continue;
            }

            body.Append("\\pard ").Append(Inline(line, link)).Append("\\par ");
        }

        return $@"{{\rtf1\ansi\ansicpg1252\deff0{{\fonttbl{{\f0\fnil\fcharset0 {Escape(fontName)};}}}}"
            + $@"{{\colortbl ;{Colour(ink)};{Colour(link)};}}"
            + $@"\f0\fs{HalfPoints(sizePoints)}\cf1{body}}}";
    }

    /// <summary>
    /// Bounds and cleans the untrusted body before it is parsed: the length is capped, and control
    /// characters other than a line break or a tab are dropped, because a raw NUL or a form feed
    /// inside an RTF document is not text the reader should get.
    /// </summary>
    /// <remarks>
    /// shortcut: the cap truncates the notes silently, upgrade to a "notes truncated" notice if real
    /// release bodies ever approach it.
    /// </remarks>
    private static string Sanitise(string markdown)
    {
        var text = markdown.Length > MaxBodyLength ? markdown[..MaxBodyLength] : markdown;

        var clean = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c is '\n' or '\r' or '\t' || c >= ' ')
                clean.Append(c);
        }

        return clean.ToString();
    }

    /// <summary>
    /// Converts one line of inline Markdown into RTF, escaping the plain text around the styling.
    /// </summary>
    /// <param name="text">One line, with its block markers already removed.</param>
    /// <param name="link">The colour of a link.</param>
    /// <returns>The line as RTF.</returns>
    private static string Inline(string text, Color link)
    {
        var rtf = new StringBuilder(text.Length);
        var index = 0;

        foreach (Match match in InlineToken().Matches(text))
        {
            rtf.Append(Escape(WebUtility.HtmlDecode(text[index..match.Index])));
            index = match.Index + match.Length;

            if (match.Groups["alt"].Success)
            {
                // An image is a picture this dialog cannot show, so only its description survives.
                rtf.Append(Escape(match.Groups["alt"].Value));
            }
            else if (match.Groups["label"].Success)
            {
                rtf.Append(Link(match.Groups["label"].Value, match.Groups["url"].Value));
            }
            else if (match.Groups["auto"].Success)
            {
                rtf.Append(Link(match.Groups["auto"].Value, match.Groups["auto"].Value));
            }
            else if (match.Groups["br"].Success)
            {
                rtf.Append("\\line ");
            }
            else if (match.Groups["code"].Success)
            {
                rtf.Append(Escape(WebUtility.HtmlDecode(match.Groups["code"].Value)));
            }
            else if (match.Groups["bold"].Success)
            {
                rtf.Append("\\b ").Append(Escape(WebUtility.HtmlDecode(match.Groups["bold"].Value))).Append("\\b0 ");
            }

            // Anything else is an HTML tag: markup only, so it is dropped.
        }

        rtf.Append(Escape(WebUtility.HtmlDecode(text[index..])));
        return rtf.ToString();
    }

    /// <summary>
    /// Renders a link as an RTF hyperlink field: a clickable label whose target is the URL.
    /// </summary>
    /// <param name="label">The text the reader sees.</param>
    /// <param name="url">The address the field carries.</param>
    /// <returns>The link as RTF; the second colour-table entry is the link colour.</returns>
    private static string Link(string label, string url)
    {
        var target = Escape(url.Replace("\"", string.Empty));
        return $@"{{\field{{\*\fldinst HYPERLINK ""{target}""}}{{\fldrslt{{\ul\cf2 {Escape(WebUtility.HtmlDecode(label))}}}}}}}";
    }

    /// <summary>
    /// Escapes text for RTF: the control characters, and every non-ASCII character as a signed
    /// 16-bit \u escape followed by a substitute for readers that cannot render it.
    /// </summary>
    private static string Escape(string text)
    {
        var rtf = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            switch (c)
            {
                case '\\' or '{' or '}':
                    rtf.Append('\\').Append(c);
                    break;
                case '\t':
                    rtf.Append("\\tab ");
                    break;
                case > (char)127:
                    rtf.Append("\\u").Append((short)c).Append('?');
                    break;
                default:
                    rtf.Append(c);
                    break;
            }
        }

        return rtf.ToString();
    }

    /// <summary>One palette colour as an RTF colour-table entry.</summary>
    private static string Colour(Color colour) => $@"\red{colour.R}\green{colour.G}\blue{colour.B}";

    /// <summary>RTF font sizes are half-points, so a fractional point size is rounded to the nearest one.</summary>
    private static int HalfPoints(float sizePoints) => (int)MathF.Round(sizePoints * 2);

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex HtmlComment();

    /// <summary>
    /// Every inline construct at once, in one pass, so the plain text around them is escaped once.
    /// The order inside the pattern is the order they are tried in: an image before a link, a break
    /// before a bare tag, and emphasis last.
    /// </summary>
    /// <remarks>
    /// shortcut: styling nested inside a link label or a bold span is dropped, upgrade when the
    /// project's own notes start nesting emphasis.
    /// </remarks>
    [GeneratedRegex(
        @"!\[(?<alt>[^\]]*)\]\([^)]*\)"
        + @"|\[(?<label>[^\]]+)\]\((?<url>[^)\s]+)(?:\s+""[^""]*"")?\)"
        + @"|<(?<auto>(?:https?|mailto):[^>\s]+)>"
        + @"|(?<br><br\s*/?>)"
        + @"|(?<tag></?[a-zA-Z][^>\n]*>)"
        + @"|`(?<code>[^`\n]+)`"
        + @"|\*\*(?<bold>.+?)\*\*",
        RegexOptions.IgnoreCase)]
    private static partial Regex InlineToken();

    /// <summary>A horizontal rule; anything that is a bullet is matched by the bullet rule instead.</summary>
    [GeneratedRegex(@"^[ \t]{0,3}(?:(?:-[ \t]*){3,}|(?:\*[ \t]*){3,}|(?:_[ \t]*){3,})$")]
    private static partial Regex MarkdownRule();

    /// <summary>ATX headings; a trailing " ##" sequence is dropped but a "C#" in the title is kept.</summary>
    [GeneratedRegex(@"^[ \t]{0,3}(?<hashes>#{1,6})[ \t]+(?<text>.+?)(?:[ \t]+#+)?[ \t]*$")]
    private static partial Regex MarkdownHeading();

    [GeneratedRegex(@"^[ \t]{0,3}>[ \t]?")]
    private static partial Regex MarkdownQuote();

    [GeneratedRegex(@"^[ \t]*[-*+][ \t]+")]
    private static partial Regex MarkdownBullet();
}
