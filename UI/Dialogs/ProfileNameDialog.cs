using System.Windows;

namespace DisplayHub.UI.Dialogs;

/// <summary>
/// WPF dialog for entering a profile name.
/// </summary>
public static class ProfileNameDialog
{
    public static string? Show(Window? owner, string defaultName = "New Profile", string title = "New Profile")
    {
        var dialog = new Window
        {
            Title = title,
            Width = 380,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = owner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow
        };

        var grid = new System.Windows.Controls.Grid();
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

        var inputPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            Margin = new Thickness(16, 20, 16, 16)
        };

        var label = new System.Windows.Controls.TextBlock
        {
            Text = "Profile Name:",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };

        var textBox = new System.Windows.Controls.TextBox
        {
            Width = 220,
            Text = defaultName,
            VerticalAlignment = VerticalAlignment.Center
        };
        textBox.SelectAll();

        inputPanel.Children.Add(label);
        inputPanel.Children.Add(textBox);
        System.Windows.Controls.Grid.SetRow(inputPanel, 0);
        grid.Children.Add(inputPanel);

        var buttonPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(16, 0, 16, 16)
        };

        var okButton = new System.Windows.Controls.Button
        {
            Content = "OK",
            Width = 80,
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true
        };

        var cancelButton = new System.Windows.Controls.Button
        {
            Content = "Cancel",
            Width = 80,
            IsCancel = true
        };

        bool confirmed = false;
        okButton.Click += (s, e) => { confirmed = true; dialog.Close(); };

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        System.Windows.Controls.Grid.SetRow(buttonPanel, 1);
        grid.Children.Add(buttonPanel);

        dialog.Content = grid;
        dialog.ShowDialog();

        if (!confirmed) return null;

        string name = textBox.Text.Trim();
        return string.IsNullOrEmpty(name) ? defaultName : name;
    }
}
