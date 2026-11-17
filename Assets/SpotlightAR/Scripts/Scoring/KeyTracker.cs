using SpotlightAR.Songs;
using UnityEngine;

namespace SpotlightAR.Scoring
{
    /// <summary>Whether the key the player sang in uses the same notes as the song's key.</summary>
    public enum KeyMatch { Unknown, Match, Different }

    /// <summary>
    /// Pitch feedback without needing the song's melody. Every steady sung pitch is rounded to
    /// its note (C, C♯, D...) and timed. From that it finds:
    /// <list type="bullet">
    /// <item>the key the player actually sang in (the scale that fits their notes best), and</item>
    /// <item>how much of the singing was in the song's real key, when the song's key is known.</item>
    /// </list>
    /// Keys are compared by their notes, so relative keys (F♯ major / D♯ minor) count as a match.
    /// </summary>
    public sealed class KeyTracker
    {
        const float MinNoteSeconds = 0.08f;
        const float MinMeasuredSeconds = 1f;
        // Naming a key needs enough notes; a few seconds can fit several neighbouring keys equally well.
        const float MinKeySeconds = 6f;

        readonly float[] noteSeconds = new float[12];
        SongInsights insights;
        int currentNote = -1;
        float noteTime;
        float totalTime;

        public bool SongKeyKnown => insights != null && insights.HasKey;
        public bool HasData => totalTime >= MinMeasuredSeconds;
        bool HasKeyData => totalTime >= MinKeySeconds;

        public void Begin(SongInsights songInsights)
        {
            insights = songInsights;
            currentNote = -1;
            noteTime = totalTime = 0f;
            System.Array.Clear(noteSeconds, 0, noteSeconds.Length);
        }

        public void AddFrame(float pitchHz, float deltaTime)
        {
            if (pitchHz <= 0f)
            {
                currentNote = -1;
                return;
            }

            int note = Mathf.RoundToInt(12f * Mathf.Log(pitchHz / 440f, 2f) + 69f);
            if (note != currentNote)
            {
                currentNote = note;
                noteTime = 0f;
            }
            noteTime += deltaTime;
            if (noteTime < MinNoteSeconds) return; // ignore slides between notes

            totalTime += deltaTime;
            noteSeconds[((note % 12) + 12) % 12] += deltaTime;
        }

        /// <summary>Share of steady singing in the song's key, 0-1, or -1 if the key is unknown or nothing was sung.</summary>
        public float InSongKey => SongKeyKnown && HasData ? ShareInScale(insights.ScaleRoot) : -1f;

        /// <summary>Share of steady singing in the best-fitting key, 0-1 (how well the player stayed in one key), or -1.</summary>
        public float InOwnKey => HasKeyData ? ShareInScale(BestScaleRoot()) : -1f;

        /// <summary>The key the player sang in, e.g. "C minor", or empty.</summary>
        public string SungKeyName
        {
            get
            {
                if (!HasKeyData) return string.Empty;
                // If the song's key fits as well as anything, report that one (avoids naming a neighbour key).
                int majorRoot = Match == KeyMatch.Match ? insights.ScaleRoot : BestScaleRoot();
                int minorRoot = (majorRoot + 9) % 12;
                // Same notes either way; call it minor if the player leaned on the minor home note.
                bool minor = noteSeconds[minorRoot] > noteSeconds[majorRoot];
                return SongInsights.KeyName(minor ? minorRoot : majorRoot, minor);
            }
        }

        public KeyMatch Match
        {
            get
            {
                if (!SongKeyKnown || !HasKeyData) return KeyMatch.Unknown;
                // Neighbouring keys share 6 of 7 notes, so allow a near-tie with the best fit.
                return ShareInScale(insights.ScaleRoot) >= InOwnKey - 0.03f ? KeyMatch.Match : KeyMatch.Different;
            }
        }

        int BestScaleRoot()
        {
            int best = 0;
            float bestShare = -1f;
            for (int root = 0; root < 12; root++)
            {
                float share = ShareInScale(root);
                if (share > bestShare) { best = root; bestShare = share; }
            }
            return best;
        }

        float ShareInScale(int majorRoot)
        {
            if (totalTime <= 0f || majorRoot < 0) return 0f;
            float inScale = 0f;
            for (int pc = 0; pc < 12; pc++)
                if (SongInsights.IsInScale(pc, majorRoot)) inScale += noteSeconds[pc];
            return inScale / totalTime;
        }
    }
}
