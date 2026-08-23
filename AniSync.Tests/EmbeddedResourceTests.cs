using System;
using System.Linq;
using AniSync.Controllers;
using FluentAssertions;
using Xunit;

namespace AniSync.Tests;

public class EmbeddedResourceTests
{
    [Theory]
    [InlineData(".js")]
    [InlineData(".css")]
    public void GetEmbeddedAsset_LoadsNestedFrontendAsset(string extension)
    {
        var assembly = typeof(AniSyncController).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name =>
                name.Replace('\\', '/').StartsWith("app/assets/", StringComparison.Ordinal) &&
                name.EndsWith(extension, StringComparison.Ordinal));
        var relativePath = resourceName.Replace('\\', '/')["app/".Length..];

        var asset = AniSyncController.GetEmbeddedAsset(relativePath);

        asset.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetEmbeddedAsset_AcceptsWindowsStyleRelativePath()
    {
        var assembly = typeof(AniSyncController).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name =>
                name.Replace('\\', '/').StartsWith("app/assets/", StringComparison.Ordinal) &&
                name.EndsWith(".js", StringComparison.Ordinal));
        var relativePath = resourceName.Replace('\\', '/')["app/".Length..].Replace('/', '\\');

        var asset = AniSyncController.GetEmbeddedAsset(relativePath);

        asset.Should().NotBeNullOrEmpty();
    }
}
