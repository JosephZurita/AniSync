using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.Metadata.Services;

namespace ShokoRelayMetadataBridge;

public sealed class MetadataBridge(
    IMetadataService metadataService,
    ISystemService systemService,
    Action<int>? refreshSeries,
    ILogger<MetadataBridge> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (refreshSeries is null)
        {
            logger.LogWarning("Metadata bridge disabled: a compatible Shoko Relay refresh queue was not found");
            return Task.CompletedTask;
        }

        metadataService.SeriesAdded += OnSeriesChanged;
        metadataService.SeriesUpdated += OnSeriesChanged;
        metadataService.EpisodeAdded += OnEpisodeChanged;
        metadataService.EpisodeUpdated += OnEpisodeChanged;
        metadataService.SeasonAdded += OnSeasonChanged;
        metadataService.SeasonUpdated += OnSeasonChanged;
        metadataService.MovieAdded += OnMovieChanged;
        metadataService.MovieUpdated += OnMovieChanged;
        logger.LogInformation("Metadata bridge active: Shoko metadata updates will use Relay's per-show refresh queue");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        metadataService.SeriesAdded -= OnSeriesChanged;
        metadataService.SeriesUpdated -= OnSeriesChanged;
        metadataService.EpisodeAdded -= OnEpisodeChanged;
        metadataService.EpisodeUpdated -= OnEpisodeChanged;
        metadataService.SeasonAdded -= OnSeasonChanged;
        metadataService.SeasonUpdated -= OnSeasonChanged;
        metadataService.MovieAdded -= OnMovieChanged;
        metadataService.MovieUpdated -= OnMovieChanged;
        return Task.CompletedTask;
    }

    private void OnSeriesChanged(object? sender, SeriesInfoUpdatedEventArgs args)
        => Queue(args.Reason, () => args.SeriesInfo.ShokoSeriesIDs);

    private void OnEpisodeChanged(object? sender, EpisodeInfoUpdatedEventArgs args)
        => Queue(args.Reason, () => args.SeriesInfo.ShokoSeriesIDs);

    private void OnSeasonChanged(object? sender, SeasonInfoUpdatedEventArgs args)
        => Queue(args.Reason, () => args.SeriesInfo.ShokoSeriesIDs);

    private void OnMovieChanged(object? sender, MovieInfoUpdatedEventArgs args)
        => Queue(args.Reason, () => args.MovieInfo.ShokoSeriesIDs);

    private void Queue(UpdateReason reason, Func<IReadOnlyList<int>> resolveSeriesIds)
    {
        // Ignore image-only notifications so Plex image syncing cannot feed back into another refresh.
        if (!systemService.IsStarted || refreshSeries is null || reason is not (UpdateReason.Added or UpdateReason.Updated))
            return;

        int[] seriesIds;
        try
        {
            seriesIds = resolveSeriesIds().Where(id => id > 0).Distinct().ToArray();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not resolve the Shoko series for a metadata update");
            return;
        }

        foreach (var seriesId in seriesIds)
        {
            try
            {
                refreshSeries(seriesId);
                logger.LogDebug("Queued Relay metadata refresh for Shoko series {SeriesId}", seriesId);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not queue Relay metadata refresh for Shoko series {SeriesId}", seriesId);
            }
        }
    }
}
