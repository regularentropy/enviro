namespace enviro.Static;

/// <summary>
/// Static helper class for displaying dialog messages.
/// </summary>
internal static class DialogHelper
{
    /// <summary>
    /// Displays an information dialog with an information icon.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">The title of the dialog. Defaults to "Information".</param>
    public static void ShowInfo(string message, string title = "Information") =>
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);

    /// <summary>
    /// Displays a warning dialog with a warning icon.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">The title of the dialog. Defaults to "Warning".</param>
    public static void ShowWarning(string message, string title = "Warning") =>
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    /// <summary>
    /// Displays an error dialog with an error icon.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">The title of the dialog. Defaults to "Error".</param>
    public static void ShowError(string message, string title = "Error") =>
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);

    /// <summary>
    /// Displays a confirmation dialog with OK and Cancel buttons.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">The title of the dialog. Defaults to "Confirm".</param>
    /// <returns>The result of the user's choice.</returns>
    public static DialogResult ShowConfirm(string message, string title = "Confirm") =>
        MessageBox.Show(message, title, MessageBoxButtons.OKCancel, MessageBoxIcon.Question);

    /// <summary>
    /// Displays a simple message dialog without an icon.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">The title of the dialog. Defaults to "Message".</param>
    public static void ShowMessage(string message, string title = "Message") =>
        MessageBox.Show(message, title, MessageBoxButtons.OK);
}
