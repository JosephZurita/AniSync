using System.Reflection;
using System.Runtime.Loader;

namespace AniSync.ReleaseTool;

internal sealed record PluginAssemblyIdentity(
    Version AssemblyVersion,
    string FileVersion,
    string InformationalVersion,
    string PackageID,
    string PackageName,
    string RuntimeIdentifier,
    string ReleaseChannel,
    string SourceRevision,
    Version AbstractionVersion);

internal static class AssemblyInspector
{
    public static PluginAssemblyIdentity Inspect(string dllPath)
    {
        var fullPath = Path.GetFullPath(dllPath);
        var assemblyName = AssemblyName.GetAssemblyName(fullPath);
        var inspectionDirectory = Path.Combine(Path.GetTempPath(), $"AniSync.ReleaseTool-{Guid.NewGuid():N}");
        Directory.CreateDirectory(inspectionDirectory);
        var inspectionPath = Path.Combine(inspectionDirectory, Path.GetFileName(fullPath));
        File.Copy(fullPath, inspectionPath);
        var loadContext = new AssemblyLoadContext($"AniSync.ReleaseTool.{Guid.NewGuid():N}", isCollectible: true);
        PluginAssemblyIdentity identity;
        try
        {
            var assembly = loadContext.LoadFromAssemblyPath(inspectionPath);
            var attributes = assembly.GetCustomAttributesData();
            var metadata = attributes
                .Where(attribute => attribute.AttributeType.FullName == typeof(AssemblyMetadataAttribute).FullName)
                .Where(attribute => attribute.ConstructorArguments.Count == 2)
                .ToDictionary(
                    attribute => (string)attribute.ConstructorArguments[0].Value!,
                    attribute => (string)attribute.ConstructorArguments[1].Value!,
                    StringComparer.Ordinal);

            var fileVersion = ReadSingleValueAttribute(attributes, typeof(AssemblyFileVersionAttribute));
            var informationalVersion = ReadSingleValueAttribute(attributes, typeof(AssemblyInformationalVersionAttribute));
            var abstractionVersion = assembly.GetReferencedAssemblies()
                .Single(reference => reference.Name == "Shoko.Abstractions")
                .Version ?? throw new InvalidOperationException("Shoko.Abstractions reference has no assembly version.");

            identity = new PluginAssemblyIdentity(
                assemblyName.Version ?? throw new InvalidOperationException("AniSync.dll has no assembly version."),
                fileVersion,
                informationalVersion,
                ReadMetadata(metadata, "PackageID"),
                ReadMetadata(metadata, "PackageName"),
                ReadMetadata(metadata, "RuntimeIdentifier"),
                ReadMetadata(metadata, "ReleaseChannel"),
                ReadMetadata(metadata, "SourceRevision"),
                abstractionVersion);
        }
        finally
        {
            loadContext.Unload();
        }

        // Collect the temporary load context so Windows releases the inspected DLL
        // before tests or release validation replace/delete staging directories.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        try
        {
            Directory.Delete(inspectionDirectory, recursive: true);
        }
        catch (IOException)
        {
            // The temporary shadow copy can be reclaimed by the OS if a runtime
            // still has a transient metadata handle after unloading the context.
        }
        catch (UnauthorizedAccessException)
        {
            // See the IOException case above.
        }
        return identity;
    }

    public static void Validate(string dllPath, ReleaseContract contract)
    {
        var identity = Inspect(dllPath);
        var expectedNumericVersion = contract.AssemblyVersion.ToString();

        Require(identity.AssemblyVersion == contract.AssemblyVersion, $"AniSync.dll AssemblyVersion is {identity.AssemblyVersion}; expected {expectedNumericVersion}.");
        Require(identity.FileVersion == expectedNumericVersion, $"AniSync.dll AssemblyFileVersion is {identity.FileVersion}; expected {expectedNumericVersion}.");
        Require(identity.InformationalVersion.StartsWith(contract.Version, StringComparison.Ordinal), $"AniSync.dll AssemblyInformationalVersion does not start with {contract.Version}.");
        Require(identity.InformationalVersion.Contains(contract.CommitSHA, StringComparison.Ordinal), "AniSync.dll AssemblyInformationalVersion does not contain the full source revision.");
        Require(identity.PackageID == ReleaseContract.PackageID, $"AniSync.dll PackageID is {identity.PackageID}; expected {ReleaseContract.PackageID}.");
        Require(identity.PackageName == ReleaseContract.PackageName, $"AniSync.dll PackageName is {identity.PackageName}; expected {ReleaseContract.PackageName}.");
        Require(identity.RuntimeIdentifier == ReleaseContract.RuntimeIdentifier, $"AniSync.dll RuntimeIdentifier is {identity.RuntimeIdentifier}; expected any.");
        Require(identity.ReleaseChannel == ReleaseContract.Channel, $"AniSync.dll ReleaseChannel is {identity.ReleaseChannel}; expected Dev.");
        Require(identity.SourceRevision == contract.CommitSHA, "AniSync.dll SourceRevision does not match the manifest source revision.");
        Require(identity.AbstractionVersion.Major == 6 && identity.AbstractionVersion.Minor == 0 && identity.AbstractionVersion.Build == 0,
            $"AniSync.dll references Shoko.Abstractions {identity.AbstractionVersion}; expected abstraction 6.0.0.");
    }

    private static string ReadSingleValueAttribute(IList<CustomAttributeData> attributes, Type attributeType)
    {
        var attribute = attributes.Single(value => value.AttributeType.FullName == attributeType.FullName);
        return (string)attribute.ConstructorArguments.Single().Value!;
    }

    private static string ReadMetadata(IReadOnlyDictionary<string, string> metadata, string key)
        => metadata.TryGetValue(key, out var value)
            ? value
            : throw new InvalidOperationException($"AniSync.dll is missing required assembly metadata '{key}'.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
