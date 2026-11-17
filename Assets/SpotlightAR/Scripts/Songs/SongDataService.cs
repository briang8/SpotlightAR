using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace SpotlightAR.Songs
{
    /// <summary>
    /// Finds a song's tempo and key from free, open music databases (no account or key needed):
    /// <list type="bullet">
    /// <item>Tempo from Deezer's public API.</item>
    /// <item>Key (and backup tempo) from AcousticBrainz, found through MusicBrainz. AcousticBrainz
    /// stopped collecting in 2022 and its values are machine-detected, so a key is only trusted when
    /// most matching recordings agree on the notes. Relative keys such as F♯ major and D♯ minor
    /// use the same notes, so they count as agreeing.</item>
    /// </list>
    /// Answers are cached on the phone. Anything not found is left empty and scoring adapts.
    /// </summary>
    public sealed class SongDataService
    {
        const string UserAgent = "SpotlightAR/1.0 ( https://github.com/briang8/SpotlightAR )";
        const int Timeout = 10;
        const int MinMatchScore = 90;

        [Serializable] sealed class DeezerSearch { public DeezerTrack[] data; }
        [Serializable] sealed class DeezerTrack { public long id; public string title; public DeezerArtist artist; public float bpm; }
        [Serializable] sealed class DeezerArtist { public string name; }
        [Serializable] sealed class MbSearch { public MbRecording[] recordings; }
        [Serializable] sealed class MbRecording { public string id; public int score; }
        [Serializable] sealed class CacheFile { public List<SongInsights> songs = new List<SongInsights>(); }

        static readonly Regex BpmPattern = new Regex("\"bpm\"\\s*:\\s*([0-9.]+)");
        static readonly Regex KeyPattern = new Regex("\"key_key\"\\s*:\\s*\"([^\"]+)\"\\s*,\\s*\"key_scale\"\\s*:\\s*\"([^\"]+)\"");

        readonly string cachePath = Path.Combine(Application.persistentDataPath, "song_data.json");
        CacheFile cache;
        bool networkFailed;

        /// <summary>Tempo and key for a song, from the cache or online. Never throws.</summary>
        public async Awaitable<SongInsights> Lookup(Song song)
        {
            SongInsights cached = Load().songs.Find(s => s.videoId == song.videoId);
            if (cached != null) return cached;

            var insights = new SongInsights { videoId = song.videoId };
            networkFailed = false;
            try
            {
                float deezerBpm = await DeezerTempo(song);
                if (deezerBpm > 0f)
                {
                    insights.bpm = deezerBpm;
                    insights.tempoSource = "Deezer";
                }
                await AddAcousticBrainz(song, insights);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SongData] {song.title}: {e.Message}");
                return insights;
            }

            // A network failure should be retried next time; a clean "not found" is remembered.
            if (!networkFailed) Store(insights);
            Debug.Log($"[SongData] {song.title}: {(insights.Found ? insights.Describe() : "not found")}");
            return insights;
        }

        // ---------------- Deezer (tempo) ----------------

        async Awaitable<float> DeezerTempo(Song song)
        {
            string title = Clean(song.title);
            string query = string.IsNullOrEmpty(song.artist) ? title : $"{title} {Clean(song.artist)}";
            DeezerSearch search = await GetJson<DeezerSearch>($"https://api.deezer.com/search?limit=5&q={Uri.EscapeDataString(query)}");
            DeezerTrack hit = PickByArtist(search?.data, song);
            if (hit == null) return 0f;

            DeezerTrack track = await GetJson<DeezerTrack>($"https://api.deezer.com/track/{hit.id}");
            return track != null ? track.bpm : 0f; // Deezer reports 0 when it has no tempo
        }

        static DeezerTrack PickByArtist(DeezerTrack[] tracks, Song song)
        {
            if (tracks == null || tracks.Length == 0) return null;
            if (string.IsNullOrEmpty(song.artist)) return tracks[0];
            foreach (DeezerTrack track in tracks)
                if (SameArtist(track.artist?.name, song.artist)) return track;
            return null;
        }

        // ---------------- MusicBrainz + AcousticBrainz (key, backup tempo) ----------------

        async Awaitable AddAcousticBrainz(Song song, SongInsights insights)
        {
            string query = $"recording:\"{Clean(song.title)}\"";
            if (!string.IsNullOrEmpty(song.artist)) query += $" AND artist:\"{Clean(song.artist)}\"";
            MbSearch search = await GetJson<MbSearch>($"https://musicbrainz.org/ws/2/recording/?fmt=json&limit=6&query={Uri.EscapeDataString(query)}");
            if (search?.recordings == null) return;

            var ids = new List<string>();
            foreach (MbRecording recording in search.recordings)
                if (recording.score >= MinMatchScore && !string.IsNullOrEmpty(recording.id)) ids.Add(recording.id);
            if (ids.Count == 0) return;

            string url = $"https://acousticbrainz.org/api/v1/low-level?recording_ids={string.Join(";", ids)}&features=tonal.key_key;tonal.key_scale;rhythm.bpm";
            string json = await GetText(url);
            if (string.IsNullOrEmpty(json)) return;

            insights.key = AgreedKey(json);
            if (!string.IsNullOrEmpty(insights.key)) insights.keySource = "AcousticBrainz";

            if (!insights.HasTempo)
            {
                insights.bpm = AgreedTempo(json);
                if (insights.HasTempo) insights.tempoSource = "AcousticBrainz";
            }
        }

        /// <summary>
        /// The key most recordings agree on (by notes, so relative keys match), or null.
        /// Needs at least two agreeing recordings and a clear majority.
        /// </summary>
        static string AgreedKey(string json)
        {
            var votes = new int[12];
            var names = new Dictionary<string, int>();
            int total = 0;
            foreach (Match match in KeyPattern.Matches(json))
            {
                string name = match.Groups[1].Value + (match.Groups[2].Value == "minor" ? "m" : "");
                if (!SongInsights.TryParseKey(name, out int root, out bool minor)) continue;
                votes[SongInsights.MajorRoot(root, minor)]++;
                names[name] = names.TryGetValue(name, out int n) ? n + 1 : 1;
                total++;
            }

            int best = 0;
            for (int i = 1; i < 12; i++) if (votes[i] > votes[best]) best = i;
            if (votes[best] < 2 || votes[best] * 2 <= total) return null;

            // Name it the way most recordings did (e.g. "D#m" rather than "F#").
            string bestName = null;
            int bestCount = 0;
            foreach (KeyValuePair<string, int> pair in names)
            {
                SongInsights.TryParseKey(pair.Key, out int root, out bool minor);
                if (SongInsights.MajorRoot(root, minor) == best && pair.Value > bestCount)
                {
                    bestName = pair.Key;
                    bestCount = pair.Value;
                }
            }
            return bestName;
        }

        /// <summary>The tempo most recordings agree on, counting half/double tempo as the same.</summary>
        static float AgreedTempo(string json)
        {
            var values = new List<float>();
            foreach (Match match in BpmPattern.Matches(json))
                if (float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) && v > 0f) values.Add(v);

            float best = 0f;
            int bestSupport = 0;
            foreach (float candidate in values)
            {
                int support = 0;
                foreach (float other in values)
                    if (Near(other, candidate) || Near(other, candidate * 2f) || Near(other, candidate * 0.5f)) support++;
                if (support > bestSupport) { best = candidate; bestSupport = support; }
            }
            return best;
        }

        static bool Near(float a, float b) => Mathf.Abs(a - b) <= b * 0.04f;

        // ---------------- helpers ----------------

        async Awaitable<T> GetJson<T>(string url) where T : class
        {
            string text = await GetText(url);
            if (string.IsNullOrEmpty(text)) return null;
            try { return JsonUtility.FromJson<T>(text); }
            catch (ArgumentException) { return null; }
        }

        async Awaitable<string> GetText(string url)
        {
            using UnityWebRequest request = UnityWebRequest.Get(url);
            request.timeout = Timeout;
            request.SetRequestHeader("User-Agent", UserAgent);
            request.SetRequestHeader("Accept", "application/json");
            await request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success) return request.downloadHandler.text;
            if (request.result == UnityWebRequest.Result.ConnectionError) networkFailed = true;
            Debug.LogWarning($"[SongData] {new Uri(url).Host}: {request.error}");
            return null;
        }

        /// <summary>Removes quotes and bracketed extras like "(Official Video)" so searches match.</summary>
        static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string value = Regex.Replace(text, @"\s*[\(\[][^\)\]]*[\)\]]", string.Empty);
            return value.Replace("\"", string.Empty).Trim();
        }

        static bool SameArtist(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            return a.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0 || b.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        CacheFile Load()
        {
            if (cache != null) return cache;
            try { cache = File.Exists(cachePath) ? JsonUtility.FromJson<CacheFile>(File.ReadAllText(cachePath)) : null; }
            catch (Exception) { cache = null; }
            return cache ??= new CacheFile();
        }

        void Store(SongInsights insights)
        {
            CacheFile file = Load();
            file.songs.RemoveAll(s => s.videoId == insights.videoId);
            file.songs.Add(insights);
            try { File.WriteAllText(cachePath, JsonUtility.ToJson(file)); }
            catch (IOException e) { Debug.LogWarning($"[SongData] cache not saved: {e.Message}"); }
        }
    }
}
