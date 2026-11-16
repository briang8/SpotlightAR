using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpotlightAR.UI
{
    /// <summary>
    /// The heads-up display while singing: song pill, hype card, voice meter and
    /// the four round controls (mic, move, pause, finale).
    /// </summary>
    public sealed class SingingHud : UIScreen
    {
        const float MeterInterval = 0.07f;

        readonly Label songTitle;
        readonly Label songTime;
        readonly VisualElement progressFill;
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

        float meterTimer;
        bool muted;
        bool paused;

        public event Action CloseRequested;
        public event Action<bool> MicToggled;
        public event Action MoveRequested;
        public event Action<bool> PauseToggled;
        public event Action FinaleRequested;

        public SingingHud(VisualElement root) : base(root)
        {
            songTitle = Require<Label>("song-title");
            songTime = Require<Label>("song-time");
            progressFill = Require<VisualElement>("song-progress-fill");
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

            Require<Button>("close-button").clicked += () => CloseRequested?.Invoke();
            Require<Button>("move-button").clicked += () => MoveRequested?.Invoke();
            Require<Button>("star-button").clicked += () => FinaleRequested?.Invoke();
            micButton.clicked += () => SetMuted(!muted, true);
            Require<Button>("pause-button").clicked += () => SetPaused(!paused, true);

            ShowNotice(null);
        }

        public void SetSong(string title) => songTitle.text = title;

        /// <summary>
        /// Reserve the video box. On the phone the native YouTube view is laid on top of it;
        /// in the Editor it shows the song cover as a stand-in.
        /// </summary>
        public void ShowPlayer(Songs.Song song, bool realVideo)
        {
            playerSlot.style.display = DisplayStyle.Flex;
            playerSlot.EnableInClassList("player-slot--simulated", !realVideo);
            playerCaption.text = realVideo ? string.Empty : "The karaoke video plays here on the phone";
            playerCover.style.backgroundImage = StyleKeyword.None;
            if (!realVideo && song != null) CoverImages.Apply(playerCover, song.GetCoverUrl());
        }

        public void HidePlayer() => playerSlot.style.display = DisplayStyle.None;

        /// <summary>The video box in panel coordinates, or an empty rect while it is not laid out.</summary>
        public Rect PlayerPanelRect => playerSlot.resolvedStyle.display == DisplayStyle.None ? Rect.zero : playerSlot.worldBound;

        /// <summary>Shows "elapsed / duration". A duration of 0 shows elapsed time only.</summary>
        public void SetTime(float elapsed, float duration)
        {
            songTime.text = duration > 0f ? $"{Format(elapsed)} / {Format(duration)}" : Format(elapsed);
            float progress = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 0f;
            progressFill.style.width = Length.Percent(progress * 100f);
        }

        public void SetScore(int score) => scoreLabel.text = score.ToString("N0");

        public void SetHype(int multiplier)
        {
            hypeLabel.text = $"Crowd hype x{multiplier}";
            hypeLabel.EnableInClassList("score-card__hype--x2", multiplier == 2);
            hypeLabel.EnableInClassList("score-card__hype--x3", multiplier >= 3);
        }

        /// <summary>A quick bounce of the score card (milestones, hype changes).</summary>
        public void PulseScore()
        {
            VisualElement card = scoreLabel.parent;
            card.AddToClassList("score-card--pulse");
            card.schedule.Execute(() => card.RemoveFromClassList("score-card--pulse")).ExecuteLater(180);
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

        /// <summary>Short message above the controls. Pass null to hide it.</summary>
        public void ShowNotice(string message)
        {
            noticeLabel.text = message ?? string.Empty;
            noticeLabel.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        protected override void OnShown() => meter.Clear();

        static string Format(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }
    }
}
