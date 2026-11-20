using System;
using SpotlightAR.Audio;
using UnityEngine;

namespace SpotlightAR.Scoring
{
    /// <summary>
    /// Keeps the show's score and the crowd's hype. It's meant to be fun, not a singing exam:
    /// singing fills the hype meter (x1 to x3) and earns points, going quiet lets the crowd
    /// cool down, and holding a note earns a little bonus. At the end, the stars are simply
    /// how much the crowd got into it.
    /// </summary>
    public sealed class ScoreManager
    {
        readonly ScoringSettings settings;

        float points;
        float hypeMeter;
        float silence;
        float playedTime;
        float singingTime;
        float hypeSeconds;
        float secondsAtMax;
        int nextMilestone;

        // Held notes, for the live note on screen and a small bonus.
        float noteReference;
        float noteTime;
        float unvoiced;

        public ScoreManager(ScoringSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public int Score => Mathf.FloorToInt(points);
        public int Hype { get; private set; } = 1;
        public int PeakHype { get; private set; } = 1;
        public float HypeMeter => hypeMeter;

        /// <summary>The note being held right now as a MIDI number (69 = A4), or -1 when no steady note is being sung.</summary>
        public int LiveNote { get; private set; } = -1;

        /// <summary>Raised when hype changes level (1 to 3).</summary>
        public event Action<int> HypeChanged;

        /// <summary>Raised each time the score passes a milestone (500, 1000, ...).</summary>
        public event Action<int> MilestoneReached;

        public void Begin()
        {
            points = hypeMeter = silence = playedTime = singingTime = hypeSeconds = secondsAtMax = 0f;
            noteReference = noteTime = unvoiced = 0f;
            Hype = PeakHype = 1;
            LiveNote = -1;
            nextMilestone = settings.milestoneStep;
        }

        /// <summary>Feed one frame: loudness 0-1 and pitch in Hz (0 = none).</summary>
        public void AddFrame(float loudness, float pitchHz, float deltaTime)
        {
            if (deltaTime <= 0f) return;
            playedTime += deltaTime;

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

        /// <summary>
        /// A held note is a pitch that stays within <see cref="ScoringSettings.sameNoteCents"/>.
        /// Only held notes are shown on screen, where the pitch detector is accurate; speech and
        /// rap don't sit on one pitch, so the note display just waits.
        /// </summary>
        void UpdateHeldNote(float pitchHz, float deltaTime)
        {
            if (pitchHz <= 0f)
            {
                unvoiced += deltaTime;
                if (unvoiced > 0.15f) EndNote();
                return;
            }
            unvoiced = 0f;

            if (noteReference <= 0f || Mathf.Abs(PitchDetector.Cents(pitchHz, noteReference)) > settings.sameNoteCents)
            {
                EndNote();
                noteReference = pitchHz;
                return;
            }

            noteTime += deltaTime;
            noteReference = Mathf.Lerp(noteReference, pitchHz, deltaTime * 2f); // follow slow drift (vibrato centre)
            LiveNote = noteTime >= settings.liveNoteSeconds ? Mathf.RoundToInt(PitchDetector.MidiNote(noteReference)) : -1;
            if (noteTime >= settings.heldNoteSeconds) points += settings.heldNoteBonusPerSecond * Hype * deltaTime;
        }

        void EndNote()
        {
            noteReference = 0f;
            noteTime = 0f;
            LiveNote = -1;
        }

        /// <summary>The crowd's verdict on the part that was performed.</summary>
        public PerformanceResult BuildResult(string songId, string songTitle, float secondsPlayed, float songDuration)
        {
            EndNote();
            float time = Mathf.Max(playedTime, 0.001f);
            float voiceShare = singingTime / time;
            float hypeScore = Mathf.InverseLerp(1f, 3f, hypeSeconds / time);

            // Stars = how much the crowd got into it: keep singing, keep the hype up.
            float rating = 0.6f * Mathf.Clamp01(voiceShare / settings.fullCoverage) + 0.4f * hypeScore;
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
                secondsAtMaxHype = secondsAtMax,
                hypeScore = hypeScore,
                voiceShare = voiceShare,
                secondsPlayed = secondsPlayed,
                songDuration = songDuration,
            };
        }
    }
}
