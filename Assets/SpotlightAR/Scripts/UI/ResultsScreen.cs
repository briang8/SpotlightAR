using System;
using SpotlightAR.Scoring;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpotlightAR.UI
{
    /// <summary>
    /// Results (flowchart: "Results: stars and score"): stars, a counting-up score,
    /// best score, coaching stats and a tip, then "Sing again" or "Pick a new song".
    /// Shown over the AR view so the stage's finale stays visible behind it.
    /// </summary>
    public sealed class ResultsScreen : UIScreen
    {
        const int StarCount = 5;
        const long StarDelayMs = 180;
        const float CountUpSeconds = 1.2f;

        readonly Label headline;
        readonly Label songTitle;
        readonly VisualElement stars;
        readonly Label score;
        readonly Label best;
        readonly VisualElement newBest;
        readonly Label coverageValue;
        readonly Label noteValue;
        readonly Label noteLabel;
        readonly Label pitchValue;
        readonly Label pitchLabel;
        readonly Label hypeValue;
        readonly Label hypeLabel;
        readonly Label tip;
        readonly Label keyLine;
        readonly Icon[] starIcons = new Icon[StarCount];

        IVisualElementScheduledItem countUp;

        public event Action SingAgainRequested;
        public event Action NewSongRequested;

        public ResultsScreen(VisualElement root) : base(root)
        {
            headline = Require<Label>("results-headline");
            songTitle = Require<Label>("results-song");
            stars = Require<VisualElement>("results-stars");
            score = Require<Label>("results-score");
            best = Require<Label>("results-best");
            newBest = Require<VisualElement>("results-new-best");
            coverageValue = Require<Label>("stat-coverage");
            noteValue = Require<Label>("stat-note");
            noteLabel = Require<Label>("stat-note-label");
            pitchValue = Require<Label>("stat-pitch");
            pitchLabel = Require<Label>("stat-pitch-label");
            hypeValue = Require<Label>("stat-hype");
            hypeLabel = Require<Label>("stat-hype-label");
            tip = Require<Label>("results-tip");
            keyLine = Require<Label>("results-key");

            Require<Button>("sing-again-button").clicked += () => SingAgainRequested?.Invoke();
            Require<Button>("new-song-button").clicked += () => NewSongRequested?.Invoke();

            stars.Clear();
            for (int i = 0; i < StarCount; i++)
            {
                var icon = new Icon { Glyph = IconGlyph.Star };
                icon.AddToClassList("results-star");
                stars.Add(icon);
                starIcons[i] = icon;
            }
        }

        public void ShowResult(PerformanceResult result)
        {
            headline.text = result.stars >= 4 ? "Encore!" : result.stars >= 2 ? "Nice show!" : "Warm-up done";
            songTitle.text = result.FinishedEarly
                ? $"{result.songTitle} · finished at {Songs.TimeFormat.MinutesSeconds(result.secondsPlayed)} of {Songs.TimeFormat.MinutesSeconds(result.songDuration)}"
                : result.songTitle;

            newBest.style.display = result.isNewBest && result.previousBest > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            best.text = result.isNewBest
                ? result.previousBest > 0 ? $"+{result.score - result.previousBest:N0} on your best" : "First time on this song: best saved"
                : $"Your best: {result.previousBest:N0}";

            coverageValue.text = $"{Mathf.RoundToInt(result.coverage * 100f)}%";
            if (result.style == ScoreStyle.Rap)
            {
                noteValue.text = $"{result.syllablesPerSecond:0.0}/s";
                noteLabel.text = "flow speed (syllables)";
                pitchValue.text = result.HasOnBeat ? Percent(result.onBeat) : "–";
                pitchLabel.text = result.HasOnBeat ? $"on the beat ({result.bpm:0} BPM)" : "song tempo not found";
            }
            else
            {
                noteValue.text = $"{result.longestNote:0.0}s";
                noteLabel.text = "longest steady note";
                if (result.HasInKey)
                {
                    pitchValue.text = Percent(result.inKey);
                    pitchLabel.text = result.HasSteadiness ? $"in the song's key · {Percent(result.steadiness)} steady" : "in the song's key";
                }
                else if (result.HasOwnKey)
                {
                    pitchValue.text = Percent(result.inOwnKey);
                    pitchLabel.text = result.HasSteadiness ? $"stayed in one key · {Percent(result.steadiness)} steady" : "stayed in one key";
                }
                else
                {
                    pitchValue.text = result.HasSteadiness ? Percent(result.steadiness) : "–";
                    pitchLabel.text = result.HasSteadiness ? "pitch steadiness" : "hold a note to rate pitch";
                }
            }
            hypeValue.text = $"x{result.peakHype}";
            hypeLabel.text = result.secondsAtMaxHype >= 1f ? $"peak hype · {result.secondsAtMaxHype:0}s at x3" : "peak crowd hype";
            tip.text = result.GetTip();

            string keyText = KeyLine(result);
            keyLine.text = keyText;
            keyLine.style.display = string.IsNullOrEmpty(keyText) ? DisplayStyle.None : DisplayStyle.Flex;
            keyLine.EnableInClassList("results__key--off", result.keyMatch == KeyMatch.Different);

            AnimateStars(result.stars);
            AnimateScore(result.score);
        }

        static string Percent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";

        /// <summary>Compares the key the player sang in with the song's key (singing only).</summary>
        static string KeyLine(PerformanceResult result)
        {
            if (result.style == ScoreStyle.Rap || !result.HasSungKey) return string.Empty;
            switch (result.keyMatch)
            {
                case KeyMatch.Match:
                    return $"You sang in {result.sungKey}. The song is in {result.songKey}: same notes, you matched it ✓";
                case KeyMatch.Different:
                    return $"You sang in {result.sungKey}, but the song is in {result.songKey}. Different notes, so you were off key.";
                default:
                    return $"You sang in {result.sungKey}. We couldn't find this song's key, so look it up on Tunebat to compare.";
            }
        }

        void AnimateStars(int earned)
        {
            for (int i = 0; i < StarCount; i++)
            {
                Icon icon = starIcons[i];
                icon.Glyph = IconGlyph.Star;
                icon.RemoveFromClassList("results-star--earned");
                if (i >= earned) continue;
                icon.schedule.Execute(() =>
                {
                    icon.Glyph = IconGlyph.StarFilled;
                    icon.AddToClassList("results-star--earned");
                }).ExecuteLater(300 + StarDelayMs * i);
            }
        }

        void AnimateScore(int target)
        {
            countUp?.Pause();
            float started = Time.unscaledTime;
            score.text = "0";
            countUp = score.schedule.Execute(() =>
            {
                float t = Mathf.Clamp01((Time.unscaledTime - started) / CountUpSeconds);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                score.text = Mathf.RoundToInt(target * eased).ToString("N0");
                if (t >= 1f) countUp.Pause();
            }).Every(16);
        }
    }
}
