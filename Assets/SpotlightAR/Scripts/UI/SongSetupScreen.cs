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
        readonly Button recheckButton;
        readonly Button partyModeButton;
        readonly List<Button> stageCards = new List<Button>();

        bool headphonesOk;
        bool partyMode;
        MicCheck.Result micResult = MicCheck.Result.Listening;

        public event Action BackRequested;
        public event Action<int> StageSelected;
        public event Action ContinueRequested;
        public event Action RecheckRequested;
        /// <summary>The player has no headphones and picked party mode.</summary>
        public event Action PartyModeRequested;
        public event Action<string> StageNameChanged;

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
            recheckButton = Require<Button>("recheck-button");
            partyModeButton = Require<Button>("party-mode-button");

            Require<Button>("back-button").clicked += () => BackRequested?.Invoke();
            continueButton.clicked += () => ContinueRequested?.Invoke();
            skipButton.clicked += () => ContinueRequested?.Invoke();
            recheckButton.clicked += () => RecheckRequested?.Invoke();
            partyModeButton.clicked += () => PartyModeRequested?.Invoke();

            nameField = Require<TextField>("stage-name-field");
            nameField.textEdition.placeholder = "Spotlight";
            nameField.textEdition.hidePlaceholderOnFocus = true;
            nameField.RegisterValueChangedCallback(evt => StageNameChanged?.Invoke(evt.newValue));
            BindModeSwitch();
        }

        readonly TextField nameField;
        Button modeKaraoke;
        Button modeSing;
        Label modeCaption;

        /// <summary>The player picked a mode: true = Sing mode (original song + synced lyrics), false = karaoke video.</summary>
        public event Action<bool> ModeSelected;

        void BindModeSwitch()
        {
            modeKaraoke = Require<Button>("mode-karaoke");
            modeSing = Require<Button>("mode-sing");
            modeCaption = Require<Label>("mode-caption");
            modeKaraoke.clicked += () => ModeSelected?.Invoke(false);
            modeSing.clicked += () => ModeSelected?.Invoke(true);
        }

        /// <summary>Shows which mode is selected, whether Sing mode can be picked, and a one-line explanation.</summary>
        public void SetMode(bool singSelected, bool singAvailable, string caption)
        {
            modeKaraoke.EnableInClassList("segment--selected", !singSelected);
            modeSing.EnableInClassList("segment--selected", singSelected);
            modeSing.SetEnabled(singAvailable);
            modeCaption.text = caption ?? string.Empty;
        }

        public void SetStageName(string stageName) => nameField.SetValueWithoutNotify(stageName ?? string.Empty);

        /// <summary>Show the headphone check. The mic check only starts once headphones pass.</summary>
        public void ShowHeadphoneCheck(SpeakerCheck.Result result)
        {
            headphonesOk = result == SpeakerCheck.Result.Headphones;
            partyMode = false;
            partyModeButton.style.display = result == SpeakerCheck.Result.Speaker ? DisplayStyle.Flex : DisplayStyle.None;
            micPanel.EnableInClassList("mic-panel--help", result == SpeakerCheck.Result.Speaker || result == SpeakerCheck.Result.NoMic);
            micPanel.EnableInClassList("mic-panel--heard", false);
            recheckButton.style.display = result == SpeakerCheck.Result.Speaker || result == SpeakerCheck.Result.NoMic ? DisplayStyle.Flex : DisplayStyle.None;

            switch (result)
            {
                case SpeakerCheck.Result.Running:
                    micStatus.text = "Checking your headphones…";
                    micTip.text = "Stay quiet for a second while we listen for three beeps.";
                    break;
                case SpeakerCheck.Result.Speaker:
                    micStatus.text = "Sound is coming from a speaker";
                    micTip.text = "Connect headphones (wired or Bluetooth) so the mic only hears you, not the song, then tap Check again. No headphones? Party mode lets the crowd dance to everything, but the show isn't scored.";
                    break;
                case SpeakerCheck.Result.NoMic:
                    micStatus.text = "No microphone access";
                    micTip.text = "Allow microphone access for Spotlight AR in your phone's Settings, then tap Check again.";
                    break;
                case SpeakerCheck.Result.Headphones:
                    ShowMicResult(micResult);
                    return;
            }
            UpdateButtons();
        }

        /// <summary>
        /// Party mode: no headphones, so the mic hears the song as loudly as the player. The
        /// crowd reacts to everything and the show isn't scored, so no mic check is needed.
        /// </summary>
        public void UsePartyMode()
        {
            partyMode = true;
            partyModeButton.style.display = DisplayStyle.None;
            recheckButton.style.display = DisplayStyle.Flex;
            micPanel.EnableInClassList("mic-panel--help", false);
            micPanel.EnableInClassList("mic-panel--heard", true);
            micStatus.text = "Party mode on";
            micTip.text = "The crowd dances to everything it hears, the song included, and the show isn't scored. Plug in headphones and tap Check again to be scored.";
            UpdateButtons();
        }

        public void SetSong(Song song)
        {
            title.text = song.title;
            meta.text = string.IsNullOrEmpty(song.artist) ? $"{song.genre} · {song.FormattedDuration}" : $"{song.artist} · {song.genre} · {song.FormattedDuration}";
            cover.style.backgroundImage = StyleKeyword.None;
            cover.RemoveFromClassList("cover--loaded");
            CoverImages.Apply(cover, song.GetCoverUrl());
            meter.ClearLevels();
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
            micResult = result;
            if (!headphonesOk || partyMode)
            {
                UpdateButtons();
                return;
            }

            micPanel.EnableInClassList("mic-panel--heard", result == MicCheck.Result.Heard);
            micPanel.EnableInClassList("mic-panel--help", result == MicCheck.Result.NeedsHelp);
            recheckButton.style.display = DisplayStyle.None;
            UpdateButtons();

            switch (result)
            {
                case MicCheck.Result.Heard:
                    micStatus.text = "Headphones on, and we can hear you!";
                    micTip.text = "You're ready. Find a floor or table for your stage.";
                    break;
                case MicCheck.Result.NeedsHelp:
                    micStatus.text = "Can't hear you yet";
                    micTip.text = "Sing a little louder or hold the phone closer. Check the app has microphone access, and turn the media volume up.";
                    break;
                default:
                    micStatus.text = "Headphones on. Now sing a line";
                    micTip.text = "Say or sing anything to test your mic.";
                    break;
            }
        }

        void UpdateButtons()
        {
#if UNITY_EDITOR
            // Laptops usually play through speakers, so the Editor can always skip the checks.
            const bool editor = true;
#else
            const bool editor = false;
#endif
            continueButton.SetEnabled(partyMode || (headphonesOk && micResult == MicCheck.Result.Heard));
            bool canSkipMic = !partyMode && headphonesOk && micResult == MicCheck.Result.NeedsHelp;
            skipButton.text = editor && !canSkipMic ? "Skip checks (Editor only)" : "Continue without mic check";
            skipButton.style.display = canSkipMic || (editor && !partyMode) ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
