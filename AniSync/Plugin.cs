using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Abstractions.Utilities;
using System;
using System.Collections.Generic;

namespace AniSync
{
    public class Plugin : IPlugin
    {
        public Guid ID => UuidUtility.GetV5(GetType().FullName!);
        public string Name => "AniSync";
        public string Description => "Syncs watch state from Shoko to MyAnimeList and other providers";
        public string EmbeddedThumbnailResourceName => string.Empty;

        public IReadOnlyList<PluginPage> GetPages() =>
        [
            new()
            {
                Name = "AniSync",
                Url = "/anisync",
                CanEmbed = false,
            },
        ];
    }
}
