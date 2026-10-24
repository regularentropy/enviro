namespace enviro.Static;

/// <summary>
/// Reads the optional <c>--window-bounds</c> argument an elevation restart passes along, and checks
/// the rectangle against the screens the caller supplies (the screen list stays with the caller).
/// </summary>
internal static class WindowGeometry
{
    /// <summary>The command-line argument the elevated restart uses to pass the current window bounds.</summary>
    public const string Argument = "--window-bounds";

    /// <summary>
    /// Reads the optional <c>--window-bounds x,y,width,height,maximized</c> argument.
    /// </summary>
    /// <param name="args">The command line, as <see cref="Environment.GetCommandLineArgs"/> returns it.</param>
    /// <param name="bounds">The parsed rectangle, or <see cref="Rectangle.Empty"/> when there is none.</param>
    /// <param name="maximized">Whether the sender asked for a maximized window.</param>
    /// <returns>Whether a well-formed argument was found.</returns>
    public static bool TryParse(string[] args, out Rectangle bounds, out bool maximized)
    {
        bounds = Rectangle.Empty;
        maximized = false;

        for (var i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], Argument, StringComparison.OrdinalIgnoreCase))
                continue;

            var parts = args[i + 1].Split(',');

            if (parts.Length != 5
                || !int.TryParse(parts[0], out var x)
                || !int.TryParse(parts[1], out var y)
                || !int.TryParse(parts[2], out var width)
                || !int.TryParse(parts[3], out var height))
                return false;

            bounds = new Rectangle(x, y, width, height);
            maximized = parts[4] == "1";
            return true;
        }

        return false;
    }

    /// <summary>
    /// Whether the passed bounds are plausible and still touch one of the given working areas.
    /// </summary>
    /// <param name="bounds">The rectangle to check.</param>
    /// <param name="workingAreas">The screens' working areas, e.g. <c>Screen.AllScreens.Select(s =&gt; s.WorkingArea)</c>.</param>
    /// <returns>True when the bounds are big enough and overlap a screen.</returns>
    public static bool IsOnScreen(Rectangle bounds, IEnumerable<Rectangle> workingAreas) =>
        bounds.Width >= 200
        && bounds.Height >= 150
        && workingAreas.Any(area => area.IntersectsWith(bounds));
}
