using System.IO.Compression;
using System.Text.Json;
using AniSync.ReleaseTool;
using FluentAssertions;
using Xunit;

namespace AniSync.Tests;

public class ReleasePackagingTests
{
    private const string AbstractionsPackageVersion = "6.0.0-alpha.81";
    private static readonly DateTimeOffset ReleasedAt = new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BuiltDLLAndGeneratedPackageMatchManifestContract()
    {
        WithTemporaryDirectory(directory =>
        {
            var dllPath = typeof(Plugin).Assembly.Location;
            var identity = AssemblyInspector.Inspect(dllPath);
            var contract = ReleaseContract.Create(identity.AssemblyVersion.Revision, identity.SourceRevision, AbstractionsPackageVersion);

            var assets = PackageAssetBuilder.Create(dllPath, directory, contract, ReleasedAt);
            var firstZipChecksum = assets.ZipChecksum;
            assets = PackageAssetBuilder.Create(dllPath, directory, contract, ReleasedAt);
            assets.ZipChecksum.Should().Be(firstZipChecksum, "identical workflow reruns must produce the same package asset");
            var manifest = ManifestManager.LoadOrCreate(existingPath: null);
            var release = ManifestManager.AddOrReplaceRelease(
                manifest,
                contract,
                $"dev-{contract.BuildNumber}-{contract.CommitSHA[..7]}",
                ReleasedAt,
                assets.ZipChecksum);
            var manifestPath = Path.Combine(directory, "manifest.json");
            ManifestManager.Save(manifest, manifestPath);

            AssemblyInspector.Validate(assets.DLLPath, contract);
            ManifestValidator.Validate(manifest, assets.ZipPath);
            AssertSerializedShokoSchema(manifestPath);
            manifest.Type.Should().Be("package");
            manifest.ID.Should().Be(Guid.Parse(ReleaseContract.PackageID));
            manifest.Name.Should().Be("AniSync");
            release.Version.Should().Be(contract.Version);
            release.SourceRevision.Should().Be(contract.CommitSHA);
            release.Channel.Should().Be("Dev");
            release.Archives.Should().ContainSingle();
            release.Archives[0].Runtime.Should().Be("any");
            release.Archives[0].Abstraction.Should().Be("6.0.0");

            using var zip = ZipFile.OpenRead(assets.ZipPath);
            zip.Entries.Should().ContainSingle().Which.FullName.Should().Be("AniSync.dll");
            PackageAssetBuilder.ComputeSHA256(assets.ZipPath).Should().Be(release.Archives[0].Checksum);
            File.ReadAllText(assets.ZipChecksumPath).Should().StartWith(release.Archives[0].Checksum);
        });
    }

    [Fact]
    public void ManifestUpdateCreatesManifestWhenNoHistoryExists()
    {
        var manifest = ManifestManager.LoadOrCreate(existingPath: null);
        var contract = Contract(1);

        ManifestManager.AddOrReplaceRelease(manifest, contract, "dev-1-0000000", ReleasedAt, Checksum('a'));

        manifest.Releases.Should().ContainSingle();
        manifest.Releases[0].Version.Should().Be("1.0.0-dev.1");
        ManifestValidator.Validate(manifest);
    }

    [Fact]
    public void ManifestRerunReplacesIdenticalVersionWithoutDuplication()
    {
        var manifest = ManifestManager.CreateBaseManifest();
        var contract = Contract(7);
        ManifestManager.AddOrReplaceRelease(manifest, contract, "dev-7-0000000", ReleasedAt, Checksum('a'));
        var firstSerialization = ManifestManager.Serialize(manifest);

        ManifestManager.AddOrReplaceRelease(manifest, contract, "dev-7-0000000", ReleasedAt.AddHours(1), Checksum('a'));

        manifest.Releases.Should().ContainSingle();
        manifest.Releases[0].ReleasedAt.Should().Be(ReleasedAt, "reruns retain the original release timestamp");
        ManifestManager.Serialize(manifest).Should().Be(firstSerialization);
    }

    [Fact]
    public void ManifestLoadRetainsHistoryAndOrdersNewestFirst()
    {
        WithTemporaryDirectory(directory =>
        {
            var existing = ManifestManager.CreateBaseManifest();
            ManifestManager.AddOrReplaceRelease(existing, Contract(1), "dev-1-0000000", ReleasedAt, Checksum('a'));
            ManifestManager.AddOrReplaceRelease(existing, Contract(2), "dev-2-0000000", ReleasedAt.AddMinutes(1), Checksum('b'));
            var path = Path.Combine(directory, "manifest.json");
            ManifestManager.Save(existing, path);

            var loaded = ManifestManager.LoadOrCreate(path);
            ManifestManager.AddOrReplaceRelease(loaded, Contract(3), "dev-3-0000000", ReleasedAt.AddMinutes(2), Checksum('c'));

            loaded.Releases.Select(release => release.Version).Should().Equal(
                "1.0.0-dev.3",
                "1.0.0-dev.2",
                "1.0.0-dev.1");
        });
    }

    [Fact]
    public void ManifestRetainsOnlyNewestThirtyDevelopmentReleases()
    {
        WithTemporaryDirectory(directory =>
        {
            var generatedReleases = new List<PackageRelease>();
            for (var buildNumber = 1; buildNumber <= 35; buildNumber++)
            {
                var singleReleaseManifest = ManifestManager.CreateBaseManifest();
                generatedReleases.Add(ManifestManager.AddOrReplaceRelease(
                    singleReleaseManifest,
                    Contract(buildNumber),
                    $"dev-{buildNumber}-0000000",
                    ReleasedAt.AddMinutes(buildNumber),
                    Checksum((char)('a' + buildNumber % 6))));
            }

            var oversizedManifest = ManifestManager.CreateBaseManifest();
            oversizedManifest.Releases = generatedReleases.OrderByDescending(release => ManifestManager.ParseBuildNumber(release.Version)).ToList();
            var path = Path.Combine(directory, "manifest.json");
            File.WriteAllText(path, ManifestManager.Serialize(oversizedManifest));

            var manifest = ManifestManager.LoadOrCreate(path);
            ManifestManager.AddOrReplaceRelease(manifest, Contract(36), "dev-36-0000000", ReleasedAt.AddMinutes(36), Checksum('a'));

            manifest.Releases.Should().HaveCount(30);
            manifest.Releases.First().Version.Should().Be("1.0.0-dev.36");
            manifest.Releases.Last().Version.Should().Be("1.0.0-dev.7");
            ManifestValidator.Validate(manifest);
        });
    }

    [Fact]
    public void ManifestSerializationIsDeterministic()
    {
        var manifest = ManifestManager.CreateBaseManifest();
        ManifestManager.AddOrReplaceRelease(manifest, Contract(4), "dev-4-0000000", ReleasedAt, Checksum('d'));

        ManifestManager.Serialize(manifest).Should().Be(ManifestManager.Serialize(manifest));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65535)]
    public void DevelopmentBuildNumberOutsideAssemblyRevisionRangeFails(int buildNumber)
    {
        var action = () => ReleaseContract.Create(buildNumber, CommitFor(1), AbstractionsPackageVersion);

        action.Should().Throw<InvalidOperationException>().WithMessage("*assembly revision range*");
    }

    private static ReleaseContract Contract(int buildNumber)
        => ReleaseContract.Create(buildNumber, CommitFor(buildNumber), AbstractionsPackageVersion);

    private static string CommitFor(int buildNumber) => buildNumber.ToString("x40");

    private static string Checksum(char value) => new(value, 64);

    private static void AssertSerializedShokoSchema(string manifestPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = document.RootElement;
        root.EnumerateObject().Select(property => property.Name).Should().Equal(
            "type",
            "id",
            "name",
            "overview",
            "authors",
            "repository_url",
            "homepage_url",
            "image_url",
            "tags",
            "releases");

        var release = root.GetProperty("releases").EnumerateArray().Single();
        release.EnumerateObject().Select(property => property.Name).Should().Equal(
            "version",
            "tag",
            "source_revision",
            "released_at",
            "channel",
            "release_notes",
            "archives");

        var archive = release.GetProperty("archives").EnumerateArray().Single();
        archive.EnumerateObject().Select(property => property.Name).Should().Equal(
            "runtime",
            "abstraction",
            "url",
            "checksum");
    }

    private static void WithTemporaryDirectory(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"AniSync.ReleasePackagingTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            action(directory);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
