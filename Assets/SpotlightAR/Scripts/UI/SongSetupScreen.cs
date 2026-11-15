using System;
using System.Collections.Generic;
using SpotlightAR.Audio;
using SpotlightAR.Songs;
using SpotlightAR.Stages;
using UnityEngine.UIElements;

namespace SpotlightAR.UI
{
    /// <summary>
    /// Song setup (flowchart step 2): pick a stage theme and pass the mic check
    /// before scanning the room.
    /// </summary>
    public sealed class SongSetupScreen : UIScreen
    {
        readonly VisualElement cover;
        readonly Label title;
        readonly Label meta;
        readonly VisualElement stageList;
        readonly VoiceMeter meter;
        readonly VisualElement micPanel;
        readonly Label micStatus;
        readonly Label micTip;
        readonly Button continueButton;
        readonly Button skipButton;
        readonly List<Button> stageCards = new List<Button>();

        public event Action BackRequested;
        public event Action<int> StageSelected;
        public event Action ContinueRequested;

        public SongSetupScreen(VisualElement root) : base(root)
        {
            cover = Require<VisualElement>("setup-cover");
            title = Require<Label>("setup-title");
            meta = Require<Label>("setup-meta");
            stageList = Require<VisualElement>("stage-list");
            meter = Require<VoiceMeter>("setup-meter");
            micPanel = Require<VisualElement>("mic-panel");
            micStatus = Require<Label>("mic-status");
            micTip = Require<Label>("mic-tip");
            continueButton = Require<Button>("continue-button");
            skipButton = Require<Button>("skip-mic-button");

            Require<Button>("back-button").clicked += () => BackRequested?.Invoke();
            continueButton.clicked += () => ContinueRequested?.Invoke();
            skipButton.clicked += () => ContinueRequested?.Invoke();
        }

        public void SetSong(Song song)
        {
            title.text = song.title;
            meta.text = string.IsNullOrEmpty(song.artist) ? $"{song.genre} · {song.FormattedDuration}" : $"{song.artist} · {song.genre} · {song.FormattedDuration}";
            cover.style.backgroundImage = StyleKeyword.None;
            cover.RemoveFromClassList("cover--loaded");
            CoverImages.Apply(cover, song.GetCoverUrl());
            meter.Clear();
            ShowMicResult(MicCheck.Result.Listening);
        }

        public void SetStages(IReadOnlyList<StageTheme> themes)
        {
            stageList.Clear();
            stageCards.Clear();
            for (int i = 0; i < themes.Count; i++)
            {
                int index = i;
                StageTheme theme = themes[i];

                var card = new Button(() => StageSelected?.Invoke(index));
                card.AddToClassList("stage-card");
                card.SetEnabled(theme.IsAvailable);

                var swatch = new VisualElement();
                swatch.AddToClassList("stage-card__swatch");
                swatch.style.backgroundColor = theme.accent;
                card.Add(swatch);

                var name = new Label(theme.displayName);
                name.AddToClassList("stage-card__name");
                name.AddToClassList("t-semibold");
                name.style.color = theme.accent;
                card.Add(name);

                var tagline = new Label(theme.IsAvailable ? theme.tagline : "Coming soon");
                tagline.AddToClassList("stage-card__tagline");
                card.Add(tagline);

                stageList.Add(card);
                stageCards.Add(card);
            }
        }

        public void SetSelectedStage(int index)
        {
            for (int i = 0; i < stageCards.Count; i++) stageCards[i].EnableInClassList("stage-card--selected", i == index);
        }

        public void PushMicLevel(float loudness) => meter.Push(loudness);

        public void ShowMicResult(MicCheck.Result result)
        {
            micPanel.EnableInClassList("mic-panel--heard", result == MicCheck.Result.Heard);
            micPanel.EnableInClassList("mic-panel--help", result == MicCheck.Result.NeedsHelp);
            continueButton.SetEnabled(result == MicCheck.Result.Heard);
            skipButton.style.display = result == MicCheck.Result.NeedsHelp ? DisplayStyle.Flex : DisplayStyle.None;

            switch (result)
            {
                case MicCheck.Result.Heard:
                    micStatus.text = "We can hear you!";
                    micTip.text = "Headphones keep the song out of the mic for a fairer score.";
                    break;
                case MicCheck.Result.NeedsHelp:
                    micStatus.text = "Can't hear you yet";
                    micTip.text = "Sing a little louder or hold the phone closer. Check the app has microphone access, and turn the media volume up.";
                    break;
                default:
                    micStatus.text = "Mic check: sing a line";
                    micTip.text = "Say or sing anything to test your mic.";
                    break;
            }
        }
    }
}
