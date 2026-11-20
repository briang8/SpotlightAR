using System;

namespace SpotlightAR.Scoring
{
    /// <summary>The crowd's verdict on one show: stars, score and how hyped they got.</summary>
    [Serializable]
    public sealed class PerformanceResult
    {
        public string songId;
        public string songTitle;
        public int score;
        public int stars;
        public int peakHype;
        public float secondsAtMaxHype;
        /// <summary>Average hype over the show, 0-1 (x1 = 0, x3 = 1).</summary>
        public float hypeScore;
        /// <summary>Share of the show the player was singing, 0-1.</summary>
        public float voiceShare;

        /// <summary>How far into the song the player got, and the song's full length (seconds).</summary>
        public float secondsPlayed;
        public float songDuration;

        public int previousBest;
        public bool isNewBest;

        /// <summary>True when the player used "Finish here" well before the end.</summary>
        public bool FinishedEarly => songDuration > 0f && secondsPlayed < songDuration - 5f;

        public int GetStars() => stars;

        public bool IsNewBest() => isNewBest;

        /// <summary>How the crowd took it, in a sentence.</summary>
        public string CrowdReaction()
        {
            int pick = (score + (songTitle ?? string.Empty).Length) & 1;
            switch (stars)
            {
                case 5: return pick == 0 ? "The crowd lost it. They're chanting for an encore." : "Phones up, lights on. That was a headline set.";
                case 4: return pick == 0 ? "The crowd was with you the whole way." : "Big cheers. They'd come back for another one.";
                case 3: return pick == 0 ? "Solid set. The crowd was nodding along." : "Good vibes. A few more big moments and they'll go wild.";
                case 2: return pick == 0 ? "The crowd warmed up to you. Keep singing to win them over." : "A few cheers. Sing through more of the song to get them going.";
                default: return "The crowd's waiting for you. Sing out and watch them react!";
            }
        }
    }
}
