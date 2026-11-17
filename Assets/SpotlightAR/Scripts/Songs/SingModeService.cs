using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace SpotlightAR.Songs
{
    /// <summary>
    /// Sing mode, like the sing-along mode in music apps: the original song plays quietly as a
    /// guide while time-synced lyrics show on screen.
    /// <list type="bullet">
    /// <item>Lyrics come from LRCLIB (https://lrclib.net), a free open lyrics database (no key).</item>
    /// <item>The original recording is found on YouTube only when the player picks Sing mode,
    /// because a search costs 100 quota units. Both answers are cached on the phone.</item>
    /// </list>
    /// </summary>
    public sealed class SingModeService
    {
        const string UserAgent = "SpotlightAR/1.0 ( https://github.com/briang8/SpotlightAR )";

        [Serializable] sealed class LrclibTrack { public string trackName; public string artistName; public float duration; public bool instrumental; public string syncedLyrics; }
        [Serializable] sealed class LrclibList { public LrclibTrack[] items; }

        [Serializable]
        sealed class Entry
        {
            public string videoId;
            public bool lyricsChecked;
            public string lyrics;          // LRC text, empty when none was found
            public float lyricsDuration;
            public string originalVideoId; // empty until found
            public int originalDuration;
        }

        [Serializable] sealed class CacheFile { public List<Entry> songs = new List<Entry>(); }

        readonly Func<Song, float, Awaitable<Song>> findOriginal;
        readonly string cachePath = Path.Combine(Application.persistentDataPath, "sing_mode.json");
        CacheFile cache;

        /// <param name="findOriginal">Searches YouTube for the original recording (see SongCatalog.FindOriginal).</param>
        public SingModeService(Func<Song, float, Awaitable<Song>> findOriginal)
        {
            this.findOriginal = findOriginal;
        }

        /// <summary>Synced lyrics for a song, or null if LRCLIB has none. Never throws.</summary>
        public async Awaitable<SyncedLyrics> GetLyrics(Song song)
        {
            Entry entry = Find(song);
            if (entry != null && entry.lyricsChecked)
                return string.IsNullOrEmpty(entry.lyrics) ? null : SyncedLyrics.Parse(entry.lyrics);

            LrclibTrack track = null;
            bool networkOk = true;
            try
            {
                string title = Uri.EscapeDataString(song.title ?? string.Empty);
                string artist = Uri.EscapeDataString(song.artist ?? string.Empty);
                if (!string.IsNullOrEmpty(song.artist))
                {
                    (string text, bool ok) = await Get($"https://lrclib.net/api/get?track_name={title}&artist_name={artist}");
                    networkOk &= ok;
                    track = Parse<LrclibTrack>(text);
                }
                if (track == null || string.IsNullOrEmpty(track.syncedLyrics))
                {
                    string query = string.IsNullOrEmpty(song.artist) ? $"track_name={title}" : $"track_name={title}&artist_name={artist}";
                    (string text, bool ok) = await Get($"https://lrclib.net/api/search?{query}");
                    networkOk &= ok;
                    track = PickSynced(Parse<LrclibList>(string.IsNullOrEmpty(text) ? null : "{\"items\":" + text + "}")?.items);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SingMode] lyrics for {song.title}: {e.Message}");
                return null;
            }

            bool found = track != null && !track.instrumental && !string.IsNullOrEmpty(track.syncedLyrics);
            if (networkOk || found)
            {
                entry = GetOrAdd(song);
                entry.lyricsChecked = true;
                entry.lyrics = found ? track.syncedLyrics : string.Empty;
                entry.lyricsDuration = found ? track.duration : 0f;
                Save();
            }
            Debug.Log($"[SingMode] {song.title}: {(found ? "synced lyrics found" : "no synced lyrics")}");
            return found ? SyncedLyrics.Parse(track.syncedLyrics) : null;
        }

        /// <summary>
        /// The original recording to play under the lyrics, or null if none matches the lyrics'
        /// timing. Throws when YouTube can't be searched (quota, no connection), so the caller can say why.
        /// </summary>
        public async Awaitable<Song> GetOriginal(Song song)
        {
            Entry entry = Find(song);
            if (entry != null && !string.IsNullOrEmpty(entry.originalVideoId))
                return new Song(entry.originalVideoId, song.title, song.artist, song.genre, song.coverUrl, entry.originalDuration);

            Song original = await findOriginal(song, entry != null ? entry.lyricsDuration : 0f);
            if (original == null) return null;

            entry = GetOrAdd(song);
            entry.originalVideoId = original.videoId;
            entry.originalDuration = original.durationSeconds;
            Save();
            return original;
        }

        static LrclibTrack PickSynced(LrclibTrack[] tracks)
        {
            if (tracks == null) return null;
            foreach (LrclibTrack track in tracks)
                if (!track.instrumental && !string.IsNullOrEmpty(track.syncedLyrics)) return track;
            return null;
        }

        static T Parse<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<T>(json); }
            catch (ArgumentException) { return null; }
        }

        static async Awaitable<(string text, bool networkOk)> Get(string url)
        {
            using UnityWebRequest request = UnityWebRequest.Get(url);
            request.timeout = 10;
            request.SetRequestHeader("User-Agent", UserAgent);
            await request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success) return (request.downloadHandler.text, true);
            // 404 means "no lyrics for this song": a real answer, worth remembering.
            bool networkOk = request.result == UnityWebRequest.Result.ProtocolError;
            return (null, networkOk);
        }

        Entry Find(Song song) => Load().songs.Find(e => e.videoId == song.videoId);

        Entry GetOrAdd(Song song)
        {
            Entry entry = Find(song);
            if (entry != null) return entry;
            entry = new Entry { videoId = song.videoId };
            Load().songs.Add(entry);
            return entry;
        }

        CacheFile Load()
        {
            if (cache != null) return cache;
            try { cache = File.Exists(cachePath) ? JsonUtility.FromJson<CacheFile>(File.ReadAllText(cachePath)) : null; }
            catch (Exception) { cache = null; }
            return cache ??= new CacheFile();
        }

        void Save()
        {
            try { File.WriteAllText(cachePath, JsonUtility.ToJson(Load())); }
            catch (IOException e) { Debug.LogWarning($"[SingMode] cache not saved: {e.Message}"); }
        }
    }
}
