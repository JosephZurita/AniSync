using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.Metadata.Services;
using Xunit;

namespace ShokoRelayMetadataBridge.Tests;

public sealed class MetadataBridgeTests
{
    private readonly Mock<IMetadataService> _metadata = new();
    private readonly Mock<ISystemService> _system = new();
    private readonly List<int> _refreshed = [];

    public MetadataBridgeTests() => _system.SetupGet(service => service.IsStarted).Returns(true);

    [Fact]
    public void RelayBindingSuppliesTheOptionalScanFlagWithoutDeferringRefresh()
    {
        var watcher = new RelayWatcher();
        var refresh = PluginServiceRegistration.BindRefresh(watcher);

        Assert.NotNull(refresh);
        refresh(41);

        Assert.Equal(41, watcher.SeriesId);
        Assert.False(watcher.DeferScan);
        Assert.Null(PluginServiceRegistration.BindRefresh(new object()));
        Assert.Null(PluginServiceRegistration.BindRefresh(null));
    }

    [Fact]
    public void PluginIdentityMatchesTheMetadataReadBeforeShokoLoadsThePlugin()
    {
        var identity = typeof(Plugin).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .Cast<System.Reflection.AssemblyMetadataAttribute>().Single(attribute => attribute.Key == "PackageID");

        Assert.Equal(new Plugin().ID.ToString(), identity.Value);
    }

    [Fact]
    public async Task LateProviderEpisodeUpdateRefreshesOnlyItsLinkedShokoSeries()
    {
        var bridge = CreateBridge();
        await bridge.StartAsync(CancellationToken.None);
        var series = Series(41, 99, 41, 0, -1);

        _metadata.Raise(service => service.EpisodeUpdated += null,
            new EpisodeInfoUpdatedEventArgs(series, Mock.Of<IEpisode>(), UpdateReason.Updated));

        Assert.Equal([41, 99], _refreshed);
    }

    [Fact]
    public async Task AddedAndUpdatedSeriesEpisodesSeasonsAndMoviesUseTheSameRelayQueue()
    {
        var bridge = CreateBridge();
        await bridge.StartAsync(CancellationToken.None);

        RaiseEveryMetadataEvent();

        Assert.Equal(Enumerable.Repeat(41, 8), _refreshed);
    }

    [Theory]
    [InlineData(UpdateReason.None)]
    [InlineData(UpdateReason.Removed)]
    [InlineData(UpdateReason.ImageAdded)]
    [InlineData(UpdateReason.ImageUpdated)]
    [InlineData(UpdateReason.ImageRemoved)]
    public async Task ImageSyncAndUnrelatedNotificationsCannotCauseRefreshLoops(UpdateReason reason)
    {
        var bridge = CreateBridge();
        await bridge.StartAsync(CancellationToken.None);

        _metadata.Raise(service => service.SeriesUpdated += null, new SeriesInfoUpdatedEventArgs(Series(41), reason));

        Assert.Empty(_refreshed);
    }

    [Fact]
    public async Task UnlinkedProviderMetadataCannotTriggerAFullLibraryRefresh()
    {
        var bridge = CreateBridge();
        await bridge.StartAsync(CancellationToken.None);

        _metadata.Raise(service => service.SeriesUpdated += null,
            new SeriesInfoUpdatedEventArgs(Series(), UpdateReason.Updated));

        Assert.Empty(_refreshed);
    }

    [Fact]
    public async Task StartupMetadataLoadingDoesNotRefreshTheLibrary()
    {
        _system.SetupGet(service => service.IsStarted).Returns(false);
        var bridge = CreateBridge();
        await bridge.StartAsync(CancellationToken.None);

        var series = new Mock<ISeries>();
        series.SetupGet(value => value.ShokoSeriesIDs).Throws(new InvalidOperationException("Database is still loading"));
        _metadata.Raise(service => service.SeriesUpdated += null,
            new SeriesInfoUpdatedEventArgs(series.Object, UpdateReason.Updated));

        series.VerifyGet(value => value.ShokoSeriesIDs, Times.Never);
        Assert.Empty(_refreshed);
    }

    [Fact]
    public async Task FailedSeriesLookupDoesNotInterruptShokoMetadataUpdates()
    {
        var bridge = CreateBridge();
        await bridge.StartAsync(CancellationToken.None);
        var series = new Mock<ISeries>();
        series.SetupGet(value => value.ShokoSeriesIDs).Throws(new InvalidOperationException("Metadata lookup failed"));

        _metadata.Raise(service => service.SeriesUpdated += null,
            new SeriesInfoUpdatedEventArgs(series.Object, UpdateReason.Updated));

        Assert.Empty(_refreshed);
    }

    [Fact]
    public async Task FailingRelayCallDoesNotInterruptShokoOrOtherLinkedSeries()
    {
        var bridge = CreateBridge(id =>
        {
            _refreshed.Add(id);
            if (id == 41)
                throw new InvalidOperationException("Relay is unavailable");
        });
        await bridge.StartAsync(CancellationToken.None);

        _metadata.Raise(service => service.SeriesUpdated += null,
            new SeriesInfoUpdatedEventArgs(Series(41, 99), UpdateReason.Updated));

        Assert.Equal([41, 99], _refreshed);
    }

    [Fact]
    public async Task StoppingDetachesEveryEventHandler()
    {
        var bridge = CreateBridge();
        await bridge.StartAsync(CancellationToken.None);
        await bridge.StopAsync(CancellationToken.None);

        RaiseEveryMetadataEvent();

        Assert.Empty(_refreshed);
    }

    [Fact]
    public async Task MissingOrIncompatibleRelayLeavesTheBridgeDisabled()
    {
        var bridge = new MetadataBridge(_metadata.Object, _system.Object, null, NullLogger<MetadataBridge>.Instance);
        await bridge.StartAsync(CancellationToken.None);

        RaiseEveryMetadataEvent();

        _metadata.VerifyAdd(service => service.SeriesUpdated += It.IsAny<EventHandler<SeriesInfoUpdatedEventArgs>>(), Times.Never);
        Assert.Empty(_refreshed);
    }

    private MetadataBridge CreateBridge(Action<int>? refresh = null)
        => new(_metadata.Object, _system.Object, refresh ?? _refreshed.Add, NullLogger<MetadataBridge>.Instance);

    public sealed class RelayWatcher
    {
        public int SeriesId { get; private set; }
        public bool DeferScan { get; private set; } = true;

        public void TriggerPlexUpdates(int seriesId, bool deferScan = false)
        {
            SeriesId = seriesId;
            DeferScan = deferScan;
        }
    }

    private static ISeries Series(params int[] ids)
    {
        var series = new Mock<ISeries>();
        series.SetupGet(value => value.ShokoSeriesIDs).Returns(ids);
        return series.Object;
    }

    private void RaiseEveryMetadataEvent()
    {
        var series = Series(41);
        var episode = Mock.Of<IEpisode>();
        var season = Mock.Of<ISeason>();
        var movie = new Mock<IMovie>();
        movie.SetupGet(value => value.ShokoSeriesIDs).Returns([41]);
        _metadata.Raise(service => service.SeriesAdded += null, new SeriesInfoUpdatedEventArgs(series, UpdateReason.Added));
        _metadata.Raise(service => service.SeriesUpdated += null, new SeriesInfoUpdatedEventArgs(series, UpdateReason.Updated));
        _metadata.Raise(service => service.EpisodeAdded += null, new EpisodeInfoUpdatedEventArgs(series, episode, UpdateReason.Added));
        _metadata.Raise(service => service.EpisodeUpdated += null, new EpisodeInfoUpdatedEventArgs(series, episode, UpdateReason.Updated));
        _metadata.Raise(service => service.SeasonAdded += null, new SeasonInfoUpdatedEventArgs(series, season, UpdateReason.Added));
        _metadata.Raise(service => service.SeasonUpdated += null, new SeasonInfoUpdatedEventArgs(series, season, UpdateReason.Updated));
        _metadata.Raise(service => service.MovieAdded += null, new MovieInfoUpdatedEventArgs(movie.Object, UpdateReason.Added));
        _metadata.Raise(service => service.MovieUpdated += null, new MovieInfoUpdatedEventArgs(movie.Object, UpdateReason.Updated));
    }
}
