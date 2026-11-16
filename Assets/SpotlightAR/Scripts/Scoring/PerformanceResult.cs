using System;

namespace SpotlightAR.Scoring
{
    /// <summary>Everything the results screen shows about one finished song.</summary>
    [Serializable]
    public sealed class PerformanceResult
    {
        public string songId;
        public string songTitle;
        public int score;
        public int stars;
        public int peakHype;

        /// <summary>Share of the song the player sang, 0-1.</summary>
        public float coverage;

        /// <summary>Longest steady note, in seconds.</summary>
        public float longestNote;

        /// <summary>Pitch steadiness on held notes, 0-1, or -1 if no notes were held long enough to judge.</summary>
        public float steadiness = -1f;

        public float secondsAtMaxHype;
        public int previousBest;
        public bool isNewBest;

        public int GetStars() => stars;

        public bool IsNewBest() => isNewBest;

        public bool HasSteadiness => steadiness >= 0f;

        /// <summary>One coaching tip aimed at the weakest part of the performance.</summary>
        public string GetTip()
        {
            if (coverage < 0.45f) return "Keep singing through the quieter parts. Every gap lets the crowd's hype cool down.";
            if (HasSteadiness && steadiness < 0.5f) return "Your long notes wobbled. Take a deeper breath and hold each note on one pitch.";
            if (peakHype < 3) return "Stay steady for longer to push the crowd to x3 hype.";
            if (!HasSteadiness) return "Try holding a long note in the chorus. Steady notes earn bonus points.";
            return stars >= 5 ? "A five-star show. Try it on another stage!" : "So close! A little more consistency gets the next star.";
        }
    }
}
