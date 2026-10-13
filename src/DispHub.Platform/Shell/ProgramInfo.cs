using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;

namespace DispHub.Platform.Shell;

/// <summary>A program with a window on screen.</summary>
public sealed record RunningProgram(string Path, string Name, string WindowTitle);

/// <summary>Names, icons and the list of running programs, for picking an app to make a rule for.</summary>
public static class ProgramInfo
{
    private static readonly HashSet<string> Hidden = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer.exe", "ApplicationFrameHost.exe", "TextInputHost.exe", "SystemSettings.exe", "ShellExperienceHost.exe",
        "StartMenuExperienceHost.exe", "SearchHost.exe", "LockApp.exe",
    };

    /// <summary>The program's own name ("Google Chrome"), falling back to its file name.</summary>
    public static string DisplayName(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
                string? name = !string.IsNullOrWhiteSpace(info.FileDescription) ? info.FileDescription : info.ProductName;
                if (!string.IsNullOrWhiteSpace(name))
                    return name.Trim();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return System.IO.Path.GetFileNameWithoutExtension(path);
    }

    /// <summary>The program's icon at <paramref name="size"/> pixels, or 0. The caller destroys it with DestroyIcon.</summary>
    public static unsafe nint ExtractIcon(string path, int size)
    {
        HICON large = default;
        fixed (char* file = path)
        {
            if (PInvoke.SHDefExtractIcon(file, 0, 0, &large, null, (uint)size).Failed)
                return 0;
        }
        return large;
    }

    public static void DestroyIcon(nint icon)
    {
        if (icon != 0)
            PInvoke.DestroyIcon((HICON)icon);
    }

    /// <summary>Programs with a visible, regular window, one entry per program, DispHub excluded.</summary>
    public static unsafe IReadOnlyList<RunningProgram> WithWindows()
    {
        var found = new List<RunningProgram>();
        GCHandle handle = GCHandle.Alloc(found);
        try
        {
            PInvoke.EnumWindows(&OnWindow, (LPARAM)GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }
        return [.. found
            .Where(p => !Hidden.Contains(System.IO.Path.GetFileName(p.Path)) && !string.Equals(p.Path, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
            .GroupBy(p => p.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe BOOL OnWindow(HWND window, LPARAM data)
    {
        if (!PInvoke.IsWindowVisible(window) || !PInvoke.GetWindow(window, GET_WINDOW_CMD.GW_OWNER).IsNull)
            return true;
        int style = PInvoke.GetWindowLong(window, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        if ((style & (int)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) != 0)
            return true;
        int cloaked = 0;
        if (PInvoke.DwmGetWindowAttribute(window, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(int)).Succeeded && cloaked != 0)
            return true;
        int length = PInvoke.GetWindowTextLength(window);
        if (length == 0)
            return true;

        Span<char> title = length < 512 ? stackalloc char[length + 1] : new char[length + 1];
        fixed (char* chars = title)
            length = PInvoke.GetWindowText(window, chars, title.Length);
        _ = PInvoke.GetWindowThreadProcessId(window, out uint processId);
        if (ForegroundWatcher.GetProcessPath(processId) is { } path)
        {
            var list = (List<RunningProgram>)GCHandle.FromIntPtr(data).Target!;
            list.Add(new RunningProgram(path, DisplayName(path), title[..length].ToString()));
        }
        return true;
    }
}
