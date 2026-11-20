using System;
using System.Collections.Generic;
using SpotlightAR.AR;
using SpotlightAR.Audio;
using SpotlightAR.Scoring;
using SpotlightAR.Songs;
using SpotlightAR.Songs.Playback;
using SpotlightAR.Stages;
using SpotlightAR.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;

namespace SpotlightAR.Core
{
    /// <summary>
    /// Runs a session and connects the systems together (Mediator): the screens raise events,
    /// the GameManager decides what happens next, and the AR, audio, song, scoring and stage
    /// systems never need to know about each other.
    /// Flow: Home → Song setup → Scan &amp; place → Sing → Results → Sing again / Home.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        const float ResultsDelay = 1.8f;
        const float HeadphonesTipSeconds = 5f;

        [Header("Systems")]
        [SerializeField] ARSession arSession;
        [SerializeField] MicInput mic;
        [SerializeField] SpeakerCheck speakerCheck;
        [SerializeField] StagePlacer stagePlacer;
        [SerializeField] TouchGestureInput gestures;
        [SerializeField] SongCatalog catalog;
        [SerializeField] SongPlayer songPlayer;
        [SerializeField] UIManager ui;

        [Header("Stages")]
        [SerializeField] List<StageTheme> stageThemes = new List<StageTheme>();

        [Header("Scoring")]
        [SerializeField] ScoringSettings scoring = new ScoringSettings();

        [Header("Performance")]
        [Tooltip("ARCore delivers camera frames at 30 FPS, so rendering faster only costs battery.")]
        [SerializeField] int targetFrameRate = 30;

        readonly MicCheck micCheck = new MicCheck();
        ScoreManager score;
        PlayerProfile profile;
        PerformanceResult pendingResult;
        string selectedGenre = HomeScreen.ForYou;
        string searchQuery = string.Empty;
        int selectedStage;
        float resultsAt = -1f;
        float noticeClearAt = -1f;
        bool showingTapHint;
        string stageName;

        const string StageNameKey = "stage_name";
        const string StageIndexKey = "stage_index";
        const string SignColorKey = "sign_color";

        PerformanceHistory history;
        int signColor;
        readonly Dictionary<int, RenderTexture> stagePreviews = new Dictionary<int, RenderTexture>();
        bool previewsStarted;

        // Sing mode: the original song quietly underneath time-synced lyrics.
        SingModeService singMode;
        SyncedLyrics singLyrics;
        Song singOriginal;
        bool singSelected;
        const float DefaultSingVolume = 0.45f;
        const string KaraokeCaption = "The karaoke version, with the lyrics in the video.";
        const string SingCaption = "The original song plays quietly as your guide, with lyrics that fill in as you go.";

        /// <summary>The video actually playing: the original recording in Sing mode, otherwise the karaoke video.</summary>
        Song PlayingSong => singSelected && singOriginal != null ? singOriginal : SelectedSong;

        string VolumeKey => singSelected ? "sing_volume" : "karaoke_volume";
        float SongVolume => PlayerPrefs.GetFloat(VolumeKey, singSelected ? DefaultSingVolume : 1f);

        public GameState State { get; private set; } = GameState.Home;
        public Song SelectedSong { get; private set; }

        public event Action<GameState> StateChanged;

        void Awake()
        {
            Application.targetFrameRate = targetFrameRate;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            score = new ScoreManager(scoring);
            profile = new PlayerProfile();
            singMode = new SingModeService(catalog.FindOriginal);
            history = new PerformanceHistory();
            selectedStage = PlayerPrefs.GetInt(StageIndexKey, 0);
            signColor = Mathf.Clamp(PlayerPrefs.GetInt(SignColorKey, 0), 0, StagesPage.SignColors.Length - 1);
        }

        void Start()
        {
            gestures.IsOverUI = ui.IsPointerOverUI;
            WireHome();
            WireSetup();
            WirePlacement();
            WireHud();
            WireResults();
            WireSystems();

            ui.Home.SetGenres(GenreNames());
            ui.Setup.SetStages(stageThemes);
            ui.Home.ShowLoading();
            catalog.LoadAll();
            EnterHome();
        }

        // ---------------- Wiring ----------------

        void WireHome()
        {
            HomeScreen home = ui.Home;
            home.GenreSelected += genre =>
            {
                selectedGenre = genre;
                home.SetSelectedGenre(genre);
                RefreshHomeList();
            };
            home.SearchChanged += text =>
            {
                searchQuery = text?.Trim() ?? string.Empty;
                RefreshHomeList();
            };
            home.OnlineSearchRequested += SearchOnline;
            home.SongChosen += song =>
            {
                SelectedSong = song;
                EnterSetup();
            };
            home.Library.SongChosen += song =>
            {
                SelectedSong = song;
                EnterSetup();
            };
            home.TabChanged += tab =>
            {
                if (tab == HomeTab.Library) home.Library.Show(history);
                if (tab == HomeTab.Stages) ShowStagesTab();
            };
            home.Stages.StageChosen += index =>
            {
                SelectStage(index);
                ShowStagesTab();
            };
            home.Stages.SignColorChosen += index =>
            {
                signColor = index;
                PlayerPrefs.SetInt(SignColorKey, index);
                ShowStagesTab();
            };
        }

        void WireSetup()
        {
            SongSetupScreen setup = ui.Setup;
            setup.BackRequested += EnterHome;
            setup.StageSelected += SelectStage;
            setup.ContinueRequested += EnterPlacing;
            setup.RecheckRequested += () => speakerCheck.Run();
            setup.ModeSelected += SelectMode;

            stageName = PlayerPrefs.GetString(StageNameKey, string.Empty);
            setup.SetStageName(stageName);
            setup.StageNameChanged += value =>
            {
                stageName = value;
                PlayerPrefs.SetString(StageNameKey, value);
            };
        }

        void WirePlacement()
        {
            PlacementScreen placement = ui.Placement;
            placement.CloseRequested += () => { stagePlacer.Clear(); EnterSetup(); };
            placement.SizeSelected += size => { stagePlacer.SetSize(size); placement.SetSize(size); };
            placement.PlaceRequested += StartSession;
            stagePlacer.SurfaceChanged += _ => RefreshPlacement();
            stagePlacer.StagePlaced += () =>
            {
                stagePlacer.Placed?.SetStageName(stageName);
                ApplySignColor();
                RefreshPlacement();
            };
        }

        void WireHud()
        {
            SingingHud hud = ui.Hud;
            hud.CloseRequested += () => EndSession(completed: false);
            hud.MoveRequested += () => { songPlayer.Pause(); EnterPlacing(); };
            hud.MicToggled += muted => mic.Muted = muted;
            hud.PauseToggled += SetPaused;
            hud.FinishRequested += () => EndSession(completed: true);
            hud.SeekRequested += seconds => songPlayer.SeekBy(seconds);
            hud.VolumeChanged += volume =>
            {
                PlayerPrefs.SetFloat(VolumeKey, volume);
                songPlayer.SetVolume(volume);
            };
        }

        void WireResults()
        {
            ui.Results.SingAgainRequested += () => BeginPerformance(restart: true);
            ui.Results.NewSongRequested += EnterHome;
        }

        void WireSystems()
        {
            catalog.GenreLoaded += (_, _, _) => RefreshHomeList();
            catalog.GenreFailed += (_, _) => RefreshHomeList();
            speakerCheck.Changed += result =>
            {
                if (result == SpeakerCheck.Result.Headphones) micCheck.Reset();
                ui.Setup.ShowHeadphoneCheck(result);
            };
            songPlayer.StateChanged += OnPlaybackState;
            songPlayer.Failed += message => ShowNotice(message, 8f);
            mic.RouteChanged += () =>
            {
                // Headphones plugged in or out: check again where the sound is going.
                if (State == GameState.SongSetup) speakerCheck.Run();
                else if (State == GameState.Singing)
                {
                    // Like music apps: pause so the song never blasts from the speaker into the mic.
                    SetPaused(true);
                    ShowNotice("Paused: your headphones changed. Put them back on, then tap play.", 6f);
                }
            };
            mic.PermissionDenied += () =>
            {
                ShowNotice("Microphone access is off. Allow it in Settings so the stage can hear you.", 8f);
                ui.Setup.ShowMicResult(MicCheck.Result.NeedsHelp);
            };

            score.HypeChanged += hype =>
            {
                stagePlacer.Placed?.SetHype(hype);
                ui.Hud.SetHype(hype);
                ui.Hud.PulseScore();
            };
            score.MilestoneReached += _ =>
            {
                stagePlacer.Placed?.OnMilestone();
                ui.Hud.PulseScore();
            };
        }

        // ---------------- States ----------------

        void EnterHome()
        {
            songPlayer.Stop();
            stagePlacer.Clear();
            SetArActive(false);
            resultsAt = -1f;
            ui.Show(ui.Home);
            RefreshHomeList();
            // A tab left open shows fresh data (a new show in the Library, a new best on a stage).
            if (ui.Home.Tab == HomeTab.Library) ui.Home.Library.Show(history);
            else if (ui.Home.Tab == HomeTab.Stages) ShowStagesTab();
            SetState(GameState.Home);
        }

        void EnterSetup()
        {
            if (SelectedSong == null) return;
            SetArActive(false);
            micCheck.Reset();
            mic.StartListening();
            ui.Setup.SetSong(SelectedSong);
            LookUpLyrics(SelectedSong);
            SelectStage(Mathf.Clamp(selectedStage, 0, Mathf.Max(0, stageThemes.Count - 1)));
            ui.Show(ui.Setup);
            SetState(GameState.SongSetup);
            // Headphones first (it needs silence), then the mic check.
            speakerCheck.Run();
        }

        void EnterPlacing()
        {
            SetArActive(true);
            if (selectedStage < stageThemes.Count && stageThemes[selectedStage].IsAvailable)
            {
                stagePlacer.SetStagePrefab(stageThemes[selectedStage].prefab);
                ui.Placement.SetStageName(stageThemes[selectedStage].displayName);
            }
            stagePlacer.Placed?.SetStageName(stageName);
            ui.Placement.SetSize(stagePlacer.Size);
            stagePlacer.BeginPlacement();
            ui.Show(ui.Placement);
            RefreshPlacement();
            SetState(GameState.Placing);
        }

        /// <summary>Lock the stage and start (or resume) the song.</summary>
        public void StartSession()
        {
            if (!stagePlacer.ConfirmPlacement()) return;
            bool resuming = songPlayer.Current != null && songPlayer.Current == PlayingSong && State == GameState.Placing && score.Score > 0;
            BeginPerformance(restart: !resuming);
        }

        void BeginPerformance(bool restart)
        {
            resultsAt = -1f;
            mic.StartListening();

            if (restart)
            {
                songPlayer.SetVolume(SongVolume);
                songPlayer.Play(PlayingSong);
                score.Begin();
                stagePlacer.Placed?.SetHype(1);
                ui.Hud.SetScore(0);
                ui.Hud.SetHype(1);
                ShowNotice("Tip: wear headphones so the mic hears you, not the song.", HeadphonesTipSeconds);
            }
            else
            {
                songPlayer.Resume();
            }

            bool lyricsOn = singSelected && singLyrics != null;
            ui.Hud.SetSong(SelectedSong.title, lyricsOn ? "Sing mode" : "Karaoke video");
            ui.Hud.SetLyricsVisible(lyricsOn);
            ui.Hud.SetVolume(SongVolume);
            ui.Hud.ShowPlayer(SelectedSong, songPlayer.ShowsVideo);
            ui.Hud.SetPaused(false);
            ui.Hud.SetMuted(mic.Muted);
            ui.Show(ui.Hud);
            SetState(GameState.Singing);
        }

        /// <summary>
        /// Finish the performance. A completed song plays the finale, saves the result and
        /// shows the results screen; closing early goes straight home with no result.
        /// </summary>
        public void EndSession(bool completed)
        {
            if (!completed)
            {
                EnterHome();
                return;
            }

            stagePlacer.Placed?.PlayFinale();
            pendingResult = score.BuildResult(SelectedSong.videoId, SelectedSong.title, songPlayer.GetTime(), songPlayer.Duration);
            profile.SaveResult(pendingResult);
            history.Add(SelectedSong, pendingResult, CurrentStageName, singSelected && singLyrics != null);
            songPlayer.Stop();
            ShowNotice(pendingResult.stars >= 4 ? "Encore! The crowd loved it." : "That's the end of the song!", ResultsDelay);
            resultsAt = Time.time + ResultsDelay;
            SetState(GameState.Results);
        }

        void SetPaused(bool paused)
        {
            if (State != GameState.Singing && State != GameState.Paused) return;
            if (paused) songPlayer.Pause();
            else songPlayer.Resume();
            SetState(paused ? GameState.Paused : GameState.Singing);
        }

        void OnPlaybackState(PlaybackState playback)
        {
            if (State != GameState.Singing && State != GameState.Paused) return;
            switch (playback)
            {
                case PlaybackState.WaitingForTap:
                    ShowNotice("Tap the video to start the song", 0f);
                    showingTapHint = true;
                    break;
                case PlaybackState.Playing:
                    if (showingTapHint) ShowNotice(null, 0f);
                    showingTapHint = false;
                    break;
                case PlaybackState.Ended:
                    EndSession(completed: true);
                    break;
            }
        }

        // ---------------- Per frame ----------------

        void Update()
        {
            HandleBackButton();

            switch (State)
            {
                case GameState.SongSetup:
                    float level = mic.GetLoudness();
                    ui.Setup.PushMicLevel(level);
                    if (!speakerCheck.Passed) break; // mic check waits for the headphone check
                    MicCheck.Result before = micCheck.Current;
                    if (micCheck.Update(level, Time.deltaTime) != before) ui.Setup.ShowMicResult(micCheck.Current);
                    break;

                case GameState.Singing:
                case GameState.Paused:
                case GameState.Results:
                    UpdatePerformance();
                    break;
            }

            if (State != GameState.Singing && State != GameState.Paused) songPlayer.SetViewport(Rect.zero, false);

            if (noticeClearAt > 0f && Time.time >= noticeClearAt)
            {
                noticeClearAt = -1f;
                ui.Hud.ShowNotice(null);
            }

            if (resultsAt > 0f && Time.time >= resultsAt)
            {
                resultsAt = -1f;
                ui.Show(ui.Results);
                ui.Results.ShowResult(pendingResult);
            }
        }

        void UpdatePerformance()
        {
            bool live = State == GameState.Singing && songPlayer.IsPlaying;
            float loudness = live ? mic.GetLoudness() : 0f;

            Stage stage = stagePlacer.Placed;
            if (stage != null) stage.ReactTo(loudness);

            if (live)
            {
                score.AddFrame(loudness, mic.GetPitch(), Time.deltaTime);
                ui.Hud.SetScore(score.Score);
                ui.Hud.SetLiveNote(score.LiveNote);
            }

            ui.Hud.Tick(loudness, Time.deltaTime);
            ui.Hud.SetTime(songPlayer.GetTime(), songPlayer.Duration);

            if (singSelected && singLyrics != null)
            {
                singLyrics.Get(songPlayer.GetTime(), out string previous, out string current, out float progress, out string next, out string afterNext);
                ui.Hud.SetLyrics(previous, current, progress, next, afterNext);
            }

            bool showVideo = State == GameState.Singing || State == GameState.Paused;
            songPlayer.SetViewport(ui.PanelToScreen(ui.Hud.PlayerPanelRect), showVideo);
        }

        void HandleBackButton()
        {
            // Android's back button arrives as Escape in the Input System.
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

#if UNITY_EDITOR
            // Editor testing only: End key finishes the song now and shows the results.
            if (keyboard.endKey.wasPressedThisFrame && (State == GameState.Singing || State == GameState.Paused)) EndSession(completed: true);
#endif
            if (!keyboard.escapeKey.wasPressedThisFrame) return;

            switch (State)
            {
                case GameState.SongSetup: EnterHome(); break;
                case GameState.Placing: stagePlacer.Clear(); EnterSetup(); break;
                case GameState.Singing:
                case GameState.Paused: EndSession(completed: false); break;
                case GameState.Results: EnterHome(); break;
            }
        }

        // ---------------- Helpers ----------------

        /// <summary>Show a HUD notice. seconds &lt;= 0 keeps it until replaced.</summary>
        void ShowNotice(string message, float seconds)
        {
            ui.Hud.ShowNotice(message);
            showingTapHint = false;
            noticeClearAt = message != null && seconds > 0f ? Time.time + seconds : -1f;
        }

        void RefreshHomeList()
        {
            HomeScreen home = ui.Home;
            if (searchQuery.Length > 0)
            {
                home.ShowSearchResults(searchQuery, catalog.Search(searchQuery));
                return;
            }

            if (selectedGenre == HomeScreen.ForYou)
            {
                List<Song> mix = catalog.GetMix(15);
                if (mix.Count > 0) home.ShowSongs(mix);
                else if (catalog.IsLoading) home.ShowLoading();
                else home.ShowError(catalog.FirstFailure ?? "No songs yet. Add playlist IDs to the GenrePlaylists asset.");
                return;
            }

            if (catalog.TryGetLoaded(selectedGenre, out IReadOnlyList<Song> songs))
                home.ShowSongs(songs, catalog.IsFromCache(selectedGenre) ? "Offline: showing your saved list." : null);
            else if (catalog.TryGetFailure(selectedGenre, out string failure))
                home.ShowError(failure);
            else
            {
                home.ShowLoading();
                catalog.LoadGenre(selectedGenre);
            }
        }

        async void SearchOnline(string query)
        {
            ui.Home.ShowOnlineSearching(query);
            try
            {
                List<Song> results = await catalog.SearchOnline(query);
                // Ignore late results if the player has typed something else meanwhile.
                if (string.Equals(searchQuery, query, StringComparison.OrdinalIgnoreCase)) ui.Home.ShowOnlineResults(query, results);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameManager] YouTube search failed: {e.Message}");
                if (string.Equals(searchQuery, query, StringComparison.OrdinalIgnoreCase))
                    ui.Home.ShowError("YouTube search isn't available right now. Check your connection and try again.");
            }
        }

        /// <summary>Checks for synced lyrics (free, cached). Sing mode can only be picked when they exist.</summary>
        async void LookUpLyrics(Song song)
        {
            singSelected = false;
            singLyrics = null;
            singOriginal = null;
            ui.Setup.SetMode(false, false, "Looking for synced lyrics for Sing mode…");

            SyncedLyrics lyrics = await singMode.GetLyrics(song);
            if (SelectedSong != song) return; // the player picked another song meanwhile
            singLyrics = lyrics;
            ui.Setup.SetMode(false, lyrics != null, lyrics != null
                ? KaraokeCaption
                : "Sing mode needs synced lyrics, and we couldn't find them for this song.");
        }

        /// <summary>
        /// Karaoke video or Sing mode. Sing mode finds the original recording on YouTube the
        /// first time (one search, then cached), and falls back to karaoke if it can't.
        /// </summary>
        async void SelectMode(bool sing)
        {
            Song song = SelectedSong;
            if (song == null) return;
            if (!sing || singLyrics == null)
            {
                singSelected = false;
                ui.Setup.SetMode(false, singLyrics != null, singLyrics != null ? KaraokeCaption : null);
                return;
            }

            ui.Setup.SetMode(true, true, "Finding the original song…");
            Song original;
            try
            {
                original = await singMode.GetOriginal(song);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameManager] Sing mode: {e.Message}");
                if (SelectedSong == song) ui.Setup.SetMode(false, true, "Sing mode can't search YouTube right now. Check your connection and try again.");
                return;
            }
            if (SelectedSong != song) return;

            if (original == null)
            {
                singSelected = false;
                ui.Setup.SetMode(false, true, "We couldn't find the original recording with matching timing, so this one is karaoke only.");
                return;
            }
            singOriginal = original;
            singSelected = true;
            ui.Setup.SetMode(true, true, SingCaption);
        }

        void SelectStage(int index)
        {
            if (index < 0 || index >= stageThemes.Count || !stageThemes[index].IsAvailable) return;
            selectedStage = index;
            ui.Setup.SetSelectedStage(index);
            PlayerPrefs.SetInt(StageIndexKey, index);
        }

        string CurrentStageName => selectedStage >= 0 && selectedStage < stageThemes.Count ? stageThemes[selectedStage].displayName : "Stage";

        void ShowStagesTab()
        {
            ui.Home.Stages.Show(stageThemes, selectedStage, history, signColor);
            foreach (var preview in stagePreviews) ui.Home.Stages.SetPreview(preview.Key, preview.Value);
            if (!previewsStarted) RenderStagePreviews();
        }

        /// <summary>Photographs each stage once for the Stages tab, one after another.</summary>
        async void RenderStagePreviews()
        {
            previewsStarted = true;
            for (int i = 0; i < stageThemes.Count; i++)
            {
                if (!stageThemes[i].IsAvailable) continue;
                RenderTexture texture = await StagePreviewRenderer.Render(stageThemes[i].prefab, 960, 560, new Color(0.086f, 0.071f, 0.075f), stageName);
                if (texture == null) continue;
                stagePreviews[i] = texture;
                ui.Home.Stages.SetPreview(i, texture);
            }
        }

        /// <summary>The neon sign colour chosen on the Stages tab (index 0 keeps the stage's own colour).</summary>
        void ApplySignColor()
        {
            if (signColor > 0) stagePlacer.Placed?.SetSignColor(StagesPage.SignColors[signColor].color);
        }

        void RefreshPlacement() => ui.Placement.SetSurface(stagePlacer.Surface, stagePlacer.Placed != null);

        /// <summary>AR only runs while it is needed: no camera or tracking cost on the menus.</summary>
        void SetArActive(bool active)
        {
            if (arSession != null && arSession.enabled != active) arSession.enabled = active;
        }

        IEnumerable<string> GenreNames()
        {
            foreach (GenreCatalog.Genre genre in catalog.Genres) yield return genre.displayName;
        }

        void SetState(GameState next)
        {
            if (State == next) return;
            State = next;
            StateChanged?.Invoke(next);
        }
    }
}
