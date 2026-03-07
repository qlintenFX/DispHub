using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace DisplayHub.UI.Dialogs;

public partial class HotkeyDialog : FluentWindow
{
    public int ResultVkCode { get; private set; }
    public int ResultModifiers { get; private set; }

    public HotkeyDialog(int currentVk = 0, int currentModifiers = 0)
    {
        InitializeComponent();

        if (currentVk != 0)
        {
            ResultVkCode = currentVk;
            ResultModifiers = currentModifiers;
            HotkeyTextBox.Text = BuildHotkeyText(currentVk, currentModifiers);
        }

        Loaded += (_, _) => HotkeyTextBox.Focus();
    }

    private void HotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
    }

    private void HotkeyTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignore standalone modifier keys
        if (key is Key.LeftCtrl or Key.RightCtrl or
                   Key.LeftAlt or Key.RightAlt or
                   Key.LeftShift or Key.RightShift or
                   Key.LWin or Key.RWin)
            return;

        int modifiers = 0;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= 0x20000;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))     modifiers |= 0x40000;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))   modifiers |= 0x10000;

        int vk = KeyInterop.VirtualKeyFromKey(key);

        if (modifiers == 0 || vk == 0)
        {
            ErrorText.Text = "A modifier key (Ctrl, Alt, or Shift) is required.";
            ErrorText.Visibility = Visibility.Visible;
            HotkeyTextBox.Text = string.Empty;
            ResultVkCode = 0;
            ResultModifiers = 0;
            return;
        }

        ErrorText.Visibility = Visibility.Collapsed;
        ResultVkCode = vk;
        ResultModifiers = modifiers;
        HotkeyTextBox.Text = BuildHotkeyText(vk, modifiers);
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        ResultVkCode = 0;
        ResultModifiers = 0;
        HotkeyTextBox.Text = string.Empty;
        ErrorText.Visibility = Visibility.Collapsed;
        DialogResult = true;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private static string BuildHotkeyText(int vk, int modifiers)
    {
        string parts = string.Empty;
        if ((modifiers & 0x20000) != 0) parts += "Ctrl+";
        if ((modifiers & 0x40000) != 0) parts += "Alt+";
        if ((modifiers & 0x10000) != 0) parts += "Shift+";

        Key key = KeyInterop.KeyFromVirtualKey(vk);
        string keyName = key switch
        {
            Key.D0 => "0", Key.D1 => "1", Key.D2 => "2", Key.D3 => "3", Key.D4 => "4",
            Key.D5 => "5", Key.D6 => "6", Key.D7 => "7", Key.D8 => "8", Key.D9 => "9",
            Key.Prior => "PageUp", Key.Next => "PageDown",
            Key.Return => "Enter",
            Key.OemPeriod => ".", Key.OemComma => ",",
            _ => key.ToString()
        };

        return parts + keyName;
    }
}
