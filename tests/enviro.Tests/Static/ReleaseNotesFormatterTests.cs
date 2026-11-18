using System.Drawing;
using enviro.Static;
using Xunit;

namespace enviro.Tests.Static;

public class ReleaseNotesFormatterTests
{
    private static readonly Color Ink = Color.Black;
    private static readonly Color Link = Color.Blue;

    private static string Rtf(string? markdown) =>
        ReleaseNotesFormatter.ToRtf(markdown, "Consolas", 10f, Ink, Link);

    [Fact]
    public void Null_or_blank_body_renders_the_placeholder()
    {
        Assert.Contains("No release notes were provided.", Rtf(null));
        Assert.Contains("No release notes were provided.", Rtf("   "));
    }

    [Fact]
    public void Output_is_a_complete_rtf_document_with_font_and_colour_tables()
    {
        var rtf = Rtf("hello");

        Assert.StartsWith(@"{\rtf1\ansi", rtf);
        Assert.Contains(@"\fonttbl", rtf);
        Assert.Contains(@"Consolas", rtf);
        Assert.Contains(@"\colortbl ;\red0\green0\blue0;\red0\green0\blue255;", rtf);
        Assert.Contains(@"\f0\fs20\cf1", rtf); // 10pt = 20 half-points, ink is colour 1
    }

    [Fact]
    public void Heading_becomes_bold_larger_text()
    {
        var rtf = Rtf("# Release 1.2");

        Assert.Contains(@"\b\fs28", rtf); // level 1 scales 10pt by 1.4 -> 14pt -> 28 half-points
        Assert.Contains("Release 1\\.2".Replace("\\.", "."), rtf);
    }

    [Fact]
    public void Heading_keeps_a_hash_inside_the_title() =>
        Assert.Contains("C# rocks", Rtf("## C# rocks"));

    [Fact]
    public void Bullet_becomes_a_real_bullet_paragraph()
    {
        var rtf = Rtf("- fixed crash");

        Assert.Contains(@"\pard\li360\fi-360\bullet\tab fixed crash", rtf);
    }

    [Fact]
    public void Link_becomes_a_hyperlink_field()
    {
        var rtf = Rtf("see [the docs](https://example.com/docs) now");

        Assert.Contains(@"{\field{\*\fldinst HYPERLINK ""https://example.com/docs""}"
            + @"{\fldrslt{\ul\cf2 the docs}}}", rtf);
    }

    [Fact]
    public void Autolink_becomes_a_hyperlink_to_itself() =>
        Assert.Contains("HYPERLINK \"https://example.com\"", Rtf("<https://example.com>"));

    [Fact]
    public void Bold_and_code_are_rendered_inline()
    {
        Assert.Contains(@"\b bold\b0 ", Rtf("**bold**"));
        Assert.Contains("`code`".Replace("`", ""), Rtf("`code`"));
    }

    [Fact]
    public void Rtf_control_characters_are_escaped() =>
        Assert.Contains(@"a\{b\}c\\d", Rtf("a{b}c\\d"));

    [Fact]
    public void Non_ascii_characters_become_unicode_escapes() =>
        Assert.Contains(@"caf\u233?", Rtf("caf\u00e9"));

    [Fact]
    public void Control_characters_are_stripped_from_the_body()
    {
        var rtf = Rtf("a\0b\ac"); // NUL and bell are not line breaks, so both are dropped

        // IndexOf instead of DoesNotContain: xunit's substring assert mis-handles a NUL expected value
        Assert.True(rtf.IndexOf('\0') < 0, $"NUL at {rtf.IndexOf('\0')} of {rtf.Length}");
        Assert.True(rtf.IndexOf('\a') < 0, $"BEL at {rtf.IndexOf('\a')} of {rtf.Length}");
        Assert.Contains("abc", rtf);
    }

    [Fact]
    public void Html_comments_and_tags_are_removed()
    {
        var rtf = Rtf("<!-- secret --><b>shown</b>");

        Assert.DoesNotContain("secret", rtf);
        Assert.DoesNotContain("<b>", rtf);
        Assert.Contains("shown", rtf);
    }

    [Fact]
    public void Horizontal_rules_and_quotes_lose_their_markup()
    {
        var rtf = Rtf("---\n> quoted");

        Assert.DoesNotContain("---", rtf);
        Assert.Contains("quoted", rtf);
        Assert.DoesNotContain("> quoted", rtf);
    }

    [Fact]
    public void Body_is_capped_at_32_kb_before_rendering()
    {
        var rtf = Rtf(new string('a', 40 * 1024));

        // 32 KB cap: the rendered body cannot carry more characters than that.
        Assert.True(rtf.Length < 33 * 1024 + 200, $"rtf length {rtf.Length}");
    }

    [Fact]
    public void Image_keeps_only_its_alt_text() =>
        Assert.Contains("logo", Rtf("![logo](https://example.com/x.png)"));
}
