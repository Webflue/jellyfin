using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.MissingFileCleanup.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public bool EnableDeletion { get; set; } = false;

    public int SafetyThresholdPercent { get; set; } = 25;

    public bool ScanMovies { get; set; } = true;

    public bool ScanEpisodes { get; set; } = true;

    public bool ScanAudio { get; set; } = true;

    public bool ScanOtherVideos { get; set; } = true;

    public string LastRunSummary { get; set; } = string.Empty;
}
