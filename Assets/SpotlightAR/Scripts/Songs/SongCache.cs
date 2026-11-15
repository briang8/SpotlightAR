using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SpotlightAR.Songs
{
    /// <summary>
    /// Saves the last song lists to the phone so the home screen still works offline
    /// or when the API is unreachable. Only titles and ids are stored, never audio.
    /// </summary>
    public sealed class SongCache
    {
        [Serializable]
        sealed class GenreEntry
        {
            public string genre;
            public List<Song> songs = new List<Song>();
        }

        [Serializable]
        sealed class CacheFile
        {
            public string savedAtUtc;
            public List<GenreEntry> genres = new List<GenreEntry>();
        }

        readonly string path = Path.Combine(Application.persistentDataPath, "song_cache.json");
        CacheFile data;

        public DateTime? SavedAt => DateTime.TryParse(Load().savedAtUtc, out DateTime time) ? time : (DateTime?)null;

        public bool TryGet(string genre, out List<Song> songs)
        {
            GenreEntry entry = Load().genres.Find(g => g.genre == genre);
            songs = entry?.songs;
            return songs != null && songs.Count > 0;
        }

        public void Store(string genre, List<Song> songs)
        {
            CacheFile file = Load();
            GenreEntry entry = file.genres.Find(g => g.genre == genre);
            if (entry == null) file.genres.Add(entry = new GenreEntry { genre = genre });
            entry.songs = new List<Song>(songs);
            file.savedAtUtc = DateTime.UtcNow.ToString("o");

            try { File.WriteAllText(path, JsonUtility.ToJson(file)); }
            catch (IOException e) { Debug.LogWarning($"[SongCache] Could not save cache: {e.Message}"); }
        }

        CacheFile Load()
        {
            if (data != null) return data;
            try { data = File.Exists(path) ? JsonUtility.FromJson<CacheFile>(File.ReadAllText(path)) : null; }
            catch (Exception e) { Debug.LogWarning($"[SongCache] Ignoring unreadable cache: {e.Message}"); }
            return data ??= new CacheFile();
        }
    }
}
