using System.Globalization;
using System.Text.RegularExpressions;

namespace AniSync.ReleaseTool;

internal sealed record ReleaseContract
{
    public const string PackageID = "73455c6e-c830-57fc-9650-41c953ff7be9";
    public const string PackageName = "AniSync";
    public const string RepositoryUrl = "https://github.com/JosephZurita/AniSync";
    public const string ImageUrl = "https://raw.githubusercontent.com/JosephZurita/AniSync/master/docs/banner-light.png";
    public const string RuntimeIdentifier = "any";
    public const string AbstractionVersion = "6.0.0";
    public const string Channel = "Dev";
    public const int MaximumBuildNumber = 65534;
    public const int RetainedReleaseCount = 30;

    private static readonly Regex CommitPattern = new("^[0-9a-f]{40}$", RegexOptions.CultureInvariant);
    private static readonly Regex AbstractionsPackagePattern = new("^6\\.0\\.0(?:-[0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant);

    public required int BuildNumber { get; init; }

    public required string CommitSHA { get; init; }

    public required string AbstractionsPackageVersion { get; init; }

    public string Version => $"1.0.0-dev.{BuildNumber.ToString(CultureInfo.InvariantCulture)}";

    public Version AssemblyVersion => new(1, 0, 0, BuildNumber);

    public string ZipFileName => $"AniSync-{Version}.zip";

    public static ReleaseContract Create(int buildNumber, string commitSHA, string abstractionsPackageVersion)
    {
        if (buildNumber is < 0 or > MaximumBuildNumber)
            throw new InvalidOperationException($"Development build number {buildNumber} is outside the CLR assembly revision range 0-{MaximumBuildNumber}.");
        if (!CommitPattern.IsMatch(commitSHA))
            throw new InvalidOperationException("Source revision must be a lowercase, full 40-character Git commit SHA.");
        if (!AbstractionsPackagePattern.IsMatch(abstractionsPackageVersion))
            throw new InvalidOperationException($"Shoko.Abstractions package version '{abstractionsPackageVersion}' does not represent the required 6.0.0 abstraction.");

        return new ReleaseContract
        {
            BuildNumber = buildNumber,
            CommitSHA = commitSHA,
            AbstractionsPackageVersion = abstractionsPackageVersion,
        };
    }
}
