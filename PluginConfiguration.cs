using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.RoTranslator.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>Cheia API DeepL (free sau pro).</summary>
        public string DeepLApiKey { get; set; } = string.Empty;

        /// <summary>Foloseste endpoint-ul DeepL Pro (true) sau Free (false).</summary>
        public bool UseProApi { get; set; } = false;

        /// <summary>Traduce titlul itemului.</summary>
        public bool TranslateTitle { get; set; } = false;

        /// <summary>Traduce descrierea/overview.</summary>
        public bool TranslateOverview { get; set; } = true;

        /// <summary>Traduce tagline-ul filmelor.</summary>
        public bool TranslateTagline { get; set; } = true;

        /// <summary>Traduce genurile folosind dictionarul intern (fara apel API).</summary>
        public bool TranslateGenres { get; set; } = true;

        /// <summary>Traduce etichetele/tags folosind dictionarul intern (fara apel API).</summary>
        public bool TranslateTags { get; set; } = true;

        /// <summary>
        /// Suprascrie chiar daca campul e deja blocat/tradus.
        /// ATENTIE: va retraduce tot de la zero.
        /// </summary>
        public bool OverwriteExisting { get; set; } = false;

        /// <summary>Numarul maxim de cereri DeepL API per minut.</summary>
        public int RequestsPerMinute { get; set; } = 30;
    }
}
