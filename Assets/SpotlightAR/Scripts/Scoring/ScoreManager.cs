using System;
using SpotlightAR.Audio;
using UnityEngine;

namespace SpotlightAR.Scoring
{
    /// <summary>
    /// Turns the voice into a score. Rewards consistency rather than raw volume:
    /// singing through the song fills the crowd hype meter (x1 to x3), going quiet drains it,
    /// points are capped at a comfortable loudness, and held notes earn a bonus that
    /// depends on how steady their pitch is.
    /// </summary>
    public sealed class ScoreManager
    {
        readonly ScoringSettings settings;

        float points;
        float hypeMeter;
        float silence;
        float songTime;
        float singingTime;
        float hypeSeconds;
        float secondsAtMax;
        int nextMilestone;

        // Held-note tracking.
        float noteReference;
        float noteTime;
        float unvoiced;
        float wobbleSum;
        float wobbleTime;
        float longestNote;

        public ScoreManager(ScoringSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public int Score => Mathf.FloorToInt(points);
        public int Hype { get; private set; } = 1;
        public int PeakHype { get; private set; } = 1;
        public float HypeMeter => hypeMeter;

        /// <summary>Raised when hype changes level (1 to 3).</summary>
        public event Action<int> HypeChanged;

        /// <summary>Raised each time the score passes a milestone (500, 1000, ...).</summary>
        public event Action<int> MilestoneReached;

        public void Begin()
        {
            points = hypeMeter = silence = songTime = singingTime = hypeSeconds = secondsAtMax = 0f;
            noteReference = noteTime = unvoiced = wobbleSum = wobbleTime = longestNote = 0f;
            Hype = PeakHype = 1;
            nextMilestone = settings.milestoneStep;
        }

        /// <summary>Feed one frame of the performance (loudness 0-1, pitch in Hz or 0).</summary>
        public void AddFrame(float loudness, float pitchHz, float deltaTime)
        {
            if (deltaTime <= 0f) return;
            songTime += deltaTime;

            bool singing = loudness >= settings.voiceThreshold;
            UpdateHype(singing, deltaTime);

            if (singing)
            {
                singingTime += deltaTime;
                float credit = Mathf.InverseLerp(settings.voiceThreshold, settings.fullCreditLoudness, loudness);
                points += settings.pointsPerSecond * Mathf.Lerp(0.4f, 1f, credit) * Hype * deltaTime;
            }

            UpdateHeldNote(singing ? pitchHz : 0f, deltaTime);

            hypeSeconds += Hype * deltaTime;
            if (Hype == 3) secondsAtMax += deltaTime;

            while (settings.milestoneStep > 0 && Score >= nextMilestone)
            {
                MilestoneReached?.Invoke(nextMilestone);
                nextMilestone += settings.milestoneStep;
            }
        }

        void UpdateHype(bool singing, float deltaTime)
        {
            if (singing)
            {
                silence = 0f;
                hypeMeter += settings.hypeRise * deltaTime;
            }
            else
            {
                silence += deltaTime;
                if (silence > settings.silenceGrace) hypeMeter -= settings.hypeFall * deltaTime;
            }
            hypeMeter = Mathf.Clamp01(hypeMeter);

            // Small hysteresis so hype doesn't flicker between levels on a boundary.
            int level = Hype;
            if (hypeMeter >= settings.hypeLevels.y) level = 3;
            else if (hypeMeter >= settings.hypeLevels.x) level = level == 3 && hypeMeter > settings.hypeLevels.y - 0.05f ? 3 : 2;
            else level = level >= 2 && hypeMeter > settings.hypeLevels.x - 0.05f ? 2 : 1;

            if (level == Hype) return;
            Hype = level;
            PeakHype = Mathf.Max(PeakHype, level);
            HypeChanged?.Invoke(level);
        }

        void UpdateHeldNote(float pitchHz, float deltaTime)
        {
            if (pitchHz <= 0f)
            {
                unvoiced += deltaTime;
                if (unvoiced > 0.15f) EndNote();
                return;
            }
            unvoiced = 0f;

            if (noteReference <= 0f)
            {
                noteReference = pitchHz;
                noteTime = 0f;
                return;
            }

            float cents = PitchDetector.Cents(pitchHz, noteReference);
            if (Mathf.Abs(cents) > settings.sameNoteCents)
            {
                EndNote();
                noteReference = pitchHz;
                return;
            }

            noteTime += deltaTime;
            // Follow slow drift (vibrato centre) without hiding fast wobble.
            noteReference = Mathf.Lerp(noteReference, pitchHz, deltaTime * 2f);

            if (noteTime < settings.heldNoteSeconds) return;
            float wobble = Mathf.Abs(cents);
            wobbleSum += wobble * deltaTime;
            wobbleTime += deltaTime;

            float steadiness = 1f - Mathf.Clamp01(wobble / settings.wobbleForZero);
            points += settings.heldNoteBonusPerSecond * steadiness * Hype * deltaTime;
        }

        void EndNote()
        {
            longestNote = Mathf.Max(longestNote, noteTime);
            noteReference = 0f;
            noteTime = 0f;
        }

        public PerformanceResult BuildResult(string songId, string songTitle)
        {
            EndNote();
            float time = Mathf.Max(songTime, 0.001f);
            float coverage = singingTime / time;
            float averageHype = hypeSeconds / time;
            float steadiness = wobbleTime >= 1f ? 1f - Mathf.Clamp01(wobbleSum / wobbleTime / settings.wobbleForZero) : -1f;

            // Rating: mostly "did you sing it", then how hyped the crowd got, then pitch control.
            float coverageScore = Mathf.Clamp01(coverage / settings.fullCoverage);
            float hypeScore = Mathf.InverseLerp(1f, 3f, averageHype);
            float pitchScore = steadiness >= 0f ? steadiness : coverageScore * 0.6f;
            float rating = 0.5f * coverageScore + 0.3f * hypeScore + 0.2f * pitchScore;

            int stars = 0;
            for (int i = 0; i < settings.starThresholds.Length; i++) if (rating >= settings.starThresholds[i]) stars = i + 1;
            if (singingTime > 3f) stars = Mathf.Max(stars, 1);

            return new PerformanceResult
            {
                songId = songId,
                songTitle = songTitle,
                score = Score,
                stars = stars,
                peakHype = PeakHype,
                coverage = coverage,
                longestNote = longestNote,
                steadiness = steadiness,
                secondsAtMaxHype = secondsAtMax
            };
        }
    }
}
