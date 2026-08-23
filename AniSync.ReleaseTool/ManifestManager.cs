using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AniSync.ReleaseTool;

internal static class ManifestManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static PackageManifest LoadOrCreate(string? existingPath)
    {
        if (string.IsNullOrWhiteSpace(existingPath) || !File.Exists(existingPath))
            return CreateBaseManifest();

        var manifest = JsonSerializer.Deserialize<PackageManifest>(File.ReadAllText(existingPath), JsonOptions)
            ?? throw new InvalidOperationException("Existing manifest.json could not be parsed.");
        ManifestValidator.Validate(manifest, enforceRetentionLimit: false);
        return manifest;
    }

    public static PackageRelease AddOrReplaceRelease(
        PackageManifest manifest,
        ReleaseContract contract,
        string tag,
        DateTimeOffset releasedAt,
        string zipChecksum)
    {
        if (string.IsNullOrWhiteSpace(tag))
            throw new InvalidOperationException("Release tag is required.");
        if (releasedAt.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("Release timestamp must be expressed in UTC.");

        var existingRelease = manifest.Releases.SingleOrDefault(release => release.Version == contract.Version);
        if (existingRelease != null && existingRelease.SourceRevision != contract.CommitSHA)
            throw new InvalidOperationException($"Version {contract.Version} already belongs to source revision {existingRelease.SourceRevision}; refusing a build-number collision.");

        var effectiveReleasedAt = existingRelease?.ReleasedAt ?? releasedAt;
        var zipUrl = $"{ReleaseContract.RepositoryUrl}/releases/download/{Uri.EscapeDataString(tag)}/{Uri.EscapeDataString(contract.ZipFileName)}";
        var release = new PackageRelease
        {
            Version = contract.Version,
            Tag = tag,
            SourceRevision = contract.CommitSHA,
            ReleasedAt = effectiveReleasedAt,
            Channel = ReleaseContract.Channel,
            ReleaseNotes = $"Automated AniSync development build {contract.Version} from commit {contract.CommitSHA} using Shoko.Abstractions {contract.AbstractionsPackageVersion}.",
            Archives =
            [
                new PackageArchive
                {
                    Runtime = ReleaseContract.RuntimeIdentifier,
                    Abstraction = ReleaseContract.AbstractionVersion,
                    Url = zipUrl,
                    Checksum = zipChecksum,
                },
            ],
        };

        if (existingRelease != null)
            manifest.Releases.Remove(existingRelease);
        manifest.Releases.Add(release);
        manifest.Releases = manifest.Releases
            .OrderByDescending(item => ParseBuildNumber(item.Version))
            .ThenByDescending(item => item.ReleasedAt)
            .Take(ReleaseContract.RetainedReleaseCount)
            .ToList();

        ApplyPublicMetadata(manifest);
        ManifestValidator.Validate(manifest);
        return release;
    }

    public static void Save(PackageManifest manifest, string outputPath)
    {
        ManifestValidator.Validate(manifest);
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (directory != null)
            Directory.CreateDirectory(directory);

        var json = Serialize(manifest);
        File.WriteAllText(outputPath, json);

        var reparsed = JsonSerializer.Deserialize<PackageManifest>(File.ReadAllText(outputPath), JsonOptions)
            ?? throw new InvalidOperationException("Generated manifest.json could not be parsed.");
        ManifestValidator.Validate(reparsed);
        if (Serialize(reparsed) != json)
            throw new InvalidOperationException("Generated manifest.json is not deterministically serialized.");
    }

    internal static string Serialize(PackageManifest manifest)
        => JsonSerializer.Serialize(manifest, JsonOptions).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";

    internal static PackageManifest CreateBaseManifest()
    {
        var manifest = new PackageManifest
        {
            Type = "package",
            ID = Guid.Parse(ReleaseContract.PackageID),
            Name = ReleaseContract.PackageName,
            Overview = "Synchronizes Shoko watch state and episode progress to AniList and MyAnimeList.",
            Authors = "JosephZurita",
            RepositoryUrl = ReleaseContract.RepositoryUrl,
            HomepageUrl = ReleaseContract.RepositoryUrl,
            ImageUrl = ReleaseContract.ImageUrl,
            Tags = ["shoko", "anime", "watch-state", "sync", "anilist", "myanimelist", "mal", "scrobbling", "plugin"],
            Releases = [],
        };
        return manifest;
    }

    private static void ApplyPublicMetadata(PackageManifest manifest)
    {
        var expected = CreateBaseManifest();
        manifest.Type = expected.Type;
        manifest.ID = expected.ID;
        manifest.Name = expected.Name;
        manifest.Overview = expected.Overview;
        manifest.Authors = expected.Authors;
        manifest.RepositoryUrl = expected.RepositoryUrl;
        manifest.HomepageUrl = expected.HomepageUrl;
        manifest.ImageUrl = expected.ImageUrl;
        manifest.Tags = expected.Tags;
    }

    internal static int ParseBuildNumber(string version)
    {
        const string prefix = "1.0.0-dev.";
        if (!version.StartsWith(prefix, StringComparison.Ordinal) ||
            !int.TryParse(version[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var buildNumber))
            throw new InvalidOperationException($"Release version '{version}' is not an AniSync development version.");
        return buildNumber;
    }
}

internal static class ManifestValidator
{
    private static readonly Regex ChecksumPattern = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant);
    private static readonly Regex CommitPattern = new("^[0-9a-f]{40}$", RegexOptions.CultureInvariant);

    public static void Validate(PackageManifest manifest, string? zipPath = null, bool enforceRetentionLimit = true)
    {
        Require(manifest.Type == "package", "Manifest type must be package.");
        Require(manifest.ID == Guid.Parse(ReleaseContract.PackageID), "Manifest package ID does not match AniSync.");
        Require(manifest.Name == ReleaseContract.PackageName, "Manifest package name must be AniSync.");
        Require(!string.IsNullOrWhiteSpace(manifest.Overview), "Manifest overview is required.");
        Require(manifest.Authors == "JosephZurita", "Manifest authors must be JosephZurita.");
        Require(IsExpectedRepositoryUrl(manifest.RepositoryUrl), "Manifest repository_url is invalid.");
        Require(IsExpectedRepositoryUrl(manifest.HomepageUrl), "Manifest homepage_url is invalid.");
        Require(Uri.TryCreate(manifest.ImageUrl, UriKind.Absolute, out var imageUri) && imageUri.Scheme == Uri.UriSchemeHttps, "Manifest image_url must be an absolute HTTPS URL.");
        Require(manifest.Tags is { Count: > 0 and <= 20 }, "Manifest must contain between 1 and 20 tags.");
        Require(manifest.Tags.Distinct(StringComparer.Ordinal).Count() == manifest.Tags.Count, "Manifest tags must be unique.");
        var releases = manifest.Releases ?? throw new InvalidOperationException("Manifest releases are required.");
        if (enforceRetentionLimit)
            Require(releases.Count <= ReleaseContract.RetainedReleaseCount, "Manifest contains more than 30 retained releases.");

        var versions = new HashSet<string>(StringComparer.Ordinal);
        var previousBuildNumber = int.MaxValue;
        foreach (var release in releases)
        {
            var buildNumber = ManifestManager.ParseBuildNumber(release.Version);
            Require(buildNumber <= ReleaseContract.MaximumBuildNumber, $"Release {release.Version} exceeds the CLR assembly revision range.");
            Require(buildNumber <= previousBuildNumber, "Manifest releases must be ordered newest first.");
            previousBuildNumber = buildNumber;
            Require(versions.Add(release.Version), $"Manifest contains duplicate release {release.Version}.");
            Require(!string.IsNullOrWhiteSpace(release.Tag), $"Release {release.Version} is missing tag.");
            Require(release.SourceRevision != null && CommitPattern.IsMatch(release.SourceRevision), $"Release {release.Version} has an invalid source_revision.");
            Require(release.ReleasedAt.Offset == TimeSpan.Zero, $"Release {release.Version} released_at is not UTC.");
            Require(release.Channel == ReleaseContract.Channel, $"Release {release.Version} must use the Dev channel.");
            Require(!string.IsNullOrWhiteSpace(release.ReleaseNotes) && release.ReleaseNotes.Contains("Shoko.Abstractions 6.0.0-", StringComparison.Ordinal),
                $"Release {release.Version} notes must include the exact Shoko.Abstractions alpha version.");
            Require(release.Archives is { Count: 1 }, $"Release {release.Version} must contain exactly one archive.");

            var archive = release.Archives[0];
            Require(archive.Runtime == ReleaseContract.RuntimeIdentifier, $"Release {release.Version} runtime must be any.");
            Require(archive.Abstraction == ReleaseContract.AbstractionVersion, $"Release {release.Version} abstraction must be 6.0.0.");
            Require(ChecksumPattern.IsMatch(archive.Checksum), $"Release {release.Version} checksum must be a lowercase SHA-256 hash.");
            Require(Uri.TryCreate(archive.Url, UriKind.Absolute, out var assetUri) && assetUri.Scheme == Uri.UriSchemeHttps, $"Release {release.Version} archive URL must be absolute HTTPS.");
            var expectedUrl = $"{ReleaseContract.RepositoryUrl}/releases/download/{Uri.EscapeDataString(release.Tag)}/{Uri.EscapeDataString($"AniSync-{release.Version}.zip")}";
            Require(archive.Url == expectedUrl, $"Release {release.Version} archive URL does not match its GitHub release asset.");

            if (zipPath != null && Path.GetFileName(zipPath) == $"AniSync-{release.Version}.zip")
                Require(PackageAssetBuilder.ComputeSHA256(zipPath) == archive.Checksum, $"Release {release.Version} manifest checksum does not match its ZIP.");
        }
    }

    private static bool IsExpectedRepositoryUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && value == ReleaseContract.RepositoryUrl;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
