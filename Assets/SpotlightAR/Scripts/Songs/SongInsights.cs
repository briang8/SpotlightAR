using System;

namespace SpotlightAR.Songs
{
    /// <summary>
    /// Musical facts about a song used for scoring: tempo (BPM) for rhythm, and key for
    /// checking whether sung notes belong to the song's scale. Either may be missing.
    /// </summary>
    [Serializable]
    public sealed class SongInsights
    {
        static readonly int[] MajorScale = { 0, 2, 4, 5, 7, 9, 11 };

        public string videoId;
        public float bpm;
        public string key;          // e.g. "F#", "D#m", "B♭ minor"; empty when unknown
        public string tempoSource;  // "Deezer" or "AcousticBrainz"
        public string keySource;

        public bool HasTempo => bpm >= 40f && bpm <= 220f;
        public bool HasKey => TryParseKey(key, out _, out _);
        public bool Found => HasTempo || HasKey;

        /// <summary>"84 BPM · F♯ major / D♯ minor", for the setup screen.</summary>
        public string Describe()
        {
            string tempo = HasTempo ? $"{bpm:0} BPM" : null;
            string keyText = HasKey ? DescribeKey() : null;
            return tempo != null && keyText != null ? $"{tempo} · {keyText}" : tempo ?? keyText ?? string.Empty;
        }

        /// <summary>
        /// The song's key with its relative key, e.g. "F♯ major / D♯ minor". Both use the same
        /// notes; sites like Tunebat may show either name.
        /// </summary>
        public string DescribeKey()
        {
            if (!TryParseKey(key, out int root, out bool minor)) return string.Empty;
            int relative = minor ? root + 3 : root - 3;
            return $"{KeyName(root, minor)} / {KeyName(relative, !minor)}";
        }

        /// <summary>True if a pitch class (0 = C ... 11 = B) belongs to the song's scale.</summary>
        public bool IsInKey(int pitchClass)
        {
            if (!TryParseKey(key, out int root, out bool minor)) return true;
            return IsInScale(pitchClass, MajorRoot(root, minor));
        }

        /// <summary>The scale of the song as the root of its major key (relative minors map to the same root).</summary>
        public int ScaleRoot => TryParseKey(key, out int root, out bool minor) ? MajorRoot(root, minor) : -1;

        /// <summary>True if the pitch class is in the major scale starting on majorRoot.</summary>
        public static bool IsInScale(int pitchClass, int majorRoot)
        {
            int interval = ((pitchClass - majorRoot) % 12 + 12) % 12;
            return Array.IndexOf(MajorScale, interval) >= 0;
        }

        /// <summary>A minor key uses the same notes as the major key three semitones above it.</summary>
        public static int MajorRoot(int root, bool minor) => Wrap(minor ? root + 3 : root);

        /// <summary>
        /// "D♯ minor", spelled the way musicians write it: keys whose scale uses sharps get sharp
        /// names (F♯ major / D♯ minor), flat keys get flat names (E♭ major / C minor).
        /// </summary>
        public static string KeyName(int root, bool minor)
        {
            bool flatKey = Array.IndexOf(FlatMajorRoots, MajorRoot(root, minor)) >= 0;
            string note = (flatKey ? FlatNames : SharpNames)[Wrap(root)];
            return $"{note} {(minor ? "minor" : "major")}";
        }

        static readonly int[] FlatMajorRoots = { 5, 10, 3, 8 }; // F, B♭, E♭, A♭ major (and D♭ is written C♯)
        static readonly string[] SharpNames = { "C", "C♯", "D", "D♯", "E", "F", "F♯", "G", "G♯", "A", "A♯", "B" };
        static readonly string[] FlatNames = { "C", "D♭", "D", "E♭", "E", "F", "G♭", "G", "A♭", "A", "B♭", "B" };

        static int Wrap(int pitchClass) => (pitchClass % 12 + 12) % 12;

        /// <summary>Parses keys like "C", "C#m", "C♯m", "Bb", "B♭ minor", "Ebm".</summary>
        public static bool TryParseKey(string text, out int rootPitchClass, out bool minor)
        {
            rootPitchClass = 0;
            minor = false;
            if (string.IsNullOrWhiteSpace(text)) return false;

            string value = text.Trim();
            int root = "C D EF G A B".IndexOf(char.ToUpperInvariant(value[0]));
            if (root < 0) return false;

            int index = 1;
            if (index < value.Length && (value[index] == '#' || value[index] == '♯')) { root++; index++; }
            else if (index < value.Length && (value[index] == 'b' || value[index] == '♭')) { root--; index++; }

            string rest = value.Substring(index).Trim().ToLowerInvariant();
            minor = rest.StartsWith("m") && !rest.StartsWith("maj");
            rootPitchClass = Wrap(root);
            return true;
        }
    }
}
