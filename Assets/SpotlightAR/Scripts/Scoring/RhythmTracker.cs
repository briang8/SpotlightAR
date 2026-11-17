using System.Collections.Generic;
using UnityEngine;

namespace SpotlightAR.Scoring
{
    /// <summary>
    /// Judges flow: how consistently the player's syllables land on the song's beat grid.
    /// Each syllable's time is turned into a position within the beat (its phase). If the
    /// syllables keep landing at the same place in the beat, their phases cluster; this is
    /// measured with the vector strength statistic (0 = random timing, 1 = perfectly locked).
    /// It does not need to know where the beat "starts", only the tempo, so audio latency and
    /// slightly-off tempo data do not break it. Checked on half beats too, for fast flows.
    /// </summary>
    public sealed class RhythmTracker
    {
        const float WindowSeconds = 8f;
        const int MinOnsetsPerWindow = 6;

        readonly List<float> onsets = new List<float>();
        float beatPeriod;
        float windowStart;
        float weightedStrength;
        float weightTotal;
        int totalOnsets;

        public bool Active => beatPeriod > 0f;

        /// <summary>Average on-beat consistency so far, 0-1 (or -1 if not enough syllables yet).</summary>
        public float OnBeat => weightTotal > 0f ? weightedStrength / weightTotal : -1f;

        /// <summary>Syllables per second while performing.</summary>
        public int TotalOnsets => totalOnsets;

        /// <summary>Consistency of the most recent finished window (for live bonus points).</summary>
        public float LastWindowStrength { get; private set; } = -1f;

        public void Begin(float bpm)
        {
            beatPeriod = bpm > 0f ? 60f / bpm : 0f;
            onsets.Clear();
            windowStart = 0f;
            weightedStrength = weightTotal = 0f;
            totalOnsets = 0;
            LastWindowStrength = -1f;
        }

        /// <summary>Record a syllable at a song time (seconds).</summary>
        public void AddOnset(float songTime)
        {
            totalOnsets++;
            if (!Active) return;
            if (onsets.Count == 0) windowStart = songTime;
            onsets.Add(songTime);
            if (songTime - windowStart >= WindowSeconds) CloseWindow();
        }

        /// <summary>Score whatever is left at the end of the song.</summary>
        public void Finish()
        {
            if (Active && onsets.Count >= MinOnsetsPerWindow) CloseWindow();
        }

        void CloseWindow()
        {
            if (onsets.Count >= MinOnsetsPerWindow)
            {
                // Try the beat and the half beat; fast flows lock to half beats.
                float strength = Mathf.Max(VectorStrength(beatPeriod), VectorStrength(beatPeriod * 0.5f));
                // Random timing still gives about 1/sqrt(n); remove that so mumbling scores near 0.
                float chance = 1f / Mathf.Sqrt(onsets.Count);
                float normalised = Mathf.Clamp01((strength - chance) / (1f - chance));
                weightedStrength += normalised * onsets.Count;
                weightTotal += onsets.Count;
                LastWindowStrength = normalised;
            }
            onsets.Clear();
        }

        float VectorStrength(float period)
        {
            float x = 0f, y = 0f;
            foreach (float time in onsets)
            {
                float angle = 2f * Mathf.PI * (time / period);
                x += Mathf.Cos(angle);
                y += Mathf.Sin(angle);
            }
            return Mathf.Sqrt(x * x + y * y) / onsets.Count;
        }
    }
}
