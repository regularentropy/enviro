using enviro.Forms;

namespace enviro.Static;

/// <summary>
/// Static helper class for displaying dialog messages.
/// </summary>
/// <remarks>
/// Every message goes through <see cref="MessageForm"/> rather than a Win32 message box: the OS paints
/// a message box itself and ignores the colour mode the application applied at startup, so it would
/// stay light on a dark window. The members below keep the message-box shapes their callers use.
/// </remarks>
internal static class DialogHelper
{
    /// <summary>
    /// Displays an information dialog with an information icon.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">The title of the dialog. Defaults to "Information".</param>
    public static void ShowInfo(string message, string title = "Information") =>
        Show(message, title, MessageBoxIcon.Information, MessageBoxButtons.OK);

    /// <summary>
    /// Displays a warning dialog with a warning icon.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">The title of the dialog. Defaults to "Warning".</param>
    public static void ShowWarning(string message, string title = "Warning") =>
        Show(message, title, MessageBoxIcon.Warning, MessageBoxButtons.OK);

    /// <summary>
    /// Displays an error dialog with an error icon.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">The title of the dialog. Defaults to "Error".</param>
    public static void ShowError(string message, string title = "Error") =>
        Show(message, title, MessageBoxIcon.Error, MessageBoxButtons.OK);

    /// <summary>
    /// Displays a confirmation dialog with OK and Cancel buttons.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">The title of the dialog. Defaults to "Confirm".</param>
    /// <returns>The result of the user's choice.</returns>
    public static DialogResult ShowConfirm(string message, string title = "Confirm") =>
        Show(message, title, MessageBoxIcon.Question, MessageBoxButtons.OKCancel);

    /// <summary>
    /// Asks what to do with unapplied changes when the window is closing.
    /// </summary>
    /// <param name="count">The number of changes that have not been applied.</param>
    /// <returns>
    /// <see cref="DialogResult.Yes"/> to apply and close,
    /// <see cref="DialogResult.No"/> to discard and close, or
    /// <see cref="DialogResult.Cancel"/> to stay open.
    /// </returns>
    public static DialogResult ShowUnsavedChanges(int count) =>
        Show(
            $"{count} {(count == 1 ? "change has" : "changes have")} not been applied.\n\n" +
            "Yes: apply the changes and close\n" +
            "No: discard the changes and close\n" +
            "Cancel: keep working",
            "Unapplied changes",
            MessageBoxIcon.Warning,
            MessageBoxButtons.YesNoCancel);

    /// <summary>
    /// Displays a simple message dialog without an icon.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">The title of the dialog. Defaults to "Message".</param>
    public static void ShowMessage(string message, string title = "Message") =>
        Show(message, title, MessageBoxIcon.None, MessageBoxButtons.OK);

    /// <summary>
    /// Shows one message dialog, owned by the window that is currently active so it stays modal to it.
    /// </summary>
    private static DialogResult Show(string message, string title, MessageBoxIcon icon, MessageBoxButtons buttons) =>
        new MessageForm(message, title, icon, buttons).ShowDialog(Form.ActiveForm);
}
