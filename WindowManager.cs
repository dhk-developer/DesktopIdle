using System.Reflection;
using System.Runtime.InteropServices;

namespace DesktopIdle;

internal sealed class WindowManager
{
    private readonly List<IntPtr> _ambientWindows = new();
    private readonly List<IntPtr> _excludedWindowsRestoredAfterFastShowDesktop = new();
    private IntPtr _previouslyActiveWindow = IntPtr.Zero;
    private bool _usedFastShowDesktop;

    private static readonly HashSet<string> ShellClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd"
    };

    public IntPtr ActiveWindow => Win32.GetForegroundWindow();

    public string GetActiveProcessName()
    {
        var hwnd = ActiveWindow;
        return hwnd == IntPtr.Zero ? string.Empty : Win32.GetProcessNameForWindow(hwnd);
    }

    public string GetActiveWindowTitle()
    {
        var hwnd = ActiveWindow;
        return hwnd == IntPtr.Zero ? string.Empty : Win32.GetWindowTitle(hwnd);
    }

    public bool IsActiveWindowFullscreen(AppSettings settings)
    {
        if (!settings.SuppressWhenFullscreen) return false;

        var hwnd = ActiveWindow;
        if (hwnd == IntPtr.Zero) return false;

        var process = Win32.GetProcessNameForWindow(hwnd);
        var className = Win32.GetWindowClass(hwnd);

        if (process.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase)) return false;
        if (ShellClasses.Contains(className)) return false;

        var placement = new Win32.WINDOWPLACEMENT { length = Marshal.SizeOf<Win32.WINDOWPLACEMENT>() };
        if (Win32.GetWindowPlacement(hwnd, ref placement) && placement.showCmd == Win32.SW_SHOWMAXIMIZED)
        {
            return false;
        }

        if (!Win32.GetWindowRect(hwnd, out var rect)) return false;
        if (rect.Width <= 0 || rect.Height <= 0) return false;

        var monitor = Win32.MonitorFromWindow(hwnd, 0x00000002);
        if (monitor == IntPtr.Zero) return false;

        var monitorInfo = new Win32.MONITORINFO { cbSize = (uint)Marshal.SizeOf<Win32.MONITORINFO>() };
        if (!Win32.GetMonitorInfo(monitor, ref monitorInfo)) return false;

        const int tolerance = 3;
        return rect.Left <= monitorInfo.rcMonitor.Left + tolerance
            && rect.Top <= monitorInfo.rcMonitor.Top + tolerance
            && rect.Right >= monitorInfo.rcMonitor.Right - tolerance
            && rect.Bottom >= monitorInfo.rcMonitor.Bottom - tolerance;
    }

    public void ShowDesktopByMinimisingWindows(AppSettings settings)
    {
        _previouslyActiveWindow = ActiveWindow;
        _ambientWindows.Clear();
        _excludedWindowsRestoredAfterFastShowDesktop.Clear();
        _usedFastShowDesktop = false;

        // Fast path: use Explorer's native "Show Desktop", but first remember only
        // the windows that were visible beforehand. On exit we restore this captured
        // list asynchronously instead of waiting on Shell.Application UndoMinimizeAll,
        // which can take several seconds on some Windows 11 setups.
        if (TryShowDesktopFast(settings)) return;

        // Fallback path: preserve manual behaviour if Shell.Application is unavailable.
        ShowDesktopByManualMinimisingWindows(settings);
    }

    public void RestoreAmbientWindows(int staggerDelayMs = 0)
    {
        if (_usedFastShowDesktop)
        {
            if (_ambientWindows.Count > 0)
            {
                RestoreCapturedWindowsFast(staggerDelayMs);
            }
            else
            {
                // Very defensive fallback: should rarely be needed, but prevents users
                // getting stuck if no windows were captured even though Shell minimize ran.
                TryShellCommand("UndoMinimizeAll");
            }

            // Keep excluded windows visible and stable. They should already be visible,
            // because they were restored immediately after entry, but this is harmless.
            foreach (var hwnd in _excludedWindowsRestoredAfterFastShowDesktop)
            {
                RestoreWindowFast(hwnd);
            }
        }
        else
        {
            RestoreCapturedWindowsFast(staggerDelayMs);
        }

        if (_previouslyActiveWindow != IntPtr.Zero && Win32.IsWindow(_previouslyActiveWindow))
        {
            Win32.SetForegroundWindow(_previouslyActiveWindow);
        }

        _ambientWindows.Clear();
        _excludedWindowsRestoredAfterFastShowDesktop.Clear();
        _previouslyActiveWindow = IntPtr.Zero;
        _usedFastShowDesktop = false;
    }

    private bool TryShowDesktopFast(AppSettings settings)
    {
        try
        {
            // Capture the exact windows we intend to bring back later. Do not cap this
            // list with RestoreMaxWindows, because Shell MinimizeAll affects all visible
            // windows. The cap is only used by the manual fallback path.
            var windowsToRestore = GetRestoreableWindows(settings, respectWindowExclusions: true, maxWindows: 500).ToList();
            var excludedWindows = GetVisibleExcludedWindows(settings).ToList();

            if (!TryShellCommand("MinimizeAll")) return false;

            _usedFastShowDesktop = true;
            _ambientWindows.AddRange(windowsToRestore);

            if (excludedWindows.Count > 0)
            {
                Thread.Sleep(25);

                foreach (var hwnd in excludedWindows)
                {
                    if (!Win32.IsWindow(hwnd)) continue;
                    RestoreWindowFast(hwnd);
                    _excludedWindowsRestoredAfterFastShowDesktop.Add(hwnd);
                }
            }

            return true;
        }
        catch
        {
            _usedFastShowDesktop = false;
            _ambientWindows.Clear();
            _excludedWindowsRestoredAfterFastShowDesktop.Clear();
            return false;
        }
    }

    private void ShowDesktopByManualMinimisingWindows(AppSettings settings)
    {
        foreach (var hwnd in GetRestoreableWindows(settings, respectWindowExclusions: true, maxWindows: Math.Max(1, settings.RestoreMaxWindows)))
        {
            _ambientWindows.Add(hwnd);
            if (!Win32.ShowWindowAsync(hwnd, Win32.SW_MINIMIZE))
            {
                Win32.ShowWindow(hwnd, Win32.SW_MINIMIZE);
            }
        }
    }

    private void RestoreCapturedWindowsFast(int staggerDelayMs = 0)
    {
        // Reverse order approximates the old behaviour and tends to return the last
        // active windows near the front. A tiny optional stagger makes the exit feel
        // less abrupt without adding a dark overlay or making the restore feel slow.
        var delay = Math.Clamp(staggerDelayMs, 0, 50);

        for (var i = _ambientWindows.Count - 1; i >= 0; i--)
        {
            RestoreWindowFast(_ambientWindows[i]);

            if (delay > 0 && i > 0)
            {
                Thread.Sleep(delay);
            }
        }
    }

    private static void RestoreWindowFast(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !Win32.IsWindow(hwnd)) return;

        if (!Win32.ShowWindowAsync(hwnd, Win32.SW_RESTORE))
        {
            Win32.ShowWindow(hwnd, Win32.SW_RESTORE);
        }
    }

    private IEnumerable<IntPtr> GetRestoreableWindows(AppSettings settings, bool respectWindowExclusions, int maxWindows)
    {
        var windows = new List<IntPtr>();
        var cap = Math.Clamp(maxWindows, 1, 500);

        Win32.EnumWindows((hwnd, _) =>
        {
            if (windows.Count >= cap) return false;
            if (IsRestoreableWindow(hwnd, settings, respectWindowExclusions)) windows.Add(hwnd);
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private IEnumerable<IntPtr> GetVisibleExcludedWindows(AppSettings settings)
    {
        var windows = new List<IntPtr>();

        if (settings.WindowExcludedProcesses.Count == 0) return windows;

        Win32.EnumWindows((hwnd, _) =>
        {
            if (!IsRestoreableWindow(hwnd, settings, respectWindowExclusions: false)) return true;
            if (IsWindowProcessExcluded(hwnd, settings)) windows.Add(hwnd);
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private static bool IsRestoreableWindow(IntPtr hwnd, AppSettings settings, bool respectWindowExclusions)
    {
        if (hwnd == IntPtr.Zero) return false;
        if (!Win32.IsWindowVisible(hwnd)) return false;

        var title = Win32.GetWindowTitle(hwnd);
        if (string.IsNullOrWhiteSpace(title)) return false;

        var className = Win32.GetWindowClass(hwnd);
        if (ShellClasses.Contains(className)) return false;

        var placement = new Win32.WINDOWPLACEMENT { length = Marshal.SizeOf<Win32.WINDOWPLACEMENT>() };
        if (Win32.GetWindowPlacement(hwnd, ref placement) && placement.showCmd == Win32.SW_SHOWMINIMIZED) return false;

        var style = Win32.GetWindowLongPtr(hwnd, Win32.GWL_STYLE).ToInt64();
        var exStyle = Win32.GetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE).ToInt64();

        if ((style & Win32.WS_VISIBLE) == 0) return false;
        if ((exStyle & Win32.WS_EX_TOOLWINDOW) != 0) return false;

        if (respectWindowExclusions && IsWindowProcessExcluded(hwnd, settings)) return false;

        return true;
    }

    private static bool IsWindowProcessExcluded(IntPtr hwnd, AppSettings settings)
    {
        var process = Win32.GetProcessNameForWindow(hwnd);
        return settings.WindowExcludedProcesses.Any(p => p.Equals(process, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryShellCommand(string commandName)
    {
        object? shell = null;

        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType is null) return false;

            shell = Activator.CreateInstance(shellType);
            if (shell is null) return false;

            shellType.InvokeMember(
                commandName,
                BindingFlags.InvokeMethod,
                binder: null,
                target: shell,
                args: Array.Empty<object>());

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (shell is not null && Marshal.IsComObject(shell))
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }
}
