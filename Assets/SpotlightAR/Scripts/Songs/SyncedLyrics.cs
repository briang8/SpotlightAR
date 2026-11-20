using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SpotlightAR.Songs
{
    /// <summary>
    /// Time-synced lyrics parsed from the LRC format ("[01:23.45] a line of lyrics").
    /// Given the song's playback time, it returns the current line, the lines around it
    /// and how far through the current line the singer should be.
    /// </summary>
    public sealed class SyncedLyrics
    {
        static readonly Regex LinePattern = new Regex(@"^\s*\[(\d+):(\d+(?:\.\d+)?)\]\s*(.*)$");

        readonly List<float> times = new List<float>();
        readonly List<string> lines = new List<string>();

        /// <summary>Seconds added to every timestamp (positive = lyrics later), for videos with a short intro.</summary>
        public float Offset { get; set; }

        public int Count => lines.Count;
        public bool IsEmpty => lines.Count == 0;

        public static SyncedLyrics Parse(string lrc)
        {
            var lyrics = new SyncedLyrics();
            if (string.IsNullOrEmpty(lrc)) return lyrics;

            foreach (string raw in lrc.Split('\n'))
            {
                Match match = LinePattern.Match(raw);
                if (!match.Success) continue;
                float minutes = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                float seconds = float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                lyrics.times.Add(minutes * 60f + seconds);
                lyrics.lines.Add(match.Groups[3].Value.Trim());
            }
            return lyrics;
        }

        /// <summary>
        /// The lines around a playback time. Progress is 0-1 through the current line
        /// (it reaches 1 a little before the next line starts, so the fill finishes on time).
        /// Before the first line, the current line is empty and "next" shows what's coming.
        /// </summary>
        public void Get(float songTime, out string previous, out string current, out float progress, out string next)
            => Get(songTime, out previous, out current, out progress, out next, out _);

        /// <summary>Same, plus the line after next, so the singer can read ahead.</summary>
        public void Get(float songTime, out string previous, out string current, out float progress, out string next, out string afterNext)
        {
            previous = current = next = afterNext = string.Empty;
            progress = 0f;
            if (IsEmpty) return;

            float time = songTime - Offset;
            int index = times.BinarySearch(time);
            if (index < 0) index = ~index - 1; // the last line that has started
            if (index < 0)
            {
                next = lines[0];
                afterNext = lines.Count > 1 ? lines[1] : string.Empty;
                return;
            }

            current = lines[index];
            previous = index > 0 ? lines[index - 1] : string.Empty;
            next = index + 1 < lines.Count ? lines[index + 1] : string.Empty;
            afterNext = index + 2 < lines.Count ? lines[index + 2] : string.Empty;

            float start = times[index];
            float end = index + 1 < times.Count ? times[index + 1] : start + 4f;
            float singing = Math.Max(0.5f, (end - start) * 0.85f);
            progress = Math.Clamp((time - start) / singing, 0f, 1f);
        }
    }
}
