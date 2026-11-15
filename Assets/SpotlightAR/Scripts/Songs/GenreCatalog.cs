using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpotlightAR.Songs
{
    /// <summary>
    /// The curated genre playlists. Edit this asset in the Inspector to change which
    /// YouTube playlist feeds each genre; no code changes needed.
    /// </summary>
    [CreateAssetMenu(fileName = "GenrePlaylists", menuName = "Spotlight AR/Genre Playlists")]
    public sealed class GenreCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Genre
        {
            public string displayName;
            [Tooltip("The part after 'list=' in the playlist link.")]
            public string playlistId;
        }

        [SerializeField] List<Genre> genres = new List<Genre>();
        [Tooltip("Maximum songs read from each playlist.")]
        [SerializeField, Range(5, 50)] int songsPerGenre = 25;

        public IReadOnlyList<Genre> Genres => genres;
        public int SongsPerGenre => songsPerGenre;
    }
}
