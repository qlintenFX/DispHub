using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DispHub.Core.Hotkeys;

namespace DispHub.Controls;

/// <summary>
/// Click, then press a key combination to record a hotkey. Esc cancels, Backspace or Delete clears.
/// While recording, global hotkeys are released (see <see cref="RecordingChanged"/>) so the combination reaches us.
/// </summary>
public sealed class HotkeyBox : Button
{
    public static readonly DependencyProperty GestureProperty = DependencyProperty.Register(
        nameof(Gesture), typeof(string), typeof(HotkeyBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((HotkeyBox)d).UpdateText()));

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder), typeof(string), typeof(HotkeyBox), new PropertyMetadata("Not set", (d, _) => ((HotkeyBox)d).UpdateText()));

    private string? _hint;
    private bool _recording;

    public HotkeyBox()
    {
        MinWidth = 150;
        HorizontalContentAlignment = HorizontalAlignment.Left;
        Loaded += (_, _) => UpdateText();
        LostKeyboardFocus += (_, _) => StopRecording();
        UpdateText();
    }

    /// <summary>Raised with true when any HotkeyBox starts recording and false when it stops.</summary>
    public static event EventHandler<bool>? RecordingChanged;

    public string? Gesture
    {
        get => (string?)GetValue(GestureProperty);
        set => SetValue(GestureProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    protected override void OnClick()
    {
        base.OnClick();
        if (_recording)
            StopRecording();
        else
            StartRecording();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (!_recording)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        e.Handled = true;
        Key key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            Key.DeadCharProcessed => e.DeadCharProcessedKey,
            _ => e.Key,
        };

        if (key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
        {
            StopRecording();
            return;
        }

        if (key is Key.Back or Key.Delete && Keyboard.Modifiers == ModifierKeys.None)
        {
            StopRecording();
            Gesture = null;
            return;
        }

        int virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (KeyNames.IsModifierKey(virtualKey))
            return;

        var gesture = new HotkeyGesture(ToModifiers(Keyboard.Modifiers), virtualKey);
        if (!gesture.IsValid)
        {
            _hint = "Add Ctrl, Alt or Win";
            UpdateText();
            return;
        }

        // Resume global hotkeys first so the new one is registered (and conflicts reported) normally.
        StopRecording();
        Gesture = gesture.ToString();
    }

    private void StartRecording()
    {
        _recording = true;
        _hint = null;
        Focus();
        RecordingChanged?.Invoke(this, true);
        UpdateText();
    }

    private void StopRecording()
    {
        if (!_recording)
            return;
        _recording = false;
        _hint = null;
        RecordingChanged?.Invoke(this, false);
        UpdateText();
    }

    private void UpdateText() =>
        Content = _recording ? _hint ?? "Press a key combination…" : Gesture ?? Placeholder;

    private static HotkeyModifiers ToModifiers(ModifierKeys keys)
    {
        var result = HotkeyModifiers.None;
        if (keys.HasFlag(ModifierKeys.Control))
            result |= HotkeyModifiers.Control;
        if (keys.HasFlag(ModifierKeys.Alt))
            result |= HotkeyModifiers.Alt;
        if (keys.HasFlag(ModifierKeys.Shift))
            result |= HotkeyModifiers.Shift;
        if (keys.HasFlag(ModifierKeys.Windows))
            result |= HotkeyModifiers.Win;
        return result;
    }
}
