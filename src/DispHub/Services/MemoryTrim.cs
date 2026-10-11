using System.Runtime;
using System.Windows.Threading;

namespace DispHub.Services;

/// <summary>
/// DispHub mostly sits in the tray. After startup and after the window closes, give unused memory back to Windows
/// so the background footprint stays small.
/// </summary>
public static class MemoryTrim
{
    private static DispatcherTimer? _timer;

    public static void Schedule(TimeSpan? delay = null)
    {
        _timer?.Stop();
        _timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = delay ?? TimeSpan.FromSeconds(5) };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            TrimNow();
        };
        _timer.Start();
    }

    public static void TrimNow()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        Platform.Shell.WindowHelpers.TrimWorkingSet();
    }
}
