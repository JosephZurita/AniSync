# Shoko Relay Metadata Bridge

A temporary Shoko plugin tested with Shoko Daily 549 / Relay 0.21.1.
When Shoko receives added or updated series, episode, season or movie metadata,
the bridge passes the linked Shoko series IDs to Relay's existing
`VfsWatcher.TriggerPlexUpdates` method. Relay combines repeated notifications
and uses its configured scan and metadata fixup delays (currently 5 seconds
and 2 minutes).

This closes the case where Shoko obtains episode metadata hours after the
initial import. It does not fetch missing metadata from AniDB or TMDB itself;
Shoko must first receive that data.

The bridge stores no credentials and has no settings, polling loop or separate
service. It skips server startup loading and image-only events to avoid image
sync feedback loops. Events without linked Shoko series are ignored. If Relay
is missing or its method signature changes, the bridge logs a warning and
stays disabled. The existing Relay queue handles merged shows and VFS paths.

## Build and test

From the repository root, using the .NET 10 SDK:

```powershell
dotnet test tools/ShokoRelayMetadataBridge.Tests/ShokoRelayMetadataBridge.Tests.csproj -c Release
dotnet publish tools/ShokoRelayMetadataBridge/ShokoRelayMetadataBridge.csproj -c Release -o tools/ShokoRelayMetadataBridge/bin/publish
```

## Native package installation

Register this repository in Shoko's native plugin package manager and select
the compatible Stable release:

```text
https://raw.githubusercontent.com/JosephZurita/AniSync/bridge-manifest/manifest.json
```

The dedicated `metadata-bridge` workflow tests and packages only the bridge.
Tags named `metadata-bridge-v<version>` publish immutable ZIPs and SHA-256
checksums, then update the independent `bridge-manifest` branch. Each ZIP
contains only the plugin DLL, never Shoko's shared assemblies.

## Manual fallback

Copy only `ShokoRelayMetadataBridge.dll` from the publish directory into a new
`ShokoRelayMetadataBridge` subdirectory of Shoko's `plugins` directory. Do not
copy Shoko's framework or abstraction assemblies. Restart Shoko once and look
for `Metadata bridge active` in the server log.

To remove it, stop Shoko, remove that plugin directory and start Shoko again.
The plugin makes no changes to Shoko or Relay configuration.

## Compatibility

This helper intentionally targets the current installation. Rebuild and review
the Relay method binding when upgrading the Shoko/Relay stack; newer Shoko
prereleases change their plugin contracts. A missing compatible release or
missing `Metadata bridge active:` message blocks coordinated updates.
Remove the helper only after Relay alone passes the same late metadata-event
tests, including startup/image suppression, duplicate IDs and unlinked metadata.
