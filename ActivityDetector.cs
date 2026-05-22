namespace DesktopIdle;

internal sealed class ActivityDetector
{
    private readonly WindowManager _windows;
    private readonly MediaSessionService _media;

    public ActivityDetector(WindowManager windows, MediaSessionService media)
    {
        _windows = windows;
        _media = media;
    }

    public async Task<bool> IsEngagedActivityActiveAsync(AppSettings settings)
    {
        if (IsExcludedActivityProcessActive(settings)) return true;
        if (_windows.IsActiveWindowFullscreen(settings)) return true;
        if (await IsActiveVideoLikeWindowAsync(settings)) return true;
        return false;
    }

    public bool IsExcludedActivityProcessActive(AppSettings settings)
    {
        var activeProcess = _windows.GetActiveProcessName();
        return settings.ExcludedActivityProcesses.Any(p => p.Equals(activeProcess, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> IsActiveVideoLikeWindowAsync(AppSettings settings)
    {
        var activeProcess = _windows.GetActiveProcessName();
        var activeTitle = _windows.GetActiveWindowTitle();

        if (settings.VideoPlayerProcesses.Any(p => p.Equals(activeProcess, StringComparison.OrdinalIgnoreCase))) return true;

        if (settings.VideoTitleExceptions.Any(t => activeTitle.Contains(t, StringComparison.OrdinalIgnoreCase))) return false;

        if (IsActiveYouTubeWindow(settings, activeProcess, activeTitle))
        {
            if (!settings.YouTubeOnlySuppressWhenPlaying) return true;
            var status = await _media.GetYouTubePlaybackStatusCachedAsync(activeTitle, settings);
            return status.Equals("playing", StringComparison.OrdinalIgnoreCase);
        }

        return settings.VideoTitlePatterns.Any(t => activeTitle.Contains(t, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsActiveYouTubeWindow(AppSettings settings, string? activeProcess = null, string? activeTitle = null)
    {
        activeProcess ??= _windows.GetActiveProcessName();
        activeTitle ??= _windows.GetActiveWindowTitle();

        var isBrowser = settings.BrowserProcesses.Any(p => p.Equals(activeProcess, StringComparison.OrdinalIgnoreCase));
        if (!isBrowser) return false;
        if (activeTitle.Contains("YouTube Music", StringComparison.OrdinalIgnoreCase)) return false;
        return activeTitle.Contains(" - YouTube", StringComparison.OrdinalIgnoreCase);
    }
}
