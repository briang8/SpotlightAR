using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace SpotlightAR.Songs
{
    /// <summary>
    /// Talks to the YouTube Data API v3. Reads a playlist (1 quota unit per 50 items),
    /// then looks up durations and embeddability for those videos (1 unit per 50 ids),
    /// and drops anything that cannot play inside the app.
    /// </summary>
    public sealed class YouTubeService
    {
        const string BaseUrl = "https://www.googleapis.com/youtube/v3/";
        const string KeyResource = "Secrets/youtube_api_key";
        const int DailyQuota = 10000;

        readonly string apiKey;

        public YouTubeService(string apiKey)
        {
            this.apiKey = apiKey?.Trim();
        }

        /// <summary>Loads the key from the git-ignored Resources/Secrets file.</summary>
        public static YouTubeService FromResources()
        {
            TextAsset key = Resources.Load<TextAsset>(KeyResource);
            return new YouTubeService(key != null ? key.text : null);
        }

        public bool HasKey => !string.IsNullOrEmpty(apiKey) && apiKey.StartsWith("AIza", StringComparison.Ordinal);

        /// <summary>Quota units used today (tracked locally; the free limit is 10,000 per day).</summary>
        public int UnitsUsedToday
        {
            get => PlayerPrefs.GetString("yt_quota_day") == Today ? PlayerPrefs.GetInt("yt_quota_used") : 0;
            private set
            {
                PlayerPrefs.SetString("yt_quota_day", Today);
                PlayerPrefs.SetInt("yt_quota_used", value);
            }
        }

        public int DailyQuotaLimit => DailyQuota;

        static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd");

        public async Awaitable<List<Song>> GetPlaylistItems(string playlistId, string genre, int maxSongs)
        {
            if (!HasKey) throw new InvalidOperationException("YouTube API key missing. Paste it into Resources/Secrets/youtube_api_key.txt.");
            if (string.IsNullOrWhiteSpace(playlistId)) throw new InvalidOperationException($"No playlist set for {genre}.");

            var songs = new List<Song>();
            string pageToken = null;
            do
            {
                string url = $"{BaseUrl}playlistItems?part=snippet&maxResults=50&playlistId={Uri.EscapeDataString(playlistId)}&key={apiKey}";
                if (pageToken != null) url += $"&pageToken={pageToken}";

                var page = await GetJson<PlaylistItemsResponse>(url);
                foreach (PlaylistItem item in page.items ?? Array.Empty<PlaylistItem>())
                {
                    PlaylistSnippet snippet = item.snippet;
                    string id = snippet?.resourceId?.videoId;
                    if (string.IsNullOrEmpty(id) || snippet.title == "Deleted video" || snippet.title == "Private video") continue;

                    var (title, artist) = SongTitleParser.Parse(snippet.title, snippet.videoOwnerChannelTitle);
                    songs.Add(new Song(id, title, artist, genre, GetThumbnail(id), 0));
                }
                pageToken = page.nextPageToken;
            } while (pageToken != null && songs.Count < maxSongs);

            if (songs.Count > maxSongs) songs.RemoveRange(maxSongs, songs.Count - maxSongs);
            await AddDurationsAndFilter(songs);
            return songs;
        }

        /// <summary>16:9 thumbnail without letterbox bars (320x180).</summary>
        public string GetThumbnail(string videoId) => $"https://i.ytimg.com/vi/{videoId}/mqdefault.jpg";

        async Awaitable AddDurationsAndFilter(List<Song> songs)
        {
            var details = new Dictionary<string, VideoItem>();
            for (int i = 0; i < songs.Count; i += 50)
            {
                string ids = string.Join(",", songs.Skip(i).Take(50).Select(s => s.videoId));
                var response = await GetJson<VideosResponse>($"{BaseUrl}videos?part=contentDetails,status&id={ids}&key={apiKey}");
                foreach (VideoItem video in response.items ?? Array.Empty<VideoItem>()) details[video.id] = video;
            }

            songs.RemoveAll(song =>
                !details.TryGetValue(song.videoId, out VideoItem video) ||
                video.status == null || !video.status.embeddable || video.status.privacyStatus == "private");

            foreach (Song song in songs) song.durationSeconds = SongTitleParser.ParseDuration(details[song.videoId].contentDetails?.duration);
        }

        async Awaitable<T> GetJson<T>(string url)
        {
            using UnityWebRequest request = UnityWebRequest.Get(url);
            request.timeout = 15;
            await request.SendWebRequest();
            UnitsUsedToday += 1;

            if (request.result != UnityWebRequest.Result.Success)
            {
                string reason = request.error;
                var apiError = TryParse<ApiErrorResponse>(request.downloadHandler?.text);
                if (apiError?.error != null && !string.IsNullOrEmpty(apiError.error.message)) reason = apiError.error.message;
                throw new Exception($"YouTube request failed ({request.responseCode}): {reason}");
            }

            return JsonUtility.FromJson<T>(request.downloadHandler.text);
        }

        static T TryParse<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<T>(json); }
            catch (ArgumentException) { return null; }
        }
    }
}
