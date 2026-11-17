using System;
using NUnit.Framework;
using SpotlightAR.Scoring;
using SpotlightAR.Songs;

namespace SpotlightAR.Tests
{
    /// <summary>Checks the key comparison and beat tracking that drive the singing and rap scores.</summary>
    public sealed class KeyAndRhythmTests
    {
        // C major scale, C4 to B4, in Hz.
        static readonly float[] CMajorScale = { 261.63f, 293.66f, 329.63f, 349.23f, 392.00f, 440.00f, 493.88f };

        [TestCase("F#", 6, false)]
        [TestCase("D#m", 3, true)]
        [TestCase("E♭ minor", 3, true)]
        [TestCase("Bb", 10, false)]
        [TestCase("C♯m", 1, true)]
        public void ParsesKeyNames(string text, int root, bool minor)
        {
            Assert.That(SongInsights.TryParseKey(text, out int parsedRoot, out bool parsedMinor), Is.True);
            Assert.That(parsedRoot, Is.EqualTo(root));
            Assert.That(parsedMinor, Is.EqualTo(minor));
        }

        [Test]
        public void RelativeKeysUseTheSameNotes()
        {
            // Tunebat lists Dear Mama as F♯ major; AcousticBrainz says D♯ minor. Same scale.
            var major = new SongInsights { key = "F#" };
            var minor = new SongInsights { key = "D#m" };
            Assert.That(minor.ScaleRoot, Is.EqualTo(major.ScaleRoot));
            Assert.That(major.DescribeKey(), Is.EqualTo("F♯ major / D♯ minor"));
        }

        [Test]
        public void SingingTheSongsScaleMatchesItsKey()
        {
            KeyTracker tracker = SingScale(new SongInsights { key = "Am" }); // A minor = C major's notes
            Assert.That(tracker.Match, Is.EqualTo(KeyMatch.Match));
            Assert.That(tracker.InSongKey, Is.EqualTo(1f).Within(0.01f));
            Assert.That(tracker.SungKeyName, Is.EqualTo("C major"));
        }

        [Test]
        public void SingingInAnotherKeyIsCaught()
        {
            KeyTracker tracker = SingScale(new SongInsights { key = "F#" }); // shares only 2 of 7 notes
            Assert.That(tracker.Match, Is.EqualTo(KeyMatch.Different));
            Assert.That(tracker.InSongKey, Is.LessThan(0.5f));
        }

        [Test]
        public void UnknownSongKeyStillNamesTheSungKey()
        {
            KeyTracker tracker = SingScale(new SongInsights());
            Assert.That(tracker.Match, Is.EqualTo(KeyMatch.Unknown));
            Assert.That(tracker.InSongKey, Is.EqualTo(-1f));
            Assert.That(tracker.InOwnKey, Is.EqualTo(1f).Within(0.01f));
            Assert.That(tracker.SungKeyName, Is.EqualTo("C major"));
        }

        [Test]
        public void TooFewNotesDoesNotNameAKey()
        {
            var tracker = new KeyTracker();
            tracker.Begin(new SongInsights());
            for (int frame = 0; frame < 40; frame++) tracker.AddFrame(261.63f, 0.05f); // 2 seconds
            Assert.That(tracker.SungKeyName, Is.Empty);
        }

        [Test]
        public void OnTheBeatScoresHigh()
        {
            var rhythm = new RhythmTracker();
            rhythm.Begin(90f);
            float beat = 60f / 90f;
            var random = new Random(3);
            // A syllable on every beat for 24 seconds, with a fixed 120 ms delay and ±15 ms human jitter.
            for (float t = 0.12f; t < 24f; t += beat) rhythm.AddOnset(t + (float)(random.NextDouble() * 0.03 - 0.015));
            rhythm.Finish();
            Assert.That(rhythm.OnBeat, Is.GreaterThan(0.8f));
        }

        [Test]
        public void RandomTimingScoresLow()
        {
            var rhythm = new RhythmTracker();
            rhythm.Begin(90f);
            var random = new Random(5);
            float t = 0f;
            while (t < 24f)
            {
                t += 0.15f + (float)random.NextDouble() * 0.5f;
                rhythm.AddOnset(t);
            }
            rhythm.Finish();
            Assert.That(rhythm.OnBeat, Is.LessThan(0.4f));
        }

        static KeyTracker SingScale(SongInsights song)
        {
            var tracker = new KeyTracker();
            tracker.Begin(song);
            foreach (float note in CMajorScale)
                for (int frame = 0; frame < 24; frame++) tracker.AddFrame(note, 0.05f); // 1.2 s per note
            return tracker;
        }
    }
}
