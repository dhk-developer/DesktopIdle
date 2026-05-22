using System.Text.RegularExpressions;
using Windows.Media.Control;

namespace DesktopIdle;

internal sealed class MediaSessionService
{
    private DateTime _lastCheckUtc = DateTime.MinValue;
    private string _cachedActiveTitle = string.Empty;
    private string _cachedYouTubeStatus = "unknown";

    public async Task<string> GetYouTubePlaybackStatusCachedAsync(string activeTitle, AppSettings settings)
    {
        var now = DateTime.UtcNow;
        var shouldRefresh = _lastCheckUtc == DateTime.MinValue
            || (now - _lastCheckUtc).TotalMilliseconds >= settings.MediaSessionCheckIntervalMs
            || !string.Equals(activeTitle, _cachedActiveTitle, StringComparison.Ordinal);

        if (shouldRefresh)
        {
            _lastCheckUtc = now;
            _cachedActiveTitle = activeTitle;
            _cachedYouTubeStatus = await GetYouTubePlaybackStatusAsync(activeTitle, settings);
        }

        return _cachedYouTubeStatus;
    }

    public async Task<string> DumpSessionsAsync()
    {
        var sessions = await GetSessionsAsync();
        if (sessions.Count == 0) return "No media sessions returned.";
        return string.Join(Environment.NewLine, sessions.Select(s => $"{s.Status}\t{s.Title}\t{s.Artist}\t{s.Source}"));
    }

    private async Task<string> GetYouTubePlaybackStatusAsync(string activeTitle, AppSettings settings)
    {
        var sessions = await GetSessionsAsync();
        var bestStatus = "unknown";

        foreach (var session in sessions)
        {
            if (TitleMatchesYouTubeMedia(activeTitle, session.Title))
            {
                if (session.Status.Equals("Playing", StringComparison.OrdinalIgnoreCase)) return "playing";
                if (session.Status.Equals("Paused", StringComparison.OrdinalIgnoreCase)) bestStatus = "paused";
                else if (bestStatus == "unknown") bestStatus = session.Status.ToLowerInvariant();
            }
        }

        if (bestStatus != "unknown") return bestStatus;

        if (settings.YouTubeFallbackAnyBrowserMedia)
        {
            foreach (var session in sessions)
            {
                if (session.Status.Equals("Playing", StringComparison.OrdinalIgnoreCase)
                    && SourceLooksLikeBrowser(session.Source, settings.BrowserProcesses))
                {
                    return "playing";
                }
            }
        }

        return "unknown";
    }

    private static async Task<List<MediaSessionInfo>> GetSessionsAsync()
    {
        var output = new List<MediaSessionInfo>();

        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            foreach (var session in manager.GetSessions())
            {
                var status = string.Empty;
                var title = string.Empty;
                var artist = string.Empty;
                var source = string.Empty;

                try { status = session.GetPlaybackInfo().PlaybackStatus.ToString(); } catch { }
                try { source = session.SourceAppUserModelId ?? string.Empty; } catch { }

                try
                {
                    var props = await session.TryGetMediaPropertiesAsync();
                    if (props is not null)
                    {
                        title = props.Title ?? string.Empty;
                        artist = props.Artist ?? string.Empty;
                    }
                }
                catch
                {
                    // Ignore individual session metadata failures.
                }

                output.Add(new MediaSessionInfo(CleanField(status), CleanField(title), CleanField(artist), CleanField(source)));
            }
        }
        catch
        {
            // Windows media sessions are optional. Failure should not break ambient mode.
        }

        return output;
    }

    private static bool TitleMatchesYouTubeMedia(string activeTitle, string mediaTitle)
    {
        if (string.IsNullOrWhiteSpace(mediaTitle)) return false;

        var active = CleanYouTubeWindowTitle(activeTitle).Trim();
        var media = mediaTitle.Trim();

        if (active.Length < 4 || media.Length < 4) return false;

        return active.Contains(media, StringComparison.OrdinalIgnoreCase)
            || media.Contains(active, StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanYouTubeWindowTitle(string title)
    {
        var clean = Regex.Replace(title, @"\s*-\s*YouTube.*$", string.Empty, RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"^\s*\([0-9]+\)\s*", string.Empty);
        return clean.Trim();
    }

    private static bool SourceLooksLikeBrowser(string source, IEnumerable<string> browserProcesses)
    {
        foreach (var browserExe in browserProcesses)
        {
            var baseName = Path.GetFileNameWithoutExtension(browserExe);
            if (!string.IsNullOrWhiteSpace(baseName) && source.Contains(baseName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string CleanField(string value) => value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();

    private sealed record MediaSessionInfo(string Status, string Title, string Artist, string Source);
}
