using FluentAssertions;
using Xunit;

namespace AniSync.Tests;

public class PluginPageTests
{
    [Fact]
    public void GetPages_ExposesAniSyncInShokoPluginNavigation()
    {
        var plugin = new Plugin();

        var pages = plugin.GetPages();

        pages.Should().ContainSingle();
        pages[0].Name.Should().Be("AniSync");
        pages[0].Url.Should().Be("/anisync");
        pages[0].CanEmbed.Should().BeFalse();
    }
}
