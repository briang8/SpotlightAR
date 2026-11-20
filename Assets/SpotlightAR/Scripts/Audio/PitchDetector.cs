using System;

namespace SpotlightAR.Audio
{
    /// <summary>
    /// Finds the pitch of a voice in a block of mic samples using the YIN algorithm
    /// (de Cheveigné &amp; Kawahara, 2002), the same family of method used by tuner apps.
    /// Samples are halved in rate first, which keeps the cost low on mid-range phones
    /// while still covering the singing range (80 Hz to 1 kHz).
    /// </summary>
    public sealed class PitchDetector
    {
        const float MinFrequency = 80f;
        const float MaxFrequency = 1000f;
        const float Threshold = 0.15f;

        readonly float[] downsampled;
        readonly float[] difference;
        readonly int windowSize;

        public PitchDetector(int inputLength)
        {
            int length = inputLength / 2;
            downsampled = new float[length];
            windowSize = length / 2;
            difference = new float[windowSize];
        }

        /// <summary>
        /// Returns the fundamental frequency in Hz, or 0 when no clear pitch is found
        /// (silence, noise, or breathy sounds). Confidence is 0..1.
        /// </summary>
        public float Detect(float[] samples, int sampleRate, out float confidence)
        {
            confidence = 0f;
            int rate = sampleRate / 2;
            int count = Math.Min(downsampled.Length, samples.Length / 2);
            for (int i = 0; i < count; i++) downsampled[i] = (samples[2 * i] + samples[2 * i + 1]) * 0.5f;

            int minLag = Math.Max(2, (int)(rate / MaxFrequency));
            int maxLag = Math.Min(windowSize - 1, (int)(rate / MinFrequency));
            if (maxLag <= minLag) return 0f;

            // Step 1-2: difference function.
            for (int lag = 1; lag <= maxLag; lag++)
            {
                float sum = 0f;
                for (int i = 0; i < windowSize; i++)
                {
                    float delta = downsampled[i] - downsampled[i + lag];
                    sum += delta * delta;
                }
                difference[lag] = sum;
            }

            // Step 3: cumulative mean normalised difference.
            difference[0] = 1f;
            float running = 0f;
            for (int lag = 1; lag <= maxLag; lag++)
            {
                running += difference[lag];
                difference[lag] = running > 0f ? difference[lag] * lag / running : 1f;
            }

            // Step 4: first dip under the threshold, then walk to its local minimum.
            int best = -1;
            for (int lag = minLag; lag <= maxLag; lag++)
            {
                if (difference[lag] >= Threshold) continue;
                while (lag + 1 <= maxLag && difference[lag + 1] < difference[lag]) lag++;
                best = lag;
                break;
            }
            if (best < 0) return 0f;

            // Step 5: parabolic interpolation for sub-sample accuracy.
            float refined = best;
            if (best > 1 && best < maxLag)
            {
                float a = difference[best - 1], b = difference[best], c = difference[best + 1];
                float denominator = a + c - 2f * b;
                if (Math.Abs(denominator) > 1e-6f) refined = best + 0.5f * (a - c) / denominator;
            }

            confidence = 1f - difference[best];
            return rate / refined;
        }

        /// <summary>Distance between two frequencies in cents (100 cents = 1 semitone).</summary>
        public static float Cents(float frequency, float reference) =>
            1200f * (float)Math.Log(frequency / reference, 2.0);

        /// <summary>Frequency as a (fractional) MIDI note number: 69 = A4 (440 Hz), 60 = middle C.</summary>
        public static float MidiNote(float frequency) => 69f + 12f * (float)Math.Log(frequency / 440f, 2.0);

        static readonly string[] NoteNames = { "C", "C♯", "D", "D♯", "E", "F", "F♯", "G", "G♯", "A", "A♯", "B" };

        /// <summary>"A4", "C♯3"... for a MIDI note number.</summary>
        public static string NoteName(int midi) => midi < 0 ? string.Empty : NoteNames[midi % 12] + (midi / 12 - 1);
    }
}
