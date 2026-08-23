using System;
using FluentAssertions;
using Moq;
using Shoko.Abstractions.Metadata.Image;
using Xunit;

namespace AniSync.Tests;

public class ShokoImageUrlTests
{
    [Fact]
    public void GetShokoImageUrl_UsesCurrentGuidEndpoint()
    {
        var imageID = Guid.Parse("dd944ea3-5bb8-49dd-b7bb-ab8b63218028");
        var image = new Mock<IImage>();
        image.Setup(value => value.ID).Returns(imageID);

        var thumbnailUrl = ShokoAniSyncPlugin.GetShokoImageUrl(image.Object);

        thumbnailUrl.Should().Be("/api/v3/Image/dd944ea3-5bb8-49dd-b7bb-ab8b63218028");
    }
}
