using Jellyfin.Plugin.RoTranslator.Services;
using Jellyfin.Plugin.RoTranslator.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.RoTranslator
{
    public class PluginServiceRegistrator : IPluginServiceRegistrator
    {
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            serviceCollection.AddHttpClient("DeepL");
            serviceCollection.AddSingleton<DeepLTranslationService>();
            serviceCollection.AddSingleton<TranslationLockService>();
            serviceCollection.AddScoped<TranslateMetadataTask>();
            serviceCollection.AddScoped<UnlockMetadataTask>();
        }
    }
}