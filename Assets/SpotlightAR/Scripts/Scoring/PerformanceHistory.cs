using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpotlightAR.Songs;
using UnityEngine;

namespace SpotlightAR.Scoring
{
    /// <summary>
    /// Every finished show, newest first, saved as JSON on the phone. The Library tab reads it
    /// for the player's stats and history, and the Stages tab for the best score on each stage.
    /// </summary>
    public sealed class PerformanceHistory
    {
        const int MaxShows = 100;

        [Serializable]
        public sealed class Show
        {
            public string videoId, title, artist, genre, coverUrl;
            public int durationSeconds;
            public string stage;
            public bool singMode;
            public int score, stars;
            public float secondsSung;
            public long playedAtTicks;

            public DateTime PlayedAt => new DateTime(playedAtTicks, DateTimeKind.Local);
            public Song ToSong() => new Song(videoId, title, artist, genre, coverUrl, durationSeconds);
        }

        [Serializable]
        sealed class SaveFile { public List<Show> shows = new List<Show>(); }

        readonly string path;
        SaveFile data;

        public PerformanceHistory(string folder = null)
        {
            path = Path.Combine(folder ?? Application.persistentDataPath, "history.json");
            Load();
        }

        public IReadOnlyList<Show> Shows => data.shows;
        public int ShowCount => data.shows.Count;
        public int BestScore => data.shows.Count == 0 ? 0 : data.shows.Max(s => s.score);

        /// <summary>Stars summed over each song's best, so singing the same song ten times doesn't inflate it.</summary>
        public int TotalStars => data.shows.GroupBy(s => s.videoId).Sum(g => g.Max(s => s.stars));

        public float MinutesSung => data.shows.Sum(s => s.secondsSung) / 60f;

        public int BestOnStage(string stage) =>
            data.shows.Where(s => s.stage == stage).Select(s => s.score).DefaultIfEmpty(0).Max();

        public int ShowsOnStage(string stage) => data.shows.Count(s => s.stage == stage);

        /// <summary>The player's best score per song, highest first.</summary>
        public IEnumerable<Show> PersonalBests(int count) =>
            data.shows.GroupBy(s => s.videoId).Select(g => g.OrderByDescending(s => s.score).First())
                .OrderByDescending(s => s.score).Take(count);

        public void Add(Song song, PerformanceResult result, string stage, bool singMode)
        {
            data.shows.Insert(0, new Show
            {
                videoId = song.videoId, title = song.title, artist = song.artist, genre = song.genre,
                coverUrl = song.coverUrl, durationSeconds = song.durationSeconds,
                stage = stage, singMode = singMode,
                score = result.score, stars = result.stars,
                secondsSung = result.voiceShare * result.secondsPlayed,
                playedAtTicks = DateTime.Now.Ticks
            });
            if (data.shows.Count > MaxShows) data.shows.RemoveRange(MaxShows, data.shows.Count - MaxShows);
            Save();
        }

        void Load()
        {
            try
            {
                data = File.Exists(path) ? JsonUtility.FromJson<SaveFile>(File.ReadAllText(path)) : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"History could not be read, starting fresh: {e.Message}");
                data = null;
            }
            data ??= new SaveFile();
            data.shows ??= new List<Show>();
        }

        void Save()
        {
            try { File.WriteAllText(path, JsonUtility.ToJson(data)); }
            catch (Exception e) { Debug.LogWarning($"History could not be saved: {e.Message}"); }
        }

        /// <summary>"Just now", "2h ago", "Yesterday", "Mon", "12 Nov".</summary>
        public static string When(DateTime time, DateTime now)
        {
            TimeSpan ago = now - time;
            if (ago.TotalMinutes < 2) return "Just now";
            if (ago.TotalHours < 1) return $"{(int)ago.TotalMinutes}m ago";
            if (time.Date == now.Date) return $"{(int)ago.TotalHours}h ago";
            if (time.Date == now.Date.AddDays(-1)) return "Yesterday";
            if (ago.TotalDays < 7) return time.ToString("ddd");
            return time.ToString("d MMM");
        }
    }
}
