using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Plugin;

namespace ShokoRelayMetadataBridge;

public sealed class Plugin : IPlugin
{
    public Guid ID => new("83afc08d-34e3-4af5-8851-4528ec19cb39");
    public string Name => "Shoko Relay Metadata Bridge";
    public string Description => "Refreshes Plex through Relay when Shoko receives updated metadata.";
}

public sealed class PluginServiceRegistration : IPluginServiceRegistration
{
    public static void RegisterServices(IServiceCollection services, IApplicationPaths applicationPaths)
    {
        services.AddHostedService(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<MetadataBridge>>();
            Action<int>? refresh = null;
            try
            {
                // shortcut: Relay exposes no public refresh contract; validate this binding on every stack update.
                var watcher = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => assembly.GetName().Name == "ShokoRelay")
                    .Select(assembly => assembly.GetType("ShokoRelay.Vfs.VfsWatcher"))
                    .OfType<Type>()
                    .Select(provider.GetService)
                    .FirstOrDefault(service => service is not null);
                refresh = BindRefresh(watcher);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not bind the Relay refresh queue");
            }

            return new MetadataBridge(
                provider.GetRequiredService<IMetadataService>(),
                provider.GetRequiredService<ISystemService>(),
                refresh,
                logger);
        });
    }

    internal static Action<int>? BindRefresh(object? watcher)
    {
        var method = watcher?.GetType().GetMethod("TriggerPlexUpdates", [typeof(int), typeof(bool)]);
        if (method is null) return null;
        var refresh = method.CreateDelegate<Action<int, bool>>(watcher);
        return id => refresh(id, false);
    }
}
