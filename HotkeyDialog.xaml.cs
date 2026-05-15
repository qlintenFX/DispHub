// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Input;
using DispHub.Constants;
using Wpf.Ui.Controls;

namespace DispHub;

public partial class HotkeyDialog : FluentWindow
{
    public int VirtualKeyCode { get; private set; }
    public uint Modifiers { get; private set; }

    public HotkeyDialog()
    {
        InitializeComponent();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        e.Handled = true;

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignore modifier-only keys
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin)
            return;

        uint modifiers = 0;
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            modifiers |= AppConstants.MOD_CONTROL;
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            modifiers |= AppConstants.MOD_ALT;
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            modifiers |= AppConstants.MOD_SHIFT;

        VirtualKeyCode = KeyInterop.VirtualKeyFromKey(key);
        Modifiers = modifiers;

        var parts = new List<string>();
        if ((modifiers & AppConstants.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((modifiers & AppConstants.MOD_ALT) != 0) parts.Add("Alt");
        if ((modifiers & AppConstants.MOD_SHIFT) != 0) parts.Add("Shift");
        parts.Add(key.ToString());
        HotkeyText.Text = string.Join(" + ", parts);

        OkButton.IsEnabled = true;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        VirtualKeyCode = 0;
        Modifiers = 0;
        DialogResult = true;
    }
}
