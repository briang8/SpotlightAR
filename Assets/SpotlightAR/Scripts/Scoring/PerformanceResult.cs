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

        /// <summary>How far into the song the player got, and the song's full length (seconds).</summary>
        public float secondsPlayed;
        public float songDuration;

        public ScoreStyle style;

        /// <summary>How consistently syllables landed on the song's beat, 0-1, or -1 if the tempo is unknown.</summary>
        public float onBeat = -1f;

        /// <summary>Singing: share of steady notes in the song's key, 0-1, or -1 if the key is unknown.</summary>
        public float inKey = -1f;

        /// <summary>Singing: share of steady notes in the player's own best-fitting key, 0-1, or -1.</summary>
        public float inOwnKey = -1f;

        /// <summary>The key the player sang in ("C minor") and the song's key ("F♯ major / D♯ minor"), if known.</summary>
        public string sungKey;
        public string songKey;
        public KeyMatch keyMatch;

        /// <summary>Song tempo, or 0 when unknown.</summary>
        public float bpm;

        public float syllablesPerSecond;

        /// <summary>"84 BPM · F♯ major / D♯ minor" when the song was found online.</summary>
        public string songInfo;

        public bool HasOnBeat => onBeat >= 0f;
        public bool HasInKey => inKey >= 0f;
        public bool HasOwnKey => inOwnKey >= 0f;
        public bool HasSungKey => !string.IsNullOrEmpty(sungKey);
        public bool HasSongKey => !string.IsNullOrEmpty(songKey);

        /// <summary>True when the player used "Finish here" well before the end.</summary>
        public bool FinishedEarly => songDuration > 0f && secondsPlayed < songDuration - 5f;
        public int previousBest;
        public bool isNewBest;

        public int GetStars() => stars;

        public bool IsNewBest() => isNewBest;

        public bool HasSteadiness => steadiness >= 0f;

        /// <summary>One coaching tip aimed at the weakest part of the performance.</summary>
        public string GetTip()
        {
            if (coverage < 0.45f) return "Keep going through the quieter parts. Every gap lets the crowd's hype cool down.";
            if (style == ScoreStyle.Rap)
            {
                if (HasOnBeat && onBeat < 0.45f) return "Your flow drifted off the beat. Nod along to the drums and land your words on them.";
                if (HasOnBeat && onBeat >= 0.75f && stars < 5) return "Locked in on the beat! Keep that energy all the way through for more hype.";
            }
            else
            {
                if (keyMatch == KeyMatch.Different) return "You sang in a different key from the song. Hum along with the intro first to find the starting note.";
                if (HasInKey && inKey < 0.7f) return "Some notes slipped out of the song's key. Listen to the melody first, then match it.";
                if (HasSteadiness && steadiness < 0.5f) return "Your long notes wobbled. Take a deeper breath and hold each note on one pitch.";
                if (HasOnBeat && onBeat < 0.35f) return "Your timing drifted from the beat. Start each line with the drums.";
            }
            if (peakHype < 3) return "Stay steady for longer to push the crowd to x3 hype.";
            if (style == ScoreStyle.Singing && !HasSteadiness) return "Try holding a long note in the chorus. Steady notes earn bonus points.";
            return stars >= 5 ? "A five-star show. Try it on another stage!" : "So close! A little more consistency gets the next star.";
        }
    }
}
