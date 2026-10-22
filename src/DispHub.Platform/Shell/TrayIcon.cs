// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Drawing;
using System.Runtime.InteropServices;
using DispHub.Core.Imaging;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace DispHub.Platform.Shell;

public enum TrayIconAction
{
    /// <summary>Left click, or Enter/Space on the focused icon.</summary>
    Select,
    DoubleClick,
    /// <summary>Right click, or the context menu key.</summary>
    ContextMenu,
    /// <summary>The user clicked a notification shown with <see cref="TrayIcon.ShowNotification"/>.</summary>
    NotificationClicked,
}

/// <summary>
/// DispHub's icon in the notification area. Uses the frame drawn for the exact icon size of the current DPI, so it
/// stays crisp, checks every shell call, and can be re-added after Explorer restarts.
/// </summary>
/// <remarks>
/// Uses NOTIFYICON_VERSION_4, so clicks arrive as NIN_SELECT and WM_CONTEXTMENU with the anchor point in wParam.
/// Messages arrive at the owner window; pass them to <see cref="HandleMessage"/>.
/// </remarks>
public sealed class TrayIcon : IDisposable
{
    /// <summary>The window message the shell sends icon events with.</summary>
    public const uint CallbackMessage = PInvoke.WM_APP + 1;

    private const uint IconId = 1;
    private const uint NIN_SELECT = 0x0400;
    private const uint NIN_KEYSELECT = 0x0401;
    private const uint NIN_BALLOONUSERCLICK = 0x0405;

    private readonly HWND _owner;
    private readonly ILogger _logger;
    private byte[] _icoFile;
    private HICON _icon;
    private string _tooltip = "DispHub";
    private bool _added;

    public TrayIcon(nint ownerWindow, byte[] icoFile, ILogger logger)
    {
        _owner = (HWND)ownerWindow;
        _icoFile = icoFile;
        _logger = logger;
    }

    public event EventHandler<TrayIconAction>? Action;

    public bool IsAdded => _added;

    /// <summary>Adds the icon, or adds it again after Explorer restarted. Returns false if the shell refused.</summary>
    public bool Show()
    {
        Delete();
        LoadIcon();
        NOTIFYICONDATAW data = Data(NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON |
            NOTIFY_ICON_DATA_FLAGS.NIF_TIP | NOTIFY_ICON_DATA_FLAGS.NIF_SHOWTIP);
        if (!PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, in data))
        {
            _logger.LogWarning("The notification area refused DispHub's icon (error {Error})", Marshal.GetLastPInvokeError());
            return false;
        }
        data.Anonymous.uVersion = PInvoke.NOTIFYICON_VERSION_4;
        if (!PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_SETVERSION, in data))
            _logger.LogWarning("The notification area didn't accept version 4 callbacks");
        _added = true;
        return true;
    }

    public void SetTooltip(string text)
    {
        _tooltip = text.Length > 127 ? text[..127] : text;
        Modify(NOTIFY_ICON_DATA_FLAGS.NIF_TIP | NOTIFY_ICON_DATA_FLAGS.NIF_SHOWTIP);
    }

    /// <summary>Switches to another icon, e.g. the paused one.</summary>
    public void SetIcon(byte[] icoFile)
    {
        _icoFile = icoFile;
        RefreshIcon();
    }

    /// <summary>Reloads the icon at the current DPI, e.g. after the scale changed.</summary>
    public void RefreshIcon()
    {
        LoadIcon();
        Modify(NOTIFY_ICON_DATA_FLAGS.NIF_ICON);
    }

    /// <summary>Shows a Windows notification from the icon; clicking it raises <see cref="TrayIconAction.NotificationClicked"/>.</summary>
    public void ShowNotification(string title, string text)
    {
        NOTIFYICONDATAW data = Data(NOTIFY_ICON_DATA_FLAGS.NIF_INFO);
        data.szInfoTitle = title.Length > 63 ? title[..63] : title;
        data.szInfo = text.Length > 255 ? text[..255] : text;
        data.dwInfoFlags = NOTIFY_ICON_INFOTIP_FLAGS.NIIF_USER | NOTIFY_ICON_INFOTIP_FLAGS.NIIF_LARGE_ICON;
        data.hBalloonIcon = _icon;
        if (_added)
            PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, in data);
    }

    /// <summary>Where the icon is on screen, in physical pixels; null when it's hidden in the overflow area.</summary>
    public unsafe Rectangle? GetBounds()
    {
        var id = new NOTIFYICONIDENTIFIER { cbSize = (uint)sizeof(NOTIFYICONIDENTIFIER), hWnd = _owner, uID = IconId };
        if (PInvoke.Shell_NotifyIconGetRect(in id, out RECT rect).Failed)
            return null;
        var bounds = Rectangle.FromLTRB(rect.left, rect.top, rect.right, rect.bottom);
        return bounds.Width > 0 ? bounds : null;
    }

    /// <summary>Call from the owner's window procedure. Returns true for icon messages.</summary>
    public bool HandleMessage(uint message, nuint wParam, nint lParam)
    {
        if (message != CallbackMessage)
            return false;
        uint notification = (uint)(lParam & 0xFFFF);
        TrayIconAction? action = notification switch
        {
            NIN_SELECT or NIN_KEYSELECT => TrayIconAction.Select,
            PInvoke.WM_LBUTTONDBLCLK => TrayIconAction.DoubleClick,
            PInvoke.WM_CONTEXTMENU => TrayIconAction.ContextMenu,
            NIN_BALLOONUSERCLICK => TrayIconAction.NotificationClicked,
            _ => null,
        };
        if (action is { } a)
            Action?.Invoke(this, a);
        return true;
    }

    public void Dispose()
    {
        Delete();
        DestroyIcon();
    }

    private void Delete()
    {
        if (!_added)
            return;
        NOTIFYICONDATAW data = Data(0);
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, in data);
        _added = false;
    }

    private void Modify(NOTIFY_ICON_DATA_FLAGS flags)
    {
        if (!_added)
            return;
        NOTIFYICONDATAW data = Data(flags);
        if (!PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, in data))
            _logger.LogDebug("Couldn't update the notification icon");
    }

    private unsafe NOTIFYICONDATAW Data(NOTIFY_ICON_DATA_FLAGS flags) => new()
    {
        cbSize = (uint)sizeof(NOTIFYICONDATAW),
        hWnd = _owner,
        uID = IconId,
        uFlags = flags,
        uCallbackMessage = CallbackMessage,
        hIcon = _icon,
        szTip = _tooltip,
    };

    private unsafe void LoadIcon()
    {
        uint dpi = PInvoke.GetDpiForWindow(_owner);
        int size = PInvoke.GetSystemMetricsForDpi(SYSTEM_METRICS_INDEX.SM_CXSMICON, dpi == 0 ? 96 : dpi);
        if (IcoFile.Pick(IcoFile.ReadFrames(_icoFile), size) is not { } frame)
            return;
        HICON icon;
        fixed (byte* bits = &_icoFile[frame.Offset])
        {
            icon = PInvoke.CreateIconFromResourceEx(bits, (uint)frame.Length, true, 0x00030000, size, size, IMAGE_FLAGS.LR_DEFAULTCOLOR);
        }
        if (icon.IsNull)
        {
            _logger.LogWarning("Couldn't create the {Size} px notification icon", size);
            return;
        }
        DestroyIcon();
        _icon = icon;
    }

    private void DestroyIcon()
    {
        if (_icon.IsNull)
            return;
        PInvoke.DestroyIcon(_icon);
        _icon = default;
    }

    /// <summary>
    /// Whether the user keeps DispHub's icon on the taskbar rather than in the hidden icons (^). Windows 11 hides new
    /// icons by default and keeps this choice per app in the registry; null when Windows hasn't recorded it yet.
    /// </summary>
    public static bool? IsOnTaskbar()
    {
        string? exe = Environment.ProcessPath;
        if (exe is null)
            return null;
        try
        {
            using Microsoft.Win32.RegistryKey? settings = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings");
            if (settings is null)
                return null;
            foreach (string name in settings.GetSubKeyNames())
            {
                using Microsoft.Win32.RegistryKey? icon = settings.OpenSubKey(name);
                if (icon?.GetValue("ExecutablePath") is string path && string.Equals(path, exe, StringComparison.OrdinalIgnoreCase))
                    return icon.GetValue("IsPromoted") is int promoted && promoted == 1;
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
        }
        return null;
    }
}
