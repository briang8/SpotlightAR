using System;
using NUnit.Framework;
using SpotlightAR.Audio;

namespace SpotlightAR.Tests
{
    /// <summary>
    /// Checks pitch detection against synthetic "voice-like" notes: a fundamental plus
    /// quieter harmonics (2x, 3x, 4x, 5x) and a little background hiss. Harmonics are what
    /// usually fool pitch detectors into octave mistakes, so a pure sine would be too easy.
    /// </summary>
    public sealed class PitchDetectorTests
    {
        const int SampleRate = 44100;
        const int WindowSize = 2048;               // same window the app uses
        const float MaxErrorCents = 5f;            // 100 cents = 1 semitone
        const float AppConfidence = 0.8f;          // MicInput rejects readings below this
        static readonly double[] Harmonics = { 1, 0.6, 0.4, 0.25, 0.15 };

        // Low male voice up to a high soprano note.
        [TestCase(82.41, TestName = "E2 (low male voice)")]
        [TestCase(110.00, TestName = "A2")]
        [TestCase(146.83, TestName = "D3")]
        [TestCase(220.00, TestName = "A3")]
        [TestCase(261.63, TestName = "C4 (middle C)")]
        [TestCase(329.63, TestName = "E4")]
        [TestCase(440.00, TestName = "A4")]
        [TestCase(523.25, TestName = "C5")]
        [TestCase(659.26, TestName = "E5")]
        [TestCase(783.99, TestName = "G5")]
        [TestCase(987.77, TestName = "B5 (high soprano)")]
        public void DetectsVoiceLikeNoteWithinFiveCents(double frequency)
        {
            var detector = new PitchDetector(WindowSize);
            var random = new Random(42);

            // Different starting phases, so the result can't depend on a lucky alignment.
            for (int trial = 0; trial < 10; trial++)
            {
                float[] samples = VoiceLikeNote(frequency, random.NextDouble() * Math.PI * 2, random);
                float hz = detector.Detect(samples, SampleRate, out float confidence);

                Assert.That(hz, Is.GreaterThan(0f), "no pitch found");
                Assert.That(confidence, Is.GreaterThanOrEqualTo(AppConfidence), "too unsure for the app to use");
                float error = Math.Abs(PitchDetector.Cents(hz, (float)frequency));
                Assert.That(error, Is.LessThanOrEqualTo(MaxErrorCents), $"heard {hz:0.00} Hz, {error:0.0} cents off");
            }
        }

        [Test]
        public void SilenceHasNoPitch()
        {
            var detector = new PitchDetector(WindowSize);
            Assert.That(detector.Detect(new float[WindowSize], SampleRate, out _), Is.EqualTo(0f));
        }

        [Test]
        public void NoiseIsNotMistakenForANote()
        {
            // Like an "sss" or room hiss: the app must not invent a note from it.
            var detector = new PitchDetector(WindowSize);
            var random = new Random(7);
            for (int trial = 0; trial < 50; trial++)
            {
                var noise = new float[WindowSize];
                for (int i = 0; i < noise.Length; i++) noise[i] = (float)(0.3 * (random.NextDouble() * 2 - 1));
                float hz = detector.Detect(noise, SampleRate, out float confidence);
                Assert.That(hz <= 0f || confidence < AppConfidence, $"noise read as {hz:0} Hz");
            }
        }

        [Test]
        public void CentsMeasuresSemitones()
        {
            Assert.That(PitchDetector.Cents(880f, 440f), Is.EqualTo(1200f).Within(0.01f));
            Assert.That(PitchDetector.Cents(466.16f, 440f), Is.EqualTo(100f).Within(0.1f));
        }

        static float[] VoiceLikeNote(double frequency, double phase, Random random)
        {
            var samples = new float[WindowSize];
            for (int i = 0; i < samples.Length; i++)
            {
                double t = i / (double)SampleRate;
                double value = 0;
                for (int h = 0; h < Harmonics.Length; h++)
                    value += Harmonics[h] * Math.Sin(2 * Math.PI * frequency * (h + 1) * t + phase * (h + 1));
                double hiss = 0.01 * (random.NextDouble() * 2 - 1);
                samples[i] = (float)(0.2 * value + hiss);
            }
            return samples;
        }
    }
}
