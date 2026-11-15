using System;

namespace SpotlightAR.Songs
{
    /// <summary>One karaoke track from a curated YouTube playlist.</summary>
    [Serializable]
    public sealed class Song
    {
        public string videoId;
        public string title;
        public string artist;
        public string genre;
        public string coverUrl;
        public int durationSeconds;

        public Song() { }

        public Song(string videoId, string title, string artist, string genre, string coverUrl, int durationSeconds)
        {
            this.videoId = videoId;
            this.title = title;
            this.artist = artist;
            this.genre = genre;
            this.coverUrl = coverUrl;
            this.durationSeconds = durationSeconds;
        }

        /// <summary>"Artist · Genre", as shown under the title in lists.</summary>
        public string GetLabel() => string.IsNullOrEmpty(artist) ? genre : $"{artist} · {genre}";

        public string GetCoverUrl() => coverUrl;

        public string FormattedDuration => TimeFormat.MinutesSeconds(durationSeconds);
    }

    public static class TimeFormat
    {
        public static string MinutesSeconds(float seconds)
        {
            int total = Math.Max(0, (int)seconds);
            return $"{total / 60}:{total % 60:00}";
        }
    }
}
