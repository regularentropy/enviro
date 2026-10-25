using System.Drawing.Imaging;

namespace enviro.Static;

/// <summary>
/// Renders the app's monochrome logo in the active theme's ink colour.
/// </summary>
/// <remarks>
/// The logo ships as black artwork with a transparent background, which disappears on the dark
/// palette. Recolouring it keeps the alpha mask, so the anti-aliased edges of the pen-square stay
/// smooth instead of turning into a hard silhouette.
/// </remarks>
internal static class ThemeArtwork
{
    /// <summary>
    /// Returns a tinted copy of a monochrome, alpha-blended image drawn in <see cref="ThemePalette.Artwork"/>.
    /// </summary>
    /// <param name="source">The dark artwork to recolour; it is not modified.</param>
    /// <returns>A new bitmap with the palette's ink colour and the source's alpha channel.</returns>
    public static Bitmap Tint(Image source)
    {
        var ink = ThemePalette.Artwork;
        var tinted = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);

        using var graphics = Graphics.FromImage(tinted);
        using var attributes = new ImageAttributes();

        // Every visible pixel takes the ink colour; only the source's alpha survives. The
        // translation row carries the colour (0..1), which is why it is the last row.
        attributes.SetColorMatrix(new ColorMatrix(
        [
            new float[] { 0, 0, 0, 0, 0 },
            new float[] { 0, 0, 0, 0, 0 },
            new float[] { 0, 0, 0, 0, 0 },
            new float[] { 0, 0, 0, 1, 0 },
            new float[] { ink.R / 255f, ink.G / 255f, ink.B / 255f, 0, 1 },
        ]));

        graphics.DrawImage(
            source,
            new Rectangle(0, 0, source.Width, source.Height),
            0,
            0,
            source.Width,
            source.Height,
            GraphicsUnit.Pixel,
            attributes);

        return tinted;
    }
}
