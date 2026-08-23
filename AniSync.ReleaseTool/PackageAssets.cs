using System.IO.Compression;
using System.Security.Cryptography;

namespace AniSync.ReleaseTool;

internal sealed record PackageAssets(string DLLPath, string DLLChecksumPath, string ZipPath, string ZipChecksumPath, string ZipChecksum);

internal static class PackageAssetBuilder
{
    public static PackageAssets Create(string sourceDLLPath, string outputDirectory, ReleaseContract contract, DateTimeOffset archiveTimestamp)
    {
        AssemblyInspector.Validate(sourceDLLPath, contract);

        Directory.CreateDirectory(outputDirectory);
        var dllPath = Path.Combine(outputDirectory, "AniSync.dll");
        if (!Path.GetFullPath(sourceDLLPath).Equals(Path.GetFullPath(dllPath), StringComparison.OrdinalIgnoreCase))
            File.Copy(sourceDLLPath, dllPath, overwrite: true);

        var dllChecksumPath = dllPath + ".sha256";
        WriteChecksumFile(dllPath, dllChecksumPath);

        var zipPath = Path.Combine(outputDirectory, contract.ZipFileName);
        if (File.Exists(zipPath))
            File.Delete(zipPath);

        using (var zipStream = new FileStream(zipPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: false))
        {
            var entry = archive.CreateEntry("AniSync.dll", CompressionLevel.Optimal);
            entry.LastWriteTime = NormalizeZipTimestamp(archiveTimestamp);
            using var entryStream = entry.Open();
            using var dllStream = File.OpenRead(dllPath);
            dllStream.CopyTo(entryStream);
        }

        var zipChecksumPath = zipPath + ".sha256";
        var zipChecksum = WriteChecksumFile(zipPath, zipChecksumPath);
        PackageAssetValidator.Validate(dllPath, dllChecksumPath, zipPath, zipChecksumPath);
        return new PackageAssets(dllPath, dllChecksumPath, zipPath, zipChecksumPath, zipChecksum);
    }

    internal static string ComputeSHA256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string WriteChecksumFile(string assetPath, string checksumPath)
    {
        var checksum = ComputeSHA256(assetPath);
        File.WriteAllText(checksumPath, $"{checksum}  {Path.GetFileName(assetPath)}\n");
        return checksum;
    }

    private static DateTimeOffset NormalizeZipTimestamp(DateTimeOffset timestamp)
    {
        var utc = timestamp.ToUniversalTime();
        if (utc.Year < 1980)
            utc = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second - utc.Second % 2, TimeSpan.Zero);
    }
}

internal static class PackageAssetValidator
{
    public static string Validate(string dllPath, string dllChecksumPath, string zipPath, string zipChecksumPath)
    {
        ValidateChecksumFile(dllPath, dllChecksumPath);
        var zipChecksum = ValidateChecksumFile(zipPath, zipChecksumPath);

        using var archive = ZipFile.OpenRead(zipPath);
        if (archive.Entries.Count != 1 || archive.Entries[0].FullName != "AniSync.dll")
            throw new InvalidOperationException("Package ZIP must contain exactly AniSync.dll at the archive root.");

        using var entryStream = archive.Entries[0].Open();
        using var dllStream = File.OpenRead(dllPath);
        if (!SHA256.HashData(entryStream).SequenceEqual(SHA256.HashData(dllStream)))
            throw new InvalidOperationException("AniSync.dll in the package ZIP does not match the released DLL.");

        return zipChecksum;
    }

    private static string ValidateChecksumFile(string assetPath, string checksumPath)
    {
        if (!File.Exists(checksumPath))
            throw new InvalidOperationException($"Missing checksum file {Path.GetFileName(checksumPath)}.");

        var fields = File.ReadAllText(checksumPath).Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 2 || fields[1] != Path.GetFileName(assetPath))
            throw new InvalidOperationException($"Checksum file {Path.GetFileName(checksumPath)} has an invalid format or asset name.");

        var actual = PackageAssetBuilder.ComputeSHA256(assetPath);
        if (fields[0] != actual)
            throw new InvalidOperationException($"Checksum mismatch for {Path.GetFileName(assetPath)}.");
        return actual;
    }

}
