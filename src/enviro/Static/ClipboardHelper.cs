using enviro.Models;

namespace enviro.Static;

/// <summary>
/// Static helper class for clipboard operations with environmental variable models.
/// </summary>
internal static class ClipboardHelper
{
    /// <summary>
    /// Copies plain text to the clipboard, ignoring a clipboard held open by another process.
    /// </summary>
    /// <param name="text">The text to copy.</param>
    public static void CopyText(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Another process holds the clipboard open: an environmental condition, not an app error.
        }
    }

    /// <summary>
    /// Formats rows as NAME=value lines, one per row, for pasting into scripts or files.
    /// </summary>
    /// <param name="models">The rows to format, in the order they should appear.</param>
    /// <returns>The lines joined by the platform newline.</returns>
    public static string FormatNameValueLines(IEnumerable<EnvModel> models) =>
        string.Join(Environment.NewLine, models.Select(m => $"{m.Name}={m.Path}"));

    /// <summary>
    /// Formats rows as bare values, one per line — what the context menu's "Copy value" puts
    /// on the clipboard for a multi-selection.
    /// </summary>
    public static string FormatValueLines(IEnumerable<EnvModel> models) =>
        string.Join(Environment.NewLine, models.Select(m => m.Path));

    /// <summary>
    /// Formats rows as bare names, one per line — what the context menu's "Copy name" puts
    /// on the clipboard for a multi-selection.
    /// </summary>
    public static string FormatNameLines(IEnumerable<EnvModel> models) =>
        string.Join(Environment.NewLine, models.Select(m => m.Name));

    /// <summary>
    /// Formats a selection the way Ctrl+C copies it: one row copies the bare value,
    /// several rows copy NAME=value lines.
    /// </summary>
    /// <param name="models">The selected rows, in row order.</param>
    public static string FormatValueSelection(IReadOnlyList<EnvModel> models) =>
        models.Count == 1 ? models[0].Path : FormatNameValueLines(models);

    /// <summary>
    /// Formats a selection the way Ctrl+Shift+C copies it: one row copies the bare name,
    /// several rows copy one name per line.
    /// </summary>
    /// <param name="models">The selected rows, in row order.</param>
    public static string FormatNameSelection(IReadOnlyList<EnvModel> models) =>
        models.Count == 1 ? models[0].Name : FormatNameLines(models);
}
