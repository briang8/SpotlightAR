using System;
using SpotlightAR.Audio;
using SpotlightAR.Songs;
using UnityEngine;

namespace SpotlightAR.Scoring
{
    /// <summary>Rap is judged on flow (staying on the beat); sung songs on held notes and key.</summary>
    public enum ScoreStyle { Singing, Rap }

    /// <summary>
    /// Turns the voice into a score. Rewards consistency rather than raw volume:
    /// singing through the song fills the crowd hype meter (x1 to x3), going quiet drains it,
    /// points are capped at a comfortable loudness, and held notes earn a bonus that
    /// depends on how steady their pitch is.
    /// </summary>
    public sealed class ScoreManager
    {
        readonly ScoringSettings settings;
        readonly RhythmTracker rhythm = new RhythmTracker();
        readonly KeyTracker keys = new KeyTracker();
        ScoreStyle style;
        SongInsights insights;

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

        public ScoreStyle Style => style;

        /// <summary>Start a new performance. Insights may be null when tempo/key are unknown.</summary>
        public void Begin(SongInsights songInsights, ScoreStyle scoreStyle)
        {
            points = hypeMeter = silence = songTime = singingTime = hypeSeconds = secondsAtMax = 0f;
            noteReference = noteTime = unvoiced = wobbleSum = wobbleTime = longestNote = 0f;
            Hype = PeakHype = 1;
            nextMilestone = settings.milestoneStep;

            insights = songInsights;
            style = scoreStyle;
            rhythm.Begin(insights != null && insights.HasTempo ? insights.bpm : 0f);
            keys.Begin(insights);
        }

        /// <summary>A syllable started at this song time. Rap earns points for staying on the beat.</summary>
        public void AddOnset(float songTimeSeconds)
        {
            float before = rhythm.LastWindowStrength;
            rhythm.AddOnset(songTimeSeconds);
            float after = rhythm.LastWindowStrength;
            if (style == ScoreStyle.Rap && after >= 0f && !Mathf.Approximately(before, after))
                points += settings.rhythmBonusPerWindow * after * Hype;
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
            keys.AddFrame(singing ? pitchHz : 0f, deltaTime);

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
            if (style == ScoreStyle.Singing) points += settings.heldNoteBonusPerSecond * steadiness * Hype * deltaTime;
        }

        void EndNote()
        {
            longestNote = Mathf.Max(longestNote, noteTime);
            noteReference = 0f;
            noteTime = 0f;
        }

        /// <summary>
        /// Builds the result for the part that was performed. Coverage and hype are measured
        /// over the time actually played, so finishing after one strong verse still rates well.
        /// </summary>
        public PerformanceResult BuildResult(string songId, string songTitle, float secondsPlayed, float songDuration)
        {
            EndNote();
            rhythm.Finish();
            float time = Mathf.Max(songTime, 0.001f);
            float coverage = singingTime / time;
            float averageHype = hypeSeconds / time;
            float steadiness = wobbleTime >= 1f ? 1f - Mathf.Clamp01(wobbleSum / wobbleTime / settings.wobbleForZero) : -1f;
            float onBeat = rhythm.OnBeat;
            float inKey = keys.InSongKey;
            float inOwnKey = keys.InOwnKey;

            // Rating: mostly "did you perform it", then how hyped the crowd got, then the
            // skill part: flow for rap; pitch (key + steadiness) and timing for singing.
            float coverageScore = Mathf.Clamp01(coverage / settings.fullCoverage);
            float hypeScore = Mathf.InverseLerp(1f, 3f, averageHype);
            float skillScore = SkillScore(steadiness, onBeat, inKey, inOwnKey, coverageScore);
            float rating = 0.45f * coverageScore + 0.25f * hypeScore + 0.3f * skillScore;

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
                secondsAtMaxHype = secondsAtMax,
                secondsPlayed = secondsPlayed,
                songDuration = songDuration,
                style = style,
                onBeat = onBeat,
                inKey = inKey,
                inOwnKey = inOwnKey,
                sungKey = keys.SungKeyName,
                songKey = insights != null ? insights.DescribeKey() : string.Empty,
                keyMatch = keys.Match,
                bpm = insights != null && insights.HasTempo ? insights.bpm : 0f,
                syllablesPerSecond = singingTime > 1f ? rhythm.TotalOnsets / singingTime : 0f,
                songInfo = insights != null ? insights.Describe() : string.Empty
            };
        }

        float SkillScore(float steadiness, float onBeat, float inKey, float inOwnKey, float coverageScore)
        {
            float fallback = coverageScore * 0.6f; // no data to judge skill: don't punish, don't reward
            if (style == ScoreStyle.Rap) return onBeat >= 0f ? onBeat : fallback;

            // Random notes land in a key by chance (about 58%), so only credit what's above that.
            // With the song's key unknown, judge staying in the player's own key, on a stricter scale.
            float keyScore = inKey >= 0f ? AboveChance(inKey, settings.inKeyByChance)
                           : inOwnKey >= 0f ? AboveChance(inOwnKey, settings.ownKeyByChance) : -1f;

            float pitch;
            if (keyScore >= 0f && steadiness >= 0f) pitch = (keyScore + steadiness) * 0.5f;
            else if (keyScore >= 0f) pitch = keyScore;
            else pitch = steadiness >= 0f ? steadiness : fallback;

            // Known tempo: being off the beat costs a share of the skill score.
            return onBeat >= 0f ? Mathf.Lerp(pitch, onBeat, settings.singingRhythmWeight) : pitch;
        }

        static float AboveChance(float share, float chance) => Mathf.Clamp01((share - chance) / (0.95f - chance));
    }
}
