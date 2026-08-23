using System.Globalization;

namespace AniSync.ReleaseTool;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
                throw new InvalidOperationException("Expected a command: pack or manifest.");

            var options = ParseOptions(args[1..]);
            return args[0] switch
            {
                "pack" => Pack(options),
                "manifest" => GenerateManifest(options),
                _ => throw new InvalidOperationException($"Unknown command '{args[0]}'. Expected pack or manifest."),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"AniSync release validation failed: {ex.Message}");
            return 1;
        }
    }

    private static int Pack(IReadOnlyDictionary<string, string> options)
    {
        var contract = GetContract(options);
        var timestamp = ParseUtcTimestamp(GetRequired(options, "timestamp"));
        var assets = PackageAssetBuilder.Create(
            GetRequired(options, "dll"),
            GetRequired(options, "output"),
            contract,
            timestamp);
        Console.WriteLine($"Created {Path.GetFileName(assets.ZipPath)} ({assets.ZipChecksum})");
        return 0;
    }

    private static int GenerateManifest(IReadOnlyDictionary<string, string> options)
    {
        var contract = GetContract(options);
        var dllPath = GetRequired(options, "dll");
        var zipPath = GetRequired(options, "zip");
        var dllChecksumPath = dllPath + ".sha256";
        var zipChecksumPath = zipPath + ".sha256";
        AssemblyInspector.Validate(dllPath, contract);
        var zipChecksum = PackageAssetValidator.Validate(dllPath, dllChecksumPath, zipPath, zipChecksumPath);

        options.TryGetValue("existing", out var existingPath);
        var manifest = ManifestManager.LoadOrCreate(existingPath);
        ManifestManager.AddOrReplaceRelease(
            manifest,
            contract,
            GetRequired(options, "tag"),
            ParseUtcTimestamp(GetRequired(options, "released-at")),
            zipChecksum);
        ManifestValidator.Validate(manifest, zipPath);
        ManifestManager.Save(manifest, GetRequired(options, "output"));
        Console.WriteLine($"Generated manifest.json for {contract.Version}");
        return 0;
    }

    private static ReleaseContract GetContract(IReadOnlyDictionary<string, string> options)
    {
        var rawBuildNumber = GetRequired(options, "run-number");
        if (!int.TryParse(rawBuildNumber, NumberStyles.None, CultureInfo.InvariantCulture, out var buildNumber))
            throw new InvalidOperationException($"Development build number '{rawBuildNumber}' is not numeric.");
        return ReleaseContract.Create(
            buildNumber,
            GetRequired(options, "commit"),
            GetRequired(options, "abstractions-version"));
    }

    private static DateTimeOffset ParseUtcTimestamp(string raw)
    {
        if (!DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp))
            throw new InvalidOperationException($"Timestamp '{raw}' is not a valid ISO 8601 timestamp.");
        return timestamp;
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal))
                throw new InvalidOperationException("Options must use --name value pairs.");
            var name = args[index][2..];
            if (!options.TryAdd(name, args[index + 1]))
                throw new InvalidOperationException($"Option --{name} was specified more than once.");
        }
        return options;
    }

    private static string GetRequired(IReadOnlyDictionary<string, string> options, string name)
        => options.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Missing required option --{name}.");
}
