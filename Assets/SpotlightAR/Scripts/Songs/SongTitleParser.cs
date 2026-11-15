using System.Text.RegularExpressions;

namespace SpotlightAR.Songs
{
    /// <summary>
    /// Turns karaoke upload titles like "Burna Boy - Last Last (Karaoke Version) [Lyrics]"
    /// into a clean title and artist for the UI.
    /// </summary>
    public static class SongTitleParser
    {
        static readonly Regex Noise = new Regex(
            @"[\(\[][^\)\]]*(karaoke|lyric|instrumental|official|audio|video|version|backing|sing along|hd|4k)[^\)\]]*[\)\]]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        static readonly Regex TrailingNoise = new Regex(
            @"\s*[-|:]?\s*\b(karaoke( version)?|lyrics?( video)?|instrumental|official( (music )?video)?)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        static readonly Regex KaraokeChannel = new Regex(@"karaoke|sing king|lyrics|backing track", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static (string title, string artist) Parse(string rawTitle, string channel)
        {
            string text = Noise.Replace(rawTitle ?? string.Empty, string.Empty);
            text = TrailingNoise.Replace(text, string.Empty);
            text = Regex.Replace(text, @"\s{2,}", " ").Trim(' ', '-', '|', ':');

            // "Artist - Title" is by far the most common karaoke naming pattern.
            int dash = IndexOfSeparator(text);
            if (dash > 0)
            {
                string artist = text.Substring(0, dash).Trim();
                string title = text.Substring(dash).TrimStart(' ', '-', '–', '—', '|').Trim();
                if (title.Length > 0 && artist.Length > 0) return (title, artist);
            }

            // No separator: fall back to the uploader unless it is a karaoke channel.
            string fallbackArtist = string.IsNullOrEmpty(channel) || KaraokeChannel.IsMatch(channel)
                ? string.Empty
                : channel.Replace(" - Topic", string.Empty).Trim();
            return (text, fallbackArtist);
        }

        static int IndexOfSeparator(string text)
        {
            foreach (string separator in new[] { " - ", " – ", " — ", " | " })
            {
                int index = text.IndexOf(separator, System.StringComparison.Ordinal);
                if (index > 0) return index;
            }
            return -1;
        }

        /// <summary>Parses ISO 8601 durations like PT1H3M42S into seconds.</summary>
        public static int ParseDuration(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return 0;
            Match match = Regex.Match(iso, @"PT(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)S)?");
            if (!match.Success) return 0;
            int Part(int group) => match.Groups[group].Success ? int.Parse(match.Groups[group].Value) : 0;
            return Part(1) * 3600 + Part(2) * 60 + Part(3);
        }
    }
}
