namespace enviro.Static;

internal static class DialogHelper
{
    public static void ShowInfo(string message, string title = "Information") =>
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static void ShowWarning(string message, string title = "Warning") =>
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static void ShowError(string message, string title = "Error") =>
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);

    public static DialogResult ShowConfirm(string message, string title = "Confirm") =>
        MessageBox.Show(message, title, MessageBoxButtons.OKCancel, MessageBoxIcon.Question);

    public static void ShowMessage(string message, string title = "Message") =>
        MessageBox.Show(message, title, MessageBoxButtons.OK);
}
