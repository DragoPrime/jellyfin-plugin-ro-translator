using System.Collections.Generic;

namespace Jellyfin.Plugin.RoTranslator.Services
{
    /// <summary>
    /// Dictionare statice pentru genuri si etichete comune din TMDB.
    /// Traducere rapida fara apel API pentru termeni standard.
    /// </summary>
    public static class GenreTagDictionary
    {
        public static readonly Dictionary<string, string> Genres = new(System.StringComparer.OrdinalIgnoreCase)
        {
            // Genuri film/serial TMDB
            { "Action", "Acțiune" },
            { "Action & Adventure", "Acțiune și Aventură" },
            { "Adventure", "Aventură" },
            { "Animation", "Animație" },
            { "Anime", "Anime" },
            { "Biography", "Biografie" },
            { "Comedy", "Comedie" },
            { "Crime", "Crimă" },
            { "Documentary", "Documentar" },
            { "Drama", "Dramă" },
            { "Family", "Familie" },
            { "Fantasy", "Fantezie" },
            { "History", "Istorie" },
            { "Horror", "Horror" },
            { "Kids", "Pentru Copii" },
            { "Music", "Muzică" },
            { "Musical", "Musical" },
            { "Mystery", "Mister" },
            { "News", "Știri" },
            { "Reality", "Reality" },
            { "Romance", "Romantic" },
            { "Sci-Fi", "SF" },
            { "Sci-Fi & Fantasy", "SF și Fantezie" },
            { "Science Fiction", "Science Fiction" },
            { "Soap", "Telenovelă" },
            { "Sport", "Sport" },
            { "Sports", "Sport" },
            { "Talk", "Talk Show" },
            { "Thriller", "Thriller" },
            { "TV Movie", "Film TV" },
            { "War", "Război" },
            { "War & Politics", "Război și Politică" },
            { "Western", "Western" },
        };

        public static readonly Dictionary<string, string> Tags = new(System.StringComparer.OrdinalIgnoreCase)
        {
            // Etichete comune TMDB/Jellyfin
            { "Based on Novel", "Bazat pe roman" },
            { "Based on Comic", "Bazat pe benzi desenate" },
            { "Based on Manga", "Bazat pe manga" },
            { "Based on True Story", "Bazat pe o poveste adevărată" },
            { "Sequel", "Continuare" },
            { "Prequel", "Prequel" },
            { "Remake", "Remake" },
            { "Spin-off", "Spin-off" },
            { "Superhero", "Supererou" },
            { "Alien", "Extraterestru" },
            { "Time Travel", "Călătorie în timp" },
            { "Space", "Spațiu" },
            { "Magic", "Magie" },
            { "Sword and Sorcery", "Sabie și Vrăjitorie" },
            { "Martial Arts", "Arte marțiale" },
            { "Post Apocalyptic", "Post-apocaliptic" },
            { "Dystopia", "Distopie" },
            { "Robots", "Roboți" },
            { "Artificial Intelligence", "Inteligență artificială" },
            { "Zombies", "Zombi" },
            { "Vampire", "Vampir" },
            { "Werewolf", "Vârcolac" },
            { "Ghost", "Fantomă" },
            { "Supernatural", "Supranatural" },
            { "High School", "Liceu" },
            { "School", "Școală" },
            { "College", "Facultate" },
            { "Love Triangle", "Triunghi amoros" },
            { "Coming of Age", "Maturizare" },
            { "Found Family", "Familie adoptivă" },
            { "Revenge", "Răzbunare" },
            { "Survival", "Supraviețuire" },
            { "Tournament", "Turneu" },
            { "Training", "Antrenament" },
            { "Military", "Militar" },
            { "Politics", "Politică" },
            { "Assassin", "Asasin" },
            { "Detective", "Detectiv" },
            { "Police", "Poliție" },
            { "Spy", "Spion" },
            { "Heist", "Jaf" },
            { "Prison", "Închisoare" },
            { "Medical", "Medical" },
            { "Cooking", "Gătit" },
            { "Music", "Muzică" },
            { "Sport", "Sport" },
            { "Isekai", "Isekai" },
            { "Mecha", "Mecha" },
            { "Harem", "Harem" },
            { "Slice of Life", "Viața de zi cu zi" },
            { "Psychological", "Psihologic" },
            { "Gore", "Gore" },
            { "Dark", "Întunecat" },
            { "Ecchi", "Ecchi" },
            { "Shounen", "Shounen" },
            { "Shoujo", "Shoujo" },
            { "Seinen", "Seinen" },
            { "Josei", "Josei" },
        };

        /// <summary>
        /// Incearca sa traducă un gen. Returneaza genul original daca nu e in dictionar.
        /// </summary>
        public static string TranslateGenre(string genre)
            => Genres.TryGetValue(genre, out var ro) ? ro : genre;

        /// <summary>
        /// Incearca sa traducă o eticheta. Returneaza eticheta originala daca nu e in dictionar.
        /// </summary>
        public static string TranslateTag(string tag)
            => Tags.TryGetValue(tag, out var ro) ? ro : tag;
    }
}
