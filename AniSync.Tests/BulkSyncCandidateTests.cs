using FluentAssertions;
using Moq;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;
using AniSync.Models.Mal;
using Xunit;

namespace AniSync.Tests;

public class BulkSyncCandidateTests
{
    [Fact]
    public void BuildBulkSyncCandidates_SelectsHighestWatchedNormalEpisodePerSeries()
    {
        var userData = new[]
        {
            CreateEpisodeData(1, 1, EpisodeType.Episode, watched: true),
            CreateEpisodeData(1, 3, EpisodeType.Episode, watched: true),
            CreateEpisodeData(1, 4, EpisodeType.Episode, watched: false),
            CreateEpisodeData(1, 99, EpisodeType.Special, watched: true),
            CreateEpisodeData(2, 2, EpisodeType.Episode, watched: true)
        };

        var candidates = ShokoAniSyncPlugin.BuildBulkSyncCandidates(userData);

        candidates.Should().HaveCount(2);
        candidates.Select(candidate => (candidate.Episode.SeriesID, candidate.Episode.EpisodeNumber))
            .Should().Equal((1, 3), (2, 2));
    }

    [Fact]
    public void BuildBulkSyncCandidates_ExcludesHistoricalPlaybackCountWhenEpisodeIsUnwatched()
    {
        var data = CreateEpisodeData(7, 5, EpisodeType.Episode, watched: false, playbackCount: 1);

        var candidates = ShokoAniSyncPlugin.BuildBulkSyncCandidates([data]);

        candidates.Should().BeEmpty();
    }

    [Fact]
    public void SelectBulkSyncCandidates_OnlyKeepsReviewedSeries()
    {
        var candidates = ShokoAniSyncPlugin.BuildBulkSyncCandidates([
            CreateEpisodeData(1, 3, EpisodeType.Episode, watched: true),
            CreateEpisodeData(2, 6, EpisodeType.Episode, watched: true),
            CreateEpisodeData(3, 9, EpisodeType.Episode, watched: true)
        ]);

        var selected = ShokoAniSyncPlugin.SelectBulkSyncCandidates(candidates, [3, 1, 3, 999]);

        selected.Select(candidate => candidate.Episode.SeriesID).Should().Equal(1, 3);
    }

    [Fact]
    public void NeedsBulkSync_ReturnsTrueWhenSeriesIsMissingFromProviderList()
    {
        var anime = new Anime { NumEpisodes = 12 };

        ShokoAniSyncPlugin.NeedsBulkSync(anime, 12, syncOnlyCompleted: true).Should().BeTrue();
    }

    [Theory]
    [InlineData(4, 7, false, true)]
    [InlineData(7, 7, false, false)]
    [InlineData(9, 7, false, false)]
    [InlineData(4, 7, true, false)]
    [InlineData(4, 12, true, true)]
    public void NeedsBulkSync_OnlyIncludesProgressThatWouldActuallyChangeProvider(
        int providerProgress,
        int shokoProgress,
        bool syncOnlyCompleted,
        bool expected)
    {
        var anime = new Anime
        {
            NumEpisodes = 12,
            MyListStatus = new MyListStatus
            {
                Status = providerProgress >= 12 ? Status.Completed : Status.Watching,
                NumEpisodesWatched = providerProgress
            }
        };

        ShokoAniSyncPlugin.NeedsBulkSync(anime, shokoProgress, syncOnlyCompleted).Should().Be(expected);
    }

    private static IEpisodeUserData CreateEpisodeData(
        int seriesID,
        int episodeNumber,
        EpisodeType episodeType,
        bool watched,
        int playbackCount = 0)
    {
        var series = new Mock<IShokoSeries>();
        var episode = new Mock<IShokoEpisode>();
        episode.SetupGet(value => value.SeriesID).Returns(seriesID);
        episode.SetupGet(value => value.EpisodeNumber).Returns(episodeNumber);
        episode.SetupGet(value => value.Type).Returns(episodeType);
        episode.SetupGet(value => value.Series).Returns(series.Object);

        var data = new Mock<IEpisodeUserData>();
        data.SetupGet(value => value.Episode).Returns(episode.Object);
        data.SetupGet(value => value.LastPlayedAt).Returns(watched ? DateTime.UtcNow : null);
        data.SetupGet(value => value.PlaybackCount).Returns(playbackCount);
        return data.Object;
    }
}
