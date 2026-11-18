using System.Drawing;
using enviro.Static;
using Xunit;

namespace enviro.Tests.Static;

public class WindowGeometryTests
{
    [Fact]
    public void TryParse_returns_false_without_the_argument()
    {
        Assert.False(WindowGeometry.TryParse([], out var bounds, out var maximized));
        Assert.Equal(Rectangle.Empty, bounds);
        Assert.False(maximized);

        Assert.False(WindowGeometry.TryParse(["--other", "1,2,3,4,1"], out _, out _));
    }

    [Fact]
    public void TryParse_ignores_a_trailing_argument_without_value() =>
        Assert.False(WindowGeometry.TryParse(["app.exe", WindowGeometry.Argument], out _, out _));

    [Theory]
    [InlineData("10,20,800,600,1", 10, 20, 800, 600, true)]
    [InlineData("-5,-6,1920,1040,0", -5, -6, 1920, 1040, false)]
    public void TryParse_reads_a_well_formed_argument(
        string value, int x, int y, int width, int height, bool maximized)
    {
        var ok = WindowGeometry.TryParse(["app.exe", WindowGeometry.Argument, value], out var bounds, out var wasMaximized);

        Assert.True(ok);
        Assert.Equal(new Rectangle(x, y, width, height), bounds);
        Assert.Equal(maximized, wasMaximized);
    }

    [Fact]
    public void TryParse_is_case_insensitive_on_the_argument_name() =>
        Assert.True(WindowGeometry.TryParse(["--WINDOW-BOUNDS", "1,2,300,200,0"], out _, out _));

    [Theory]
    [InlineData("a,b,c,d,e")]     // not numbers
    [InlineData("1,2,3,4")]       // too few parts
    [InlineData("1,2,3,4,5,6")]   // too many parts
    public void TryParse_rejects_malformed_values(string value) =>
        Assert.False(WindowGeometry.TryParse([WindowGeometry.Argument, value], out _, out _));

    [Fact]
    public void IsOnScreen_requires_minimum_size_and_full_containment()
    {
        var screen = new Rectangle(0, 0, 1920, 1080);

        Assert.True(WindowGeometry.IsOnScreen(new Rectangle(10, 20, 800, 600), [screen]));
        Assert.True(WindowGeometry.IsOnScreen(new Rectangle(0, 0, 1920, 1080), [screen])); // exactly the edge
        Assert.False(WindowGeometry.IsOnScreen(new Rectangle(0, 0, 199, 600), [screen])); // too narrow
        Assert.False(WindowGeometry.IsOnScreen(new Rectangle(0, 0, 800, 149), [screen])); // too short
        Assert.False(WindowGeometry.IsOnScreen(new Rectangle(1500, 0, 500, 600), [screen])); // sticks out
        Assert.False(WindowGeometry.IsOnScreen(new Rectangle(10, 20, 800, 600), []));        // no screens
        Assert.False(WindowGeometry.IsOnScreen(new Rectangle(10, 20, 800, 600),
            [new Rectangle(2000, 0, 1920, 1080)])); // on another monitor only
    }
}
