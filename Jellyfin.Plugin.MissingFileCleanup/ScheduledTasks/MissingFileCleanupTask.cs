using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MissingFileCleanup.ScheduledTasks;

public class MissingFileCleanupTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<MissingFileCleanupTask> _logger;

    public MissingFileCleanupTask(ILibraryManager libraryManager, ILogger<MissingFileCleanupTask> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    public string Name => "Remove Missing Files";

    public string Key => "MissingFileCleanup";

    public string Description =>
        "Scans the library and removes database entries for items whose files no longer exist on disk.";

    public string Category => "Library";

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.WeeklyTrigger,
            DayOfWeek = DayOfWeek.Sunday,
            TimeOfDayTicks = TimeSpan.FromHours(3).Ticks,
        };
    }

    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance
            ?? throw new InvalidOperationException("Plugin instance is not initialised.");
        var cfg = plugin.Configuration;

        var kinds = new List<BaseItemKind>();
        if (cfg.ScanMovies)
        {
            kinds.Add(BaseItemKind.Movie);
        }

        if (cfg.ScanEpisodes)
        {
            kinds.Add(BaseItemKind.Episode);
        }

        if (cfg.ScanAudio)
        {
            kinds.Add(BaseItemKind.Audio);
        }

        if (cfg.ScanOtherVideos)
        {
            kinds.Add(BaseItemKind.Video);
            kinds.Add(BaseItemKind.MusicVideo);
        }

        if (kinds.Count == 0)
        {
            _logger.LogInformation("No item types selected to scan. Nothing to do.");
            cfg.LastRunSummary = $"{DateTime.UtcNow:u} - no item types selected.";
            plugin.SaveConfiguration();
            progress.Report(100);
            return Task.CompletedTask;
        }

        var items = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = kinds.ToArray(),
            Recursive = true,
            IsVirtualItem = false,
        });

        var total = items.Count;
        _logger.LogInformation("Scanning {Total} library items for missing files.", total);

        var missing = new List<BaseItem>();
        for (var i = 0; i < total; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = items[i];

            if (item.IsFileProtocol
                && !string.IsNullOrEmpty(item.Path)
                && !File.Exists(item.Path)
                && !Directory.Exists(item.Path))
            {
                missing.Add(item);
            }

            if (total > 0)
            {
                progress.Report((i + 1) * 50.0 / total);
            }
        }

        _logger.LogInformation("Scan complete. {Missing}/{Total} items have missing files.", missing.Count, total);

        if (cfg.SafetyThresholdPercent > 0 && total > 0)
        {
            var pct = missing.Count * 100.0 / total;
            if (pct > cfg.SafetyThresholdPercent)
            {
                _logger.LogWarning(
                    "Aborting: {Missing}/{Total} ({Pct:F1}%) flagged, above threshold {Threshold}%. Likely a missing mount.",
                    missing.Count,
                    total,
                    pct,
                    cfg.SafetyThresholdPercent);
                cfg.LastRunSummary =
                    $"{DateTime.UtcNow:u} - ABORTED: {missing.Count}/{total} flagged ({pct:F1}%) exceeds threshold {cfg.SafetyThresholdPercent}%.";
                plugin.SaveConfiguration();
                progress.Report(100);
                return Task.CompletedTask;
            }
        }

        if (!cfg.EnableDeletion)
        {
            _logger.LogInformation(
                "Dry-run: would delete {Count} items. Enable deletion in the plugin configuration to remove.",
                missing.Count);

            foreach (var m in missing.Take(50))
            {
                _logger.LogInformation("  (dry-run) {Path}", m.Path);
            }

            if (missing.Count > 50)
            {
                _logger.LogInformation("  (... {Overflow} more not shown)", missing.Count - 50);
            }

            cfg.LastRunSummary =
                $"{DateTime.UtcNow:u} - dry-run: scanned {total}, {missing.Count} would be deleted.";
            plugin.SaveConfiguration();
            progress.Report(100);
            return Task.CompletedTask;
        }

        var options = new DeleteOptions { DeleteFileLocation = false };
        var deleted = 0;
        var failed = 0;

        for (var i = 0; i < missing.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = missing[i];
            try
            {
                _libraryManager.DeleteItem(item, options, notifyParentItem: true);
                deleted++;
                _logger.LogInformation("Deleted {Path}", item.Path);
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, "Failed to delete {Path}", item.Path);
            }

            if (missing.Count > 0)
            {
                progress.Report(50 + (i + 1) * 50.0 / missing.Count);
            }
        }

        _logger.LogInformation(
            "Done. Scanned {Total}, deleted {Deleted}, failed {Failed}.",
            total,
            deleted,
            failed);
        cfg.LastRunSummary =
            $"{DateTime.UtcNow:u} - scanned {total}, deleted {deleted}, failed {failed}.";
        plugin.SaveConfiguration();
        progress.Report(100);
        return Task.CompletedTask;
    }
}
