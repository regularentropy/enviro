using enviro.Factories;
using Xunit;

namespace enviro.Tests.Factories;

public class EnvGridFactoryTests
{
    [Fact]
    public void WrapToolTip_leaves_short_text_together() =>
        Assert.Equal(@"C:\tools\bin", EnvGridFactory.WrapToolTip(@"C:\tools\bin"));

    [Fact]
    public void WrapToolTip_breaks_a_long_path_into_lines_of_at_most_the_width()
    {
        var path = @"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer";

        var lines = EnvGridFactory.WrapToolTip(path, 30).Split('\n');

        Assert.All(lines, line => Assert.True(line.Length <= 30, $"'{line}' is longer than 30"));
        Assert.Equal(path, string.Concat(lines));
    }

    [Fact]
    public void WrapToolTip_keeps_existing_lines_together() =>
        Assert.Equal(
            $"enviro: {new string('a', 20)}\nsystem: {new string('b', 20)}",
            EnvGridFactory.WrapToolTip($"enviro: {new string('a', 20)}\nsystem: {new string('b', 20)}", 32));
}
