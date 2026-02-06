namespace KeyedColors.UI.Dialogs;

/// <summary>
/// Reusable dialog for entering a profile name.
/// </summary>
public static class ProfileNameDialog
{
    /// <summary>
    /// Shows a modal dialog prompting the user for a profile name.
    /// Returns the entered name, or null if the user cancelled.
    /// </summary>
    public static string? Show(IWin32Window owner, string defaultName = "New Profile", string title = "New Profile")
    {
        using var form = new Form
        {
            Text = title,
            ClientSize = new Size(350, 100),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var label = new Label
        {
            Left = 20,
            Top = 20,
            Text = "Profile Name:",
            AutoSize = true
        };

        var textBox = new TextBox
        {
            Left = 120,
            Top = 20,
            Width = 200,
            Text = defaultName
        };

        var confirmButton = new Button
        {
            Text = "OK",
            Left = 140,
            Width = 80,
            Top = 60,
            DialogResult = DialogResult.OK
        };

        form.AcceptButton = confirmButton;
        form.Controls.Add(label);
        form.Controls.Add(textBox);
        form.Controls.Add(confirmButton);

        if (form.ShowDialog(owner) != DialogResult.OK)
            return null;

        string name = textBox.Text.Trim();
        return string.IsNullOrEmpty(name) ? defaultName : name;
    }
}
