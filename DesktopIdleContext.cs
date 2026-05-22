using System.Runtime.Versioning;

namespace DesktopIdle;

[SupportedOSPlatform("windows10.0.19041.0")]
internal sealed class DesktopIdleContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 250 };
    private readonly WindowManager _windows = new();
    private readonly TaskbarController _taskbar = new();
    private readonly MediaSessionService _media = new();
    private readonly ActivityDetector _activityDetector;
    private readonly CursorMaskManager _cursorMask = new();

    private AppSettings _settings;
    private bool _ambientModeActive;
    private bool _ambientTransitioning;
    private DateTime _ambientInputGraceUntilUtc = DateTime.MinValue;
    private bool _taskbarAutoHideBeforeAmbient;
    private bool _checking;
    private Point _ambientCursorAnchor = Point.Empty;

    public DesktopIdleContext()
    {
        _settings = SettingsStore.Load();
        EnsureOwnProcessExcluded();
        _activityDetector = new ActivityDetector(_windows, _media);

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Desktop Idle",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };
        _trayIcon.DoubleClick += (_, _) => OpenSettings();


        _timer.Tick += async (_, _) => await CheckIdleAsync();
        _timer.Start();
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings...", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Reload settings", null, (_, _) => ReloadSettings());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        return menu;
    }

    private async Task CheckIdleAsync()
    {
        if (_checking || _ambientTransitioning) return;
        _checking = true;

        try
        {

            if (_ambientModeActive)
            {
                if (DateTime.UtcNow > _ambientInputGraceUntilUtc
                    && AmbientExitInputDetector.ShouldExitAmbientMode(_ambientCursorAnchor))
                {
                    ExitAmbientMode();
                }

                return;
            }


            if (await _activityDetector.IsEngagedActivityActiveAsync(_settings))
            {
                return;
            }

            var idleMs = Win32.GetIdleMilliseconds();

            if (idleMs >= (uint)(_settings.AmbientDelaySeconds * 1000))
            {
                EnterAmbientMode();
            }
        }
        finally
        {
            _checking = false;
        }
    }

    private void EnterAmbientMode()
    {
        if (_ambientModeActive || _ambientTransitioning) return;

        _ambientModeActive = true;
        _ambientTransitioning = true;
        _timer.Stop();

        try
        {

            CursorManager.MoveToCurrentScreenCentre();
            _ambientCursorAnchor = Cursor.Position;
            AmbientExitInputDetector.ClearKeyStateLatches();

            if (_settings.EnableTaskbarAutoHideInAmbient)
            {
                _taskbarAutoHideBeforeAmbient = _taskbar.GetAutoHide();
            }

            _windows.ShowDesktopByMinimisingWindows(_settings);
            _cursorMask.Show();

            if (_settings.EnableTaskbarAutoHideInAmbient)
            {

                _taskbar.StartDeferredAutoHide(175);
            }

            _ambientInputGraceUntilUtc = DateTime.UtcNow.AddMilliseconds(GetProgrammaticInputGraceMs());
        }
        catch
        {
            // Never leave the app stuck because one window refused to minimise.
        }
        finally
        {
            _ambientTransitioning = false;
            _timer.Start();
        }
    }

    private void ExitAmbientMode()
    {
        if (!_ambientModeActive || _ambientTransitioning) return;

        _ambientTransitioning = true;
        _timer.Stop();

        try
        {
            _taskbar.StopKeeper();
            _cursorMask.Hide();


            _windows.RestoreAmbientWindows(staggerDelayMs: 18);
            _ambientCursorAnchor = Point.Empty;

            if (_settings.EnableTaskbarAutoHideInAmbient)
            {
                _taskbar.SetAutoHide(_taskbarAutoHideBeforeAmbient);
            }
        }
        finally
        {
            _ambientModeActive = false;
            _ambientTransitioning = false;
            _ambientCursorAnchor = Point.Empty;
            _timer.Start();
        }
    }

    private int GetProgrammaticInputGraceMs()
    {

        return Math.Max(_settings.AmbientInputGraceMs, _settings.InputFreshThresholdMs + 500);
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(_settings, _windows.GetActiveProcessName);
        if (form.ShowDialog() == DialogResult.OK)
        {
            _settings = form.Settings;
            EnsureOwnProcessExcluded();
            SettingsStore.Save(_settings);
        }
    }

    private void ReloadSettings()
    {
        _settings = SettingsStore.Load();
        EnsureOwnProcessExcluded();
        _trayIcon.ShowBalloonTip(1500, "Desktop Idle", "Settings reloaded.", ToolTipIcon.Info);
    }

    private void EnsureOwnProcessExcluded()
    {
        var ownName = Path.GetFileName(Application.ExecutablePath);
        if (!string.IsNullOrWhiteSpace(ownName)
            && !_settings.WindowExcludedProcesses.Any(p => p.Equals(ownName, StringComparison.OrdinalIgnoreCase)))
        {
            _settings.WindowExcludedProcesses.Add(ownName);
            _settings.Sanitise();
        }
    }

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        _taskbar.StopKeeper();

        if (_ambientModeActive || _ambientTransitioning)
        {
            _cursorMask.Hide();
            _windows.RestoreAmbientWindows();
            if (_settings.EnableTaskbarAutoHideInAmbient) _taskbar.SetAutoHide(_taskbarAutoHideBeforeAmbient);
        }

        _cursorMask.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        base.ExitThreadCore();
    }
}
