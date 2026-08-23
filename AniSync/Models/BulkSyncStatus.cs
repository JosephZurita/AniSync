namespace AniSync.Models;

/// <summary>
/// Snapshot of a per-user bulk sync job.
/// </summary>
public sealed record BulkSyncStatus
{
    public string State { get; init; } = "idle";

    public int TotalSeries { get; init; }

    public int ProcessedSeries { get; init; }

    public int UpdatedSeries { get; init; }

    public int FailedSeries { get; init; }

    public string? CurrentSeries { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public string? Error { get; init; }
}

/// <summary>
/// A watched Shoko series that can be selected for bulk sync.
/// </summary>
public sealed record BulkSyncPreviewItem
{
    public int SeriesID { get; init; }

    public int AnidbAnimeID { get; init; }

    public string Title { get; init; } = "Unknown";

    public int EpisodeNumber { get; init; }

    public int TotalEpisodes { get; init; }

    public string? Image { get; init; }
}

/// <summary>
/// The user-selected Shoko series to include in a bulk sync.
/// </summary>
public sealed class BulkSyncRequest
{
    public List<int> SeriesIDs { get; set; } = [];
}
