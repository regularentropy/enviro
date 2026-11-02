using System.Drawing;

namespace enviro.Forms;

/// <summary>
/// Themed replacement for <see cref="MessageBox"/>.
/// </summary>
/// <remarks>
/// A Win32 message box is painted by the OS and ignores the colour mode the application applied at
/// startup, so it stayed light on a dark window. This form is made of standard controls only, which
/// WinForms does theme, and it returns the same <see cref="DialogResult"/> values a message box
/// would, so <see cref="Static.DialogHelper"/> can swap one for the other without the callers
/// noticing.
/// </remarks>
internal sealed class MessageForm : Form
{
    /// <summary>One bitmap per stock icon, created once and shared by every message.</summary>
    private static readonly Dictionary<MessageBoxIcon, Image> Glyphs = new()
    {
        [MessageBoxIcon.Information] = SystemIcons.Information.ToBitmap(),
        [MessageBoxIcon.Warning] = SystemIcons.Warning.ToBitmap(),
        [MessageBoxIcon.Error] = SystemIcons.Error.ToBitmap(),
        [MessageBoxIcon.Question] = SystemIcons.Question.ToBitmap(),
    };

    /// <summary>
    /// Builds a message dialog.
    /// </summary>
    /// <param name="message">The text to show; a line break starts a new line.</param>
    /// <param name="title">The window title, which is also what a screen reader announces first.</param>
    /// <param name="icon">The stock icon, or <see cref="MessageBoxIcon.None"/> for no icon.</param>
    /// <param name="buttons">
    /// The button set. <see cref="MessageBoxButtons.OK"/>, <see cref="MessageBoxButtons.OKCancel"/> and
    /// <see cref="MessageBoxButtons.YesNoCancel"/> are the sets the application uses; anything else
    /// falls back to a single OK.
    /// </param>
    public MessageForm(string message, string title, MessageBoxIcon icon, MessageBoxButtons buttons)
    {
        Text = title;
        AccessibleRole = AccessibleRole.Dialog;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);
        KeyPreview = true;

        var body = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Dock = DockStyle.Fill,
        };

        if (Glyphs.TryGetValue(icon, out var glyph))
        {
            body.Controls.Add(new PictureBox
            {
                Image = glyph,
                Size = new Size(32, 32),
                SizeMode = PictureBoxSizeMode.Zoom,
                Margin = new Padding(0, 0, 12, 0),
                TabStop = false,
            }, 0, 0);
        }

        body.Controls.Add(new Label
        {
            Text = message,
            AutoSize = true,
            MaximumSize = new Size(420, 0),
            UseMnemonic = false,
            Margin = new Padding(0),
        }, 1, 0);

        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0, 16, 0, 0),
        };

        Button? accept = null;
        Button? cancel = null;
        foreach (var (text, result) in Choices(buttons).Reverse())
        {
            var button = new Button
            {
                Text = text,
                DialogResult = result,
                AutoSize = true,
                MinimumSize = new Size(75, 23),
                Margin = new Padding(6, 0, 0, 0),
            };

            row.Controls.Add(button);

            if (result is DialogResult.OK or DialogResult.Yes)
                accept = button;
            else if (result == DialogResult.Cancel)
                cancel = button;
        }

        // Enter picks the affirmative button and Esc the negative one; a dialog with a single button
        // answers both, the way a message box does.
        AcceptButton = accept ?? (Button)row.Controls[0];
        CancelButton = cancel ?? (Button)row.Controls[0];

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Dock = DockStyle.Fill,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(body, 0, 0);
        layout.Controls.Add(row, 0, 1);
        Controls.Add(layout);
    }

    /// <summary>
    /// The buttons to build for one message-box button set, in reading order. The caller adds them in
    /// reverse, because the row flows right to left so the leftmost button is the affirmative one, as
    /// in a message box.
    /// </summary>
    private static (string Text, DialogResult Result)[] Choices(MessageBoxButtons buttons) => buttons switch
    {
        MessageBoxButtons.OKCancel => [("OK", DialogResult.OK), ("Cancel", DialogResult.Cancel)],
        MessageBoxButtons.YesNoCancel =>
        [
            ("Yes", DialogResult.Yes),
            ("No", DialogResult.No),
            ("Cancel", DialogResult.Cancel),
        ],
        _ => [("OK", DialogResult.OK)],
    };
}
