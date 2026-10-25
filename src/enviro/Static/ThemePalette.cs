using enviro.Models;
using enviro.Services;

namespace enviro.Static;

/// <summary>
/// The single owner of the application's colours: one light and one dark palette, chosen from the
/// colour mode Windows Forms applied at startup.
/// </summary>
/// <remarks>
/// Feature code asks here for a colour instead of naming one, so the two modes cannot drift apart
/// and theming a new surface means adding a lookup rather than another literal. System colours stay
/// in use only where a standard control expects one (for example the unchanged row's light
/// background, which is the window colour).
/// </remarks>
internal static class ThemePalette
{
    private static readonly Palette Light = new()
    {
        GridBackground = SystemColors.Window,
        CellText = SystemColors.WindowText,
        GridLines = SystemColors.Control,
        HeaderBackground = SystemColors.Control,
        HeaderText = SystemColors.ControlText,
        SystemHeaders = true,
        Artwork = Color.Black,
        SystemLinkColours = true,
        LinkText = SystemColors.HotTrack,
        LinkActive = Color.Red,
        LinkVisited = Color.Purple,
        SelectionText = Color.Black,
        MutedText = SystemColors.GrayText,
        InfoBackground = SystemColors.Info,
        Unchanged = new(SystemColors.Window, Color.FromArgb(214, 214, 214)),
        Added = new(Color.LightGreen, Color.FromArgb(112, 199, 112)),
        Modified = new(Color.LightYellow, Color.FromArgb(229, 214, 128)),
        Deleted = new(Color.LightCoral, Color.FromArgb(222, 133, 133)),
        Corrupted = new(Color.Red, Color.FromArgb(230, 96, 96)),
        SegmentExists = Color.ForestGreen,
        SegmentMissing = Color.Firebrick,
        SegmentNotPath = SystemColors.GrayText,
        FileStatusNew = Color.LightGreen,
        FileStatusOverwrites = Color.LightYellow,
        FileStatusDefault = SystemColors.Window,
    };

    // Dark tints are designed for contrast, not derived by inverting the light ones: every row
    // background is dark enough for the light text the dark mode gives a standard control, and
    // every selection keeps a distinguishable, slightly lighter shade of its state colour.
    private static readonly Palette Dark = new()
    {
        GridBackground = Color.FromArgb(32, 32, 32),
        CellText = Color.FromArgb(235, 235, 235),
        GridLines = Color.FromArgb(64, 64, 64),
        HeaderBackground = Color.FromArgb(45, 45, 48),
        HeaderText = Color.FromArgb(235, 235, 235),
        SystemHeaders = false,
        Artwork = Color.FromArgb(235, 235, 235),
        SystemLinkColours = false,
        LinkText = Color.White,
        LinkActive = Color.FromArgb(140, 200, 255),
        LinkVisited = Color.FromArgb(200, 200, 200),
        SelectionText = Color.White,
        MutedText = Color.FromArgb(160, 160, 160),
        InfoBackground = Color.FromArgb(58, 52, 24),
        Unchanged = new(Color.FromArgb(32, 32, 32), Color.FromArgb(60, 60, 64)),
        Added = new(Color.FromArgb(30, 58, 36), Color.FromArgb(46, 86, 54)),
        Modified = new(Color.FromArgb(58, 52, 24), Color.FromArgb(88, 79, 36)),
        Deleted = new(Color.FromArgb(68, 34, 34), Color.FromArgb(100, 50, 50)),
        Corrupted = new(Color.FromArgb(90, 26, 26), Color.FromArgb(128, 38, 38)),
        SegmentExists = Color.FromArgb(126, 217, 140),
        SegmentMissing = Color.FromArgb(255, 128, 128),
        SegmentNotPath = Color.FromArgb(160, 160, 160),
        FileStatusNew = Color.FromArgb(30, 58, 36),
        FileStatusOverwrites = Color.FromArgb(58, 52, 24),
        FileStatusDefault = Color.FromArgb(32, 32, 32),
    };

    /// <summary>
    /// The palette of the mode the process started in. The colour mode cannot change without a
    /// restart, so the choice is stable for the lifetime of the window.
    /// </summary>
    private static Palette Active
    {
        get
        {
#pragma warning disable WFO5001 // The colour-mode API is still experimental.
            return Application.IsDarkModeEnabled ? Dark : Light;
#pragma warning restore WFO5001
        }
    }

    /// <summary>
    /// The background of the empty area of a grid.
    /// </summary>
    public static Color GridBackground => Active.GridBackground;

    /// <summary>
    /// The text colour of an ordinary grid cell (a row's state colour supplies the background).
    /// </summary>
    public static Color CellText => Active.CellText;

    /// <summary>
    /// The colour of a grid's row and column lines.
    /// </summary>
    public static Color GridLines => Active.GridLines;

    /// <summary>
    /// The background of a grid's column headers.
    /// </summary>
    public static Color HeaderBackground => Active.HeaderBackground;

    /// <summary>
    /// The text colour of a grid's column headers.
    /// </summary>
    public static Color HeaderText => Active.HeaderText;

    /// <summary>
    /// Whether grid headers should be painted by the OS visual style.
    /// </summary>
    /// <remarks>
    /// The OS header painter ignores the dark colour mode, so the dark palette turns it off and
    /// uses <see cref="HeaderBackground"/> / <see cref="HeaderText"/>; the light palette keeps the
    /// themed headers the app has always shown.
    /// </remarks>
    public static bool SystemHeaders => Active.SystemHeaders;

    /// <summary>
    /// The ink colour of the monochrome logo artwork: black on the light palette, light on the dark
    /// one, so the pen-square stays visible on either background.
    /// </summary>
    public static Color Artwork => Active.Artwork;

    /// <summary>
    /// Whether links keep the system's colours.
    /// </summary>
    /// <remarks>
    /// The system link colours (dark blue, red while active, purple once visited) are unreadable on
    /// the dark palette, so the dark mode supplies <see cref="LinkText"/>, <see cref="LinkActive"/>
    /// and <see cref="LinkVisited"/> instead; the light mode leaves the system choice untouched.
    /// </remarks>
    public static bool SystemLinkColours => Active.SystemLinkColours;

    /// <summary>
    /// The colour of a link at rest.
    /// </summary>
    public static Color LinkText => Active.LinkText;

    /// <summary>
    /// The colour of a link while the pointer is over it.
    /// </summary>
    public static Color LinkActive => Active.LinkActive;

    /// <summary>
    /// The colour of a link that has already been followed.
    /// </summary>
    public static Color LinkVisited => Active.LinkVisited;

    /// <summary>
    /// The text colour used on a selected row, chosen for contrast against its selection background.
    /// </summary>
    public static Color SelectionText => Active.SelectionText;

    /// <summary>
    /// The colour for secondary text: a disabled or unchanged file entry, or a segment that is not a path.
    /// </summary>
    public static Color MutedText => Active.MutedText;

    /// <summary>
    /// The background of the read-only banner above the Machine grid.
    /// </summary>
    public static Color InfoBackground => Active.InfoBackground;

    /// <summary>
    /// The backgrounds of one grid row: its state colour, or the corrupted overlay when it applies.
    /// </summary>
    /// <param name="state">The row's edit state.</param>
    /// <param name="isCorrupted">Whether the corrupted-path overlay marks the row.</param>
    /// <returns>The normal and selected backgrounds for the row.</returns>
    public static RowColors Row(EnvironmentalVariableState state, bool isCorrupted)
    {
        if (isCorrupted)
            return Active.Corrupted;

        return state switch
        {
            EnvironmentalVariableState.Added => Active.Added,
            EnvironmentalVariableState.Modified => Active.Modified,
            EnvironmentalVariableState.Deleted => Active.Deleted,
            _ => Active.Unchanged,
        };
    }

    /// <summary>
    /// The text colour of one PATH segment verdict.
    /// </summary>
    /// <param name="verdict">The segment's existence verdict.</param>
    /// <returns>The colour for the existence cell.</returns>
    public static Color SegmentText(PathSegmentState verdict) => verdict switch
    {
        PathSegmentState.Exists => Active.SegmentExists,
        PathSegmentState.Missing => Active.SegmentMissing,
        _ => Active.SegmentNotPath,
    };

    /// <summary>
    /// The background of one file-entry status cell in the import/export review window.
    /// </summary>
    /// <param name="status">The entry's comparison, or null on an export review.</param>
    /// <returns>The status background; entries that are not carried use the grid background.</returns>
    public static Color FileStatusBackground(VariableFileEntryStatus? status) => status switch
    {
        VariableFileEntryStatus.New => Active.FileStatusNew,
        VariableFileEntryStatus.Overwrites => Active.FileStatusOverwrites,
        _ => Active.FileStatusDefault,
    };

    /// <summary>
    /// The background and selection background of one grid row.
    /// </summary>
    /// <param name="Background">The row's normal background.</param>
    /// <param name="SelectionBackground">The background while the row is selected.</param>
    public readonly record struct RowColors(Color Background, Color SelectionBackground);

    /// <summary>
    /// One colour mode's colours; a palette is complete, so a surface never falls back to a literal.
    /// </summary>
    private sealed class Palette
    {
        public required Color GridBackground { get; init; }
        public required Color CellText { get; init; }
        public required Color GridLines { get; init; }
        public required Color HeaderBackground { get; init; }
        public required Color HeaderText { get; init; }
        public required bool SystemHeaders { get; init; }
        public required Color Artwork { get; init; }
        public required bool SystemLinkColours { get; init; }
        public required Color LinkText { get; init; }
        public required Color LinkActive { get; init; }
        public required Color LinkVisited { get; init; }
        public required Color SelectionText { get; init; }
        public required Color MutedText { get; init; }
        public required Color InfoBackground { get; init; }
        public required RowColors Unchanged { get; init; }
        public required RowColors Added { get; init; }
        public required RowColors Modified { get; init; }
        public required RowColors Deleted { get; init; }
        public required RowColors Corrupted { get; init; }
        public required Color SegmentExists { get; init; }
        public required Color SegmentMissing { get; init; }
        public required Color SegmentNotPath { get; init; }
        public required Color FileStatusNew { get; init; }
        public required Color FileStatusOverwrites { get; init; }
        public required Color FileStatusDefault { get; init; }
    }
}
