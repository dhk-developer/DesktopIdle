namespace DesktopIdle;

internal sealed class TaskbarController
{
    private readonly System.Windows.Forms.Timer _keeperTimer = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer _deferredAutoHideTimer = new();
    private bool _keeperActive;
    private int _pendingAutoHideDelayMs = 175;
    private int _repairMisses;
    private DateTime _lastBroadcastUtc = DateTime.MinValue;

    public TaskbarController()
    {
        _keeperTimer.Tick += (_, _) =>
        {
            if (!_keeperActive)
            {
                _keeperTimer.Stop();
                return;
            }

            // Watchdog behaviour: verify the state regularly, but only write to
            // Explorer when auto-hide has actually dropped out. This avoids the
            // old stutter problem caused by repeatedly setting taskbar state.
            EnsureAutoHideOn(allowBroadcastOnFailure: false);
        };

        _deferredAutoHideTimer.Tick += (_, _) =>
        {
            _deferredAutoHideTimer.Stop();

            // Initial entry path. Try quiet first for smoothness, then escalate to
            // one settings broadcast only if the verification says it did not stick.
            EnsureAutoHideOn(allowBroadcastOnFailure: true);
            StartKeeper();
        };
    }

    public bool GetAutoHide()
    {
        try
        {
            var data = new Win32.APPBARDATA { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Win32.APPBARDATA>() };
            var state = Win32.SHAppBarMessage(Win32.ABM_GETSTATE, ref data).ToInt64();
            return (state & Win32.ABS_AUTOHIDE) != 0;
        }
        catch
        {
            return false;
        }
    }

    public void ForceAutoHideOn()
    {
        EnsureAutoHideOn(allowBroadcastOnFailure: true);
    }

    public bool EnsureAutoHideOn(bool allowBroadcastOnFailure)
    {
        try
        {
            if (GetAutoHide())
            {
                _repairMisses = 0;
                return true;
            }

            _repairMisses++;

            // First repair attempt is quiet. This is the smooth path and is usually
            // enough when Explorer simply missed the first deferred set call.
            SetAutoHide(true, broadcast: false, force: true);
            if (GetAutoHide())
            {
                _repairMisses = 0;
                return true;
            }

            // If quiet repair did not stick, escalate. We allow immediate escalation
            // during entry, or after repeated watchdog misses / cooldown thereafter.
            var broadcastCooldownElapsed = (DateTime.UtcNow - _lastBroadcastUtc).TotalMilliseconds >= 5000;
            var shouldBroadcast = allowBroadcastOnFailure || _repairMisses >= 2 || broadcastCooldownElapsed;

            if (shouldBroadcast)
            {
                SetAutoHide(true, broadcast: true, force: true);
                _lastBroadcastUtc = DateTime.UtcNow;
            }
            else
            {
                SetAutoHide(true, broadcast: false, force: true);
            }

            if (GetAutoHide())
            {
                _repairMisses = 0;
                return true;
            }
        }
        catch
        {
            // Best effort. Taskbar state should never crash the utility.
        }

        return false;
    }

    public void StartDeferredAutoHide(int delayMs = 175)
    {
        _pendingAutoHideDelayMs = Math.Clamp(delayMs, 0, 1000);
        StopKeeper();

        if (_pendingAutoHideDelayMs <= 0)
        {
            ForceAutoHideOn();
            StartKeeper();
            return;
        }

        _deferredAutoHideTimer.Interval = _pendingAutoHideDelayMs;
        _deferredAutoHideTimer.Start();
    }

    public void StartKeeper()
    {
        _keeperActive = true;
        _keeperTimer.Start();
    }

    public void StopKeeper()
    {
        _keeperActive = false;
        _repairMisses = 0;
        _keeperTimer.Stop();
        _deferredAutoHideTimer.Stop();
    }

    public void SetAutoHide(bool enable)
    {
        SetAutoHide(enable, broadcast: true);
    }

    public void SetAutoHide(bool enable, bool broadcast, bool force = false)
    {
        try
        {
            if (!force && GetAutoHide() == enable) return;

            var trayWindows = FindTrayWindows();
            if (trayWindows.Count == 0) return;

            foreach (var trayHwnd in trayWindows)
            {
                if (trayHwnd == IntPtr.Zero) continue;

                var data = new Win32.APPBARDATA
                {
                    cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Win32.APPBARDATA>(),
                    hWnd = trayHwnd,
                    lParam = enable
                        ? new IntPtr(Win32.ABS_AUTOHIDE | Win32.ABS_ALWAYSONTOP)
                        : new IntPtr(Win32.ABS_ALWAYSONTOP)
                };

                Win32.SHAppBarMessage(Win32.ABM_SETSTATE, ref data);
            }

            if (broadcast)
            {
                BroadcastTraySettingsChanged();
            }
        }
        catch
        {
            // Best effort. Taskbar state should never crash the utility.
        }
    }

    private static List<IntPtr> FindTrayWindows()
    {
        var trayWindows = new List<IntPtr>();

        Win32.EnumWindows((hwnd, _) =>
        {
            var className = Win32.GetWindowClass(hwnd);

            if (className.Equals("Shell_TrayWnd", StringComparison.OrdinalIgnoreCase))
            {
                // Primary taskbar first.
                trayWindows.Insert(0, hwnd);
            }
            else if (className.Equals("Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase))
            {
                trayWindows.Add(hwnd);
            }

            return true;
        }, IntPtr.Zero);

        return trayWindows;
    }

    private static void BroadcastTraySettingsChanged()
    {
        try
        {
            _ = Win32.SendMessageTimeout(
                Win32.HWND_BROADCAST,
                Win32.WM_SETTINGCHANGE,
                IntPtr.Zero,
                "TraySettings",
                Win32.SMTO_ABORTIFHUNG,
                75,
                out _);
        }
        catch
        {
            // Ignore broadcast failures.
        }
    }
}
