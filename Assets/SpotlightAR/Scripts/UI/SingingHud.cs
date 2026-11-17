using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpotlightAR.UI
{
    /// <summary>
    /// The heads-up display while singing: a slim song bar with score and hype on top,
    /// the AR stage in the middle, then synced lyrics (Sing mode), the playback controls
    /// and the YouTube player at the bottom.
    /// </summary>
    public sealed class SingingHud : UIScreen
    {
        const float MeterInterval = 0.07f;
        static readonly CustomStyleProperty<Color> AccentStyle = new CustomStyleProperty<Color>("--lyric-sung");

        readonly Label songTitle;
        readonly Label songSub;
        readonly Label timeElapsed;
        readonly Label timeTotal;
        readonly VisualElement progressFill;
        readonly VisualElement scoreCard;
        readonly Label scoreLabel;
        readonly Label hypeLabel;
        readonly Label noticeLabel;
        readonly VoiceMeter meter;
        readonly Icon micIcon;
        readonly Icon pauseIcon;
        readonly Button micButton;
        readonly VisualElement playerSlot;
        readonly VisualElement playerCover;
        readonly Label playerCaption;
        readonly VisualElement lyricsCard;
        readonly Label lyricPrev;
        readonly Label lyricNow;
        readonly Label lyricNext;
        readonly VisualElement volumeRow;
        readonly Slider volumeSlider;
        readonly Label volumeValue;
        readonly Label volumeLabel;

        string sungColor = "#8C1D40";
        float meterTimer;
        bool muted;
        bool paused;

        public event Action CloseRequested;
        public event Action<bool> MicToggled;
        public event Action MoveRequested;
        public event Action<bool> PauseToggled;
        /// <summary>The player wants to stop here and get results for what they sang.</summary>
        public event Action FinishRequested;
        /// <summary>Jump back or forward in the song by this many seconds.</summary>
        public event Action<float> SeekRequested;
        /// <summary>Song volume chosen on the slider, 0-1.</summary>
        public event Action<float> VolumeChanged;

        public SingingHud(VisualElement root) : base(root)
        {
            songTitle = Require<Label>("song-title");
            songSub = Require<Label>("song-sub");
            timeElapsed = Require<Label>("time-elapsed");
            timeTotal = Require<Label>("time-total");
            progressFill = Require<VisualElement>("song-progress-fill");
            scoreCard = Require<VisualElement>("score-card");
            scoreLabel = Require<Label>("score-label");
            hypeLabel = Require<Label>("hype-label");
            noticeLabel = Require<Label>("notice-label");
            meter = Require<VoiceMeter>("voice-meter");
            micIcon = Require<Icon>("mic-icon");
            pauseIcon = Require<Icon>("pause-icon");
            micButton = Require<Button>("mic-button");
            playerSlot = Require<VisualElement>("player-slot");
            playerCover = Require<VisualElement>("player-cover");
            playerCaption = Require<Label>("player-caption");
            lyricsCard = Require<VisualElement>("lyrics-card");
            lyricPrev = Require<Label>("lyric-prev");
            lyricNow = Require<Label>("lyric-now");
            lyricNext = Require<Label>("lyric-next");
            volumeRow = Require<VisualElement>("volume-row");
            volumeSlider = Require<Slider>("volume-slider");
            volumeValue = Require<Label>("volume-value");
            volumeLabel = Require<Label>("volume-label");

            Require<Button>("close-button").clicked += () => CloseRequested?.Invoke();
            Require<Button>("move-button").clicked += () => MoveRequested?.Invoke();
            Require<Button>("finish-button").clicked += () => FinishRequested?.Invoke();
            Require<Button>("rewind-button").clicked += () => SeekRequested?.Invoke(-10f);
            Require<Button>("forward-button").clicked += () => SeekRequested?.Invoke(10f);
            Require<Button>("volume-button").clicked += () => volumeRow.ToggleInClassList("volume-row--open");
            micButton.clicked += () => SetMuted(!muted, true);
            Require<Button>("pause-button").clicked += () => SetPaused(!paused, true);

            volumeSlider.RegisterValueChangedCallback(evt =>
            {
                ShowVolume(evt.newValue / 100f);
                VolumeChanged?.Invoke(evt.newValue / 100f);
            });

            // The sung part of the lyric is coloured with the theme accent (rich text needs a literal colour).
            lyricNow.RegisterCallback<CustomStyleResolvedEvent>(evt =>
            {
                if (evt.customStyle.TryGetValue(AccentStyle, out Color accent)) sungColor = "#" + ColorUtility.ToHtmlStringRGB(accent);
            });

            ShowNotice(null);
            SetLyricsVisible(false);
        }

        /// <summary>Song title, and the mode underneath ("Sing mode" with synced lyrics, or "Karaoke video").</summary>
        public void SetSong(string title, string subtitle = null)
        {
            songTitle.text = title;
            songSub.text = subtitle ?? "Karaoke video";
        }

        /// <summary>
        /// Reserve the video box. On the phone the native YouTube view is laid on top of it;
        /// in the Editor it shows the song cover as a stand-in.
        /// </summary>
        public void ShowPlayer(Songs.Song song, bool realVideo)
        {
            playerSlot.style.display = DisplayStyle.Flex;
            playerSlot.EnableInClassList("player-slot--simulated", !realVideo);
            playerCaption.text = realVideo ? string.Empty : "The YouTube video plays here on the phone";
            playerCover.style.backgroundImage = StyleKeyword.None;
            if (!realVideo && song != null) CoverImages.Apply(playerCover, song.GetCoverUrl());
        }

        public void HidePlayer() => playerSlot.style.display = DisplayStyle.None;

        /// <summary>The video box in panel coordinates, or an empty rect while it is not laid out.</summary>
        public Rect PlayerPanelRect => playerSlot.resolvedStyle.display == DisplayStyle.None ? Rect.zero : playerSlot.worldBound;

        /// <summary>Shows elapsed and total time either side of the progress bar.</summary>
        public void SetTime(float elapsed, float duration)
        {
            timeElapsed.text = Format(elapsed);
            timeTotal.text = duration > 0f ? Format(duration) : "--:--";
            float progress = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 0f;
            progressFill.style.width = Length.Percent(progress * 100f);
        }

        public void SetScore(int score) => scoreLabel.text = score.ToString("N0");

        public void SetHype(int multiplier)
        {
            hypeLabel.text = $"x{multiplier}";
            hypeLabel.EnableInClassList("hype-pill--x2", multiplier == 2);
            hypeLabel.EnableInClassList("hype-pill--x3", multiplier >= 3);
        }

        /// <summary>A quick bounce of the score (milestones, hype changes).</summary>
        public void PulseScore()
        {
            scoreCard.AddToClassList("score-card--pulse");
            scoreCard.schedule.Execute(() => scoreCard.RemoveFromClassList("score-card--pulse")).ExecuteLater(180);
        }

        /// <summary>Feed the live loudness; the meter samples it at a steady rate.</summary>
        public void Tick(float loudness, float deltaTime)
        {
            meterTimer += deltaTime;
            if (meterTimer < MeterInterval) return;
            meterTimer = 0f;
            meter.Push(paused ? 0f : loudness);
        }

        public void SetMuted(bool value, bool notify = false)
        {
            muted = value;
            micIcon.Glyph = muted ? IconGlyph.MicOff : IconGlyph.Mic;
            micButton.EnableInClassList("round-button--off", muted);
            if (notify) MicToggled?.Invoke(muted);
        }

        public void SetPaused(bool value, bool notify = false)
        {
            paused = value;
            pauseIcon.Glyph = paused ? IconGlyph.Play : IconGlyph.Pause;
            if (notify) PauseToggled?.Invoke(paused);
        }

        /// <summary>Short message above the lyrics. Pass null to hide it.</summary>
        public void ShowNotice(string message)
        {
            noticeLabel.text = message ?? string.Empty;
            noticeLabel.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // ---------- Sing mode ----------

        public void SetLyricsVisible(bool visible) => lyricsCard.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        /// <summary>
        /// Shows the previous, current and next lines. The part of the current line already
        /// sung (by time) fills in with the accent colour, like a karaoke screen.
        /// </summary>
        public void SetLyrics(string previous, string current, float progress, string next)
        {
            lyricPrev.text = previous ?? string.Empty;
            lyricNext.text = next ?? string.Empty;
            current ??= string.Empty;

            int split = Mathf.Clamp(Mathf.RoundToInt(current.Length * Mathf.Clamp01(progress)), 0, current.Length);
            // Fill whole words so the colour change doesn't land mid-word.
            while (split > 0 && split < current.Length && current[split] != ' ') split++;
            string sung = Escape(current.Substring(0, split));
            string rest = Escape(current.Substring(split));
            lyricNow.text = split > 0 ? $"<color={sungColor}>{sung}</color>{rest}" : rest;
        }

        /// <summary>Sets the slider and labels without raising VolumeChanged.</summary>
        public void SetVolume(float volume)
        {
            volumeSlider.SetValueWithoutNotify(Mathf.Round(Mathf.Clamp01(volume) * 100f));
            ShowVolume(volume);
        }

        void ShowVolume(float volume)
        {
            string text = $"{Mathf.RoundToInt(Mathf.Clamp01(volume) * 100f)}%";
            volumeValue.text = text;
            volumeLabel.text = text;
        }

        protected override void OnShown()
        {
            meter.ClearLevels();
            volumeRow.RemoveFromClassList("volume-row--open");
        }

        static string Escape(string text) => text.Replace("<", "‹"); // keep lyric text from being read as rich-text tags

        static string Format(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }
    }
}
