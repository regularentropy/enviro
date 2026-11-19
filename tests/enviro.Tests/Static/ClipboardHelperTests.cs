using enviro.Static;
using enviro.Tests.Services;
using Xunit;

namespace enviro.Tests.Static;

public class ClipboardHelperTests
{
    [Fact]
    public void FormatNameValueLines_writes_one_name_equals_value_line_per_row()
    {
        var rows = new[] { Workspace.Row("PATH", "a;b"), Workspace.Row("JAVA_HOME", @"C:\jdk") };

        Assert.Equal(
            $"PATH=a;b{Environment.NewLine}JAVA_HOME=C:\\jdk",
            ClipboardHelper.FormatNameValueLines(rows));
    }

    [Fact]
    public void FormatNameValueLines_of_nothing_is_empty() =>
        Assert.Equal(string.Empty, ClipboardHelper.FormatNameValueLines([]));

    [Fact]
    public void FormatValueSelection_copies_one_row_bare_and_many_as_name_value_lines()
    {
        var one = new[] { Workspace.Row("PATH", "a;b") };
        var many = new[] { Workspace.Row("PATH", "a;b"), Workspace.Row("JAVA_HOME", @"C:\jdk") };

        Assert.Equal("a;b", ClipboardHelper.FormatValueSelection(one));
        Assert.Equal(
            $"PATH=a;b{Environment.NewLine}JAVA_HOME=C:\\jdk",
            ClipboardHelper.FormatValueSelection(many));
    }

    [Fact]
    public void FormatNameSelection_copies_one_row_bare_and_many_as_name_lines()
    {
        var one = new[] { Workspace.Row("PATH", "a;b") };
        var many = new[] { Workspace.Row("PATH", "a;b"), Workspace.Row("JAVA_HOME", @"C:\jdk") };

        Assert.Equal("PATH", ClipboardHelper.FormatNameSelection(one));
        Assert.Equal($"PATH{Environment.NewLine}JAVA_HOME", ClipboardHelper.FormatNameSelection(many));
    }

    [Fact]
    public void FormatNameLines_and_FormatValueLines_join_one_property_per_line()
    {
        var many = new[] { Workspace.Row("PATH", "a;b"), Workspace.Row("JAVA_HOME", @"C:\jdk") };

        Assert.Equal($"PATH{Environment.NewLine}JAVA_HOME", ClipboardHelper.FormatNameLines(many));
        Assert.Equal($"a;b{Environment.NewLine}C:\\jdk", ClipboardHelper.FormatValueLines(many));
    }
}
