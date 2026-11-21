using System;

namespace SpotlightAR.Audio
{
    /// <summary>
    /// The crowd's energy in party mode (no headphones). The song plays through the speaker
    /// right next to the mic and the phone boosts the mic automatically, so the raw level sits
    /// near the top whatever the volume. Instead of the level itself, this measures how much
    /// louder the room is than it was a few seconds ago: a chorus kicking in, a beat drop or
    /// someone belting over the song spikes it, and a sound that stays the same settles back
    /// down, like a real crowd. Quiet parts drop it quickly so the next build-up counts again.
    /// Pure logic with no Unity dependencies, so it can be tested on its own.
    /// </summary>
    public sealed class PartyEnergy
    {
        const float LevelAttack = 12f;   // per second: how fast the level follows a louder sound
        const float LevelRelease = 6f;   // ...and a quieter one
        const float EnergyAttack = 10f;
        const float EnergyRelease = 2.5f;

        readonly float settleRate;
        readonly float dropRate;
        readonly float startDb;
        readonly float fullDb;
        readonly float silenceDb;

        float level;
        float baseline;
        bool started;

        /// <param name="settleSeconds">Roughly how long a steady sound keeps the crowd excited.</param>
        /// <param name="dropSeconds">How quickly the "normal" level follows the room down in quiet parts.</param>
        /// <param name="startDb">How much louder than normal (dB) before the crowd reacts at all.</param>
        /// <param name="fullDb">How much louder than normal (dB) for full energy.</param>
        /// <param name="silenceDb">Below this the room counts as silent.</param>
        public PartyEnergy(float settleSeconds = 3f, float dropSeconds = 0.6f, float startDb = 1.5f, float fullDb = 9f, float silenceDb = -55f)
        {
            settleRate = 1f / Math.Max(0.01f, settleSeconds);
            dropRate = 1f / Math.Max(0.01f, dropSeconds);
            this.startDb = startDb;
            this.fullDb = Math.Max(startDb + 0.1f, fullDb);
            this.silenceDb = silenceDb;
        }

        /// <summary>Current energy, 0 (calm) to 1 (going wild).</summary>
        public float Energy { get; private set; }

        public void Reset()
        {
            started = false;
            Energy = 0f;
        }

        /// <summary>Takes the mic level in decibels and returns the crowd's energy (0 to 1).</summary>
        public float Process(float decibels, float deltaTime)
        {
            if (!started)
            {
                level = baseline = decibels;
                started = true;
            }

            level += (decibels - level) * Smooth(decibels > level ? LevelAttack : LevelRelease, deltaTime);
            // "Normal" climbs slowly toward a louder room (so steady sound settles) and drops quickly.
            baseline += (level - baseline) * Smooth(level > baseline ? settleRate : dropRate, deltaTime);

            float louder = level - baseline;
            float target = level < silenceDb ? 0f : Clamp01((louder - startDb) / (fullDb - startDb));
            Energy += (target - Energy) * Smooth(target > Energy ? EnergyAttack : EnergyRelease, deltaTime);
            return Energy;
        }

        static float Smooth(float rate, float deltaTime) => 1f - (float)Math.Exp(-rate * deltaTime);

        static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
