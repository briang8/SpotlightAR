using UnityEngine;

namespace SpotlightAR.Scoring
{
    /// <summary>
    /// Keeps the player's best score and stars for each song on the phone (PlayerPrefs),
    /// plus how many songs they have sung in total.
    /// </summary>
    public sealed class PlayerProfile
    {
        const string BestPrefix = "best_";
        const string StarsPrefix = "stars_";
        const string SongsSungKey = "songs_sung";

        public int SongsSung => PlayerPrefs.GetInt(SongsSungKey, 0);

        public int GetBest(string videoId) => PlayerPrefs.GetInt(BestPrefix + videoId, 0);

        public int GetBestStars(string videoId) => PlayerPrefs.GetInt(StarsPrefix + videoId, 0);

        /// <summary>Saves the result if it beats the previous best, and fills in previousBest / isNewBest.</summary>
        public void SaveResult(PerformanceResult result)
        {
            result.previousBest = GetBest(result.songId);
            result.isNewBest = result.score > result.previousBest;

            if (result.isNewBest) PlayerPrefs.SetInt(BestPrefix + result.songId, result.score);
            if (result.stars > GetBestStars(result.songId)) PlayerPrefs.SetInt(StarsPrefix + result.songId, result.stars);
            PlayerPrefs.SetInt(SongsSungKey, SongsSung + 1);
            PlayerPrefs.Save();
        }
    }
}
