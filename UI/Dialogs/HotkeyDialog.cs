using System.Windows;
using System.Windows.Input;

namespace DisplayHub.UI.Dialogs;

/// <summary>
/// WPF dialog for capturing a hotkey combination.
/// </summary>
public class HotkeyDialog : Window
{
    private readonly System.Windows.Controls.TextBox textBox;

    public int ResultVkCode { get; private set; }
    public int ResultModifiers { get; private set; }

    public HotkeyDialog(int currentVk = 0, int currentModifiers = 0)
    {
        Title = "Set Hotkey";
        Width = 340;
        Height = 180;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.ToolWindow;

        var stack = new System.Windows.Controls.StackPanel { Margin = new Thickness(16) };

        var label = new System.Windows.Controls.TextBlock
        {
            Text = "Press a key combination:",
            Margin = new Thickness(0, 0, 0, 8)
        };

        textBox = new System.Windows.Controls.TextBox
        {
            IsReadOnly = true,
            Height = 28,
            Margin = new Thickness(0, 0, 0, 16)
        };

        if (currentVk != 0)
            textBox.Text = $"Modifier+VK{currentVk}";

        textBox.PreviewKeyDown += TextBox_PreviewKeyDown;

        var buttonPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };

        var okButton = new System.Windows.Controls.Button
        {
            Content = "OK",
            Width = 80,
            Margin = new Thickness(0, 0, 8, 0)
        };
        okButton.Click += (s, e) => { DialogResult = true; };

        var cancelButton = new System.Windows.Controls.Button
        {
            Content = "Cancel",
            Width = 80,
            IsCancel = true
        };

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);

        stack.Children.Add(label);
        stack.Children.Add(textBox);
        stack.Children.Add(buttonPanel);

        Content = stack;
    }

    private void TextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;

        // Build modifier mask using WinForms-compatible values
        int modifiers = 0;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= 0x20000; // Keys.Control
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= 0x40000;     // Keys.Alt
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= 0x10000;   // Keys.Shift

        // Convert WPF Key to Win32 VK code
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        int vk = KeyInterop.VirtualKeyFromKey(key);

        // Ignore modifier-only keys
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin)
            return;

        if (modifiers != 0 && vk != 0)
        {
            ResultVkCode = vk;
            ResultModifiers = modifiers;

            string modText = "";
            if ((modifiers & 0x20000) != 0) modText += "Ctrl+";
            if ((modifiers & 0x40000) != 0) modText += "Alt+";
            if ((modifiers & 0x10000) != 0) modText += "Shift+";
            textBox.Text = $"{modText}{key}";
        }
    }
}
