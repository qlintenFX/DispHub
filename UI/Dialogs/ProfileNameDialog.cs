using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace DisplayHub.UI.Dialogs;

public partial class ProfileNameDialog : FluentWindow
{
    public string? ResultName { get; private set; }

    public ProfileNameDialog(string defaultName = "New Profile")
    {
        InitializeComponent();
        NameTextBox.Text = defaultName;
        Loaded += (_, _) =>
        {
            NameTextBox.Focus();
            NameTextBox.SelectAll();
        };
    }

    private void OkButton_Click(object sender, RoutedEventArgs e) => Confirm();

    private void NameTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Return) Confirm();
    }

    private void Confirm()
    {
        string name = NameTextBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;
        ResultName = name;
        DialogResult = true;
    }

    /// <summary>Convenience helper — shows the dialog and returns the entered name, or null if cancelled.</summary>
    public static string? Show(Window? owner, string defaultName = "New Profile", string title = "New Profile")
    {
        var dialog = new ProfileNameDialog(defaultName) { Title = title, Owner = owner };
        return dialog.ShowDialog() == true ? dialog.ResultName : null;
    }
}
