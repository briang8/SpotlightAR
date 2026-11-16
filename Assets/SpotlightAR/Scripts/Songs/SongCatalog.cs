using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpotlightAR.Songs
{
    /// <summary>
    /// The app's song library: loads each genre playlist through <see cref="YouTubeService"/>,
    /// keeps the lists in memory, falls back to the on-device cache when offline,
    /// and searches across every loaded genre.
    /// </summary>
    public sealed class SongCatalog : MonoBehaviour
    {
        public enum Source { Live, Cache }

        [SerializeField] GenreCatalog genres;

        readonly Dictionary<string, List<Song>> loaded = new Dictionary<string, List<Song>>();
        readonly HashSet<string> loading = new HashSet<string>();
        readonly HashSet<string> fromCache = new HashSet<string>();
        readonly Dictionary<string, string> failures = new Dictionary<string, string>();
        YouTubeService youTube;
        SongCache cache;

        /// <summary>Raised when a genre's list is ready (genre, songs, where they came from).</summary>
        public event Action<string, IReadOnlyList<Song>, Source> GenreLoaded;

        /// <summary>Raised when a genre could not be loaded at all (genre, message).</summary>
        public event Action<string, string> GenreFailed;

        public IReadOnlyList<GenreCatalog.Genre> Genres => genres.Genres;

        public bool IsLoading => loading.Count > 0;

        /// <summary>The first load error, used when nothing at all could be loaded.</summary>
        public string FirstFailure => failures.Values.FirstOrDefault();

        public bool IsFromCache(string genre) => fromCache.Contains(genre);

        public bool TryGetFailure(string genre, out string message) => failures.TryGetValue(genre, out message);

        void Awake()
        {
            youTube = YouTubeService.FromResources();
            cache = new SongCache();
        }

        /// <summary>Starts loading every genre in the background.</summary>
        public void LoadAll()
        {
            foreach (GenreCatalog.Genre genre in genres.Genres) LoadGenre(genre.displayName);
        }

        public async void LoadGenre(string genre)
        {
            if (loaded.TryGetValue(genre, out List<Song> ready))
            {
                GenreLoaded?.Invoke(genre, ready, Source.Live);
                return;
            }
            if (failures.ContainsKey(genre) || !loading.Add(genre)) return;

            GenreCatalog.Genre entry = genres.Genres.FirstOrDefault(g => g.displayName == genre);
            try
            {
                List<Song> songs = await youTube.GetPlaylistItems(entry?.playlistId, genre, genres.SongsPerGenre);
                loaded[genre] = songs;
                cache.Store(genre, songs);
                loading.Remove(genre);
                GenreLoaded?.Invoke(genre, songs, Source.Live);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SongCatalog] {genre}: {e.Message}");
                if (cache.TryGet(genre, out List<Song> cached))
                {
                    loaded[genre] = cached;
                    fromCache.Add(genre);
                    loading.Remove(genre);
                    GenreLoaded?.Invoke(genre, cached, Source.Cache);
                }
                else
                {
                    failures[genre] = e.Message;
                    loading.Remove(genre);
                    GenreFailed?.Invoke(genre, e.Message);
                }
            }
            finally
            {
                loading.Remove(genre);
            }
        }

        public bool TryGetLoaded(string genre, out IReadOnlyList<Song> songs)
        {
            bool found = loaded.TryGetValue(genre, out List<Song> list);
            songs = list;
            return found;
        }

        /// <summary>
        /// "For you": a mix that interleaves the top songs of every loaded genre,
        /// so the default tab always shows a bit of everything.
        /// </summary>
        public List<Song> GetMix(int count)
        {
            var mix = new List<Song>();
            for (int rank = 0; mix.Count < count; rank++)
            {
                bool any = false;
                foreach (GenreCatalog.Genre genre in genres.Genres)
                {
                    if (!loaded.TryGetValue(genre.displayName, out List<Song> list) || rank >= list.Count) continue;
                    mix.Add(list[rank]);
                    any = true;
                    if (mix.Count == count) break;
                }
                if (!any) break;
            }
            return mix;
        }

        readonly Dictionary<string, List<Song>> onlineSearches = new Dictionary<string, List<Song>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Searches all of YouTube for karaoke versions (100 quota units per new query).
        /// Repeated queries are answered from memory for free.
        /// </summary>
        public async Awaitable<List<Song>> SearchOnline(string text)
        {
            string query = text?.Trim() ?? string.Empty;
            if (query.Length == 0) return new List<Song>();
            if (onlineSearches.TryGetValue(query, out List<Song> cached)) return cached;

            List<Song> results = await youTube.SearchKaraoke(query, 15);
            onlineSearches[query] = results;
            return results;
        }

        /// <summary>Searches titles and artists across every loaded genre (no extra API cost).</summary>
        public List<Song> Search(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<Song>();
            string query = text.Trim();
            return loaded.Values
                .SelectMany(list => list)
                .Where(song => Contains(song.title, query) || Contains(song.artist, query))
                .GroupBy(song => song.videoId)
                .Select(group => group.First())
                .ToList();
        }

        static bool Contains(string source, string query) =>
            !string.IsNullOrEmpty(source) && source.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
