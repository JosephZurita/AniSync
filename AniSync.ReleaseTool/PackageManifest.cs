using System.Text.Json.Serialization;

namespace AniSync.ReleaseTool;

/// <summary>
/// The remote package-manifest shape consumed by Shoko Server's
/// RemotePackageManifestInfo model.
/// </summary>
internal sealed class PackageManifest
{
    public required string Type { get; set; }

    public required Guid ID { get; set; }

    public required string Name { get; set; }

    public required string Overview { get; set; }

    public required string Authors { get; set; }

    public required string RepositoryUrl { get; set; }

    public required string HomepageUrl { get; set; }

    public required string ImageUrl { get; set; }

    public required List<string> Tags { get; set; }

    public required List<PackageRelease> Releases { get; set; }
}

internal sealed class PackageRelease
{
    public required string Version { get; set; }

    public required string Tag { get; set; }

    public required string SourceRevision { get; set; }

    public required DateTimeOffset ReleasedAt { get; set; }

    public required string Channel { get; set; }

    public required string ReleaseNotes { get; set; }

    public required List<PackageArchive> Archives { get; set; }
}

internal sealed class PackageArchive
{
    public required string Runtime { get; set; }

    public required string Abstraction { get; set; }

    public required string Url { get; set; }

    public required string Checksum { get; set; }
}
