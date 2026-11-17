using System;
using UnityEngine;

namespace SpotlightAR.Scoring
{
    /// <summary>All scoring numbers in one place, editable on the Game Manager in the Inspector.</summary>
    [Serializable]
    public sealed class ScoringSettings
    {
        [Header("Voice")]
        [Tooltip("Loudness (0-1) above which the player counts as singing.")]
        [Range(0f, 1f)] public float voiceThreshold = 0.2f;
        [Tooltip("Loudness that earns full points. Anything louder earns no extra, so shouting doesn't win.")]
        [Range(0f, 1f)] public float fullCreditLoudness = 0.55f;
        public float pointsPerSecond = 10f;

        [Header("Crowd hype (x1 to x3)")]
        [Tooltip("How fast the hype meter fills while singing (per second, meter is 0-1).")]
        public float hypeRise = 0.06f;
        [Tooltip("How fast it drains after going quiet.")]
        public float hypeFall = 0.12f;
        [Tooltip("Seconds of silence allowed (breaths, short gaps) before hype starts to drop.")]
        public float silenceGrace = 1.5f;
        [Tooltip("Meter values where hype reaches x2 and x3.")]
        public Vector2 hypeLevels = new Vector2(0.4f, 0.8f);

        [Header("Held notes and pitch steadiness")]
        [Tooltip("A note counts as held after this many seconds on the same pitch.")]
        public float heldNoteSeconds = 0.5f;
        [Tooltip("Pitch can wander this far (cents) and still be the same note.")]
        public float sameNoteCents = 150f;
        [Tooltip("Average wobble (cents) on held notes that scores 0% steadiness.")]
        public float wobbleForZero = 60f;
        public float heldNoteBonusPerSecond = 6f;

        [Header("Rhythm (rap) and key (singing)")]
        [Tooltip("Bonus points for each 8-second stretch of rapping on the beat (scaled by how on-beat it was and by hype).")]
        public float rhythmBonusPerWindow = 40f;
        [Tooltip("Share of random notes that land in a key by chance; in-key scores are measured above this.")]
        [Range(0f, 1f)] public float inKeyByChance = 0.58f;
        [Tooltip("Same idea when the song's key is unknown and the player is judged on staying in their own best-fitting key (higher, since the best of 12 keys is picked).")]
        [Range(0f, 1f)] public float ownKeyByChance = 0.72f;
        [Tooltip("When a sung song's tempo is known, how much of the pitch skill score comes from timing on the beat.")]
        [Range(0f, 1f)] public float singingRhythmWeight = 0.25f;

        [Header("Milestones and stars")]
        [Tooltip("The crowd cheers every time the score passes a multiple of this.")]
        public int milestoneStep = 500;
        [Tooltip("Share of the song you need to sing for full coverage credit (songs have instrumental parts).")]
        [Range(0.1f, 1f)] public float fullCoverage = 0.7f;
        [Tooltip("Rating needed for 1 to 5 stars (rating is 0-1).")]
        public float[] starThresholds = { 0.15f, 0.35f, 0.55f, 0.72f, 0.86f };
    }
}
