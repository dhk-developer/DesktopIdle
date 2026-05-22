namespace DesktopIdle;

public sealed class AppSettings
{
    public int AmbientDelaySeconds { get; set; } = 120;
    public int InputFreshThresholdMs { get; set; } = 750;
    public int AmbientInputGraceMs { get; set; } = 1500;
    public bool EnableTaskbarAutoHideInAmbient { get; set; } = true;
    public bool SuppressWhenFullscreen { get; set; } = true;
    public int RestoreMaxWindows { get; set; } = 50;
    public bool YouTubeOnlySuppressWhenPlaying { get; set; } = true;
    public bool YouTubeFallbackAnyBrowserMedia { get; set; } = false;
    public int MediaSessionCheckIntervalMs { get; set; } = 3000;

    public List<string> BrowserProcesses { get; set; } = new()
    {
        "opera.exe",
        "opera_gx.exe",
        "chrome.exe",
        "msedge.exe",
        "firefox.exe",
        "brave.exe",
        "vivaldi.exe"
    };

    public List<string> ExcludedActivityProcesses { get; set; } = DefaultActivityExclusions();

    public List<string> VideoPlayerProcesses { get; set; } = new()
    {
        "vlc.exe",
        "mpv.exe",
        "mpc-hc.exe",
        "mpc-hc64.exe",
        "PotPlayerMini.exe",
        "PotPlayerMini64.exe",
        "Video.UI.exe",
        "wmplayer.exe"
    };

    public List<string> VideoTitlePatterns { get; set; } = new()
    {
        "Netflix",
        "Disney+",
        "Prime Video",
        "Amazon Prime Video",
        "Twitch",
        "Crunchyroll",
        "Vimeo",
        "Plex",
        "Jellyfin",
        "BBC iPlayer",
        "ITVX",
        "Channel 4",
        "NOW",
        "Sky Go",
        "Bilibili",
        "Picture-in-Picture",
        "Picture in Picture"
    };

    public List<string> VideoTitleExceptions { get; set; } = new()
    {
        "YouTube Music",
        "Spotify",
        "SoundCloud",
        "Apple Music",
        "Amazon Music"
    };

    public List<string> WindowExcludedProcesses { get; set; } = DefaultWindowExclusions();

    public static List<string> DefaultActivityExclusions() => new()
    {
        "GenshinImpact.exe",
        "YuanShen.exe",
        "ZenlessZoneZero.exe",
        "ZZZ.exe",
        "StarRail.exe",
        "ffxiv_dx11.exe",
        "ffxiv.exe",
        "NevernessToEverness.exe",
        "NTE.exe"
    };

    public static List<string> DefaultWindowExclusions() => new()
    {
        "rainmeter.exe",
        "DesktopIdle.exe",
        "AutoHotkey64.exe",
        "AutoHotkey32.exe",
        "AutoHotkey.exe"
    };

    public void Sanitise()
    {
        AmbientDelaySeconds = Math.Max(5, AmbientDelaySeconds);
        InputFreshThresholdMs = Math.Max(100, InputFreshThresholdMs);
        AmbientInputGraceMs = Math.Max(250, AmbientInputGraceMs);
        RestoreMaxWindows = Math.Clamp(RestoreMaxWindows, 1, 500);
        MediaSessionCheckIntervalMs = Math.Max(500, MediaSessionCheckIntervalMs);

        BrowserProcesses = NormaliseDistinct(BrowserProcesses);
        ExcludedActivityProcesses = NormaliseDistinct(ExcludedActivityProcesses);
        VideoPlayerProcesses = NormaliseDistinct(VideoPlayerProcesses);
        WindowExcludedProcesses = NormaliseDistinct(WindowExcludedProcesses);
        VideoTitlePatterns = VideoTitlePatterns.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        VideoTitleExceptions = VideoTitleExceptions.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static string NormaliseProcessName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return string.Empty;
        var trimmed = processName.Trim().Trim('"');
        if (trimmed.Contains('\\') || trimmed.Contains('/')) trimmed = Path.GetFileName(trimmed);
        return trimmed.Trim();
    }

    public static List<string> NormaliseDistinct(IEnumerable<string>? names)
    {
        if (names is null) return new List<string>();
        return names
            .Select(NormaliseProcessName)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
