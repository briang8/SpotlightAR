using System;
using System.Collections.Generic;
using SpotlightAR.AR;
using SpotlightAR.Audio;
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
    /// the GameManager decides what happens next, and the AR, audio, song and stage systems
    /// never need to know about each other.
    /// Flow: Home → Song setup → Scan &amp; place → Sing → (Results) → Home.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        const float ReturnHomeDelay = 3f;

        [Header("Systems")]
        [SerializeField] ARSession arSession;
        [SerializeField] MicInput mic;
        [SerializeField] StagePlacer stagePlacer;
        [SerializeField] TouchGestureInput gestures;
        [SerializeField] SongCatalog catalog;
        [SerializeField] SongPlayer songPlayer;
        [SerializeField] UIManager ui;

        [Header("Stages")]
        [SerializeField] List<StageTheme> stageThemes = new List<StageTheme>();

        [Header("Performance")]
        [Tooltip("ARCore delivers camera frames at 30 FPS, so rendering faster only costs battery.")]
        [SerializeField] int targetFrameRate = 30;

        readonly MicCheck micCheck = new MicCheck();
        string selectedGenre = HomeScreen.ForYou;
        string searchQuery = string.Empty;
        int selectedStage;
        float returnHomeAt = -1f;

        public GameState State { get; private set; } = GameState.Home;
        public Song SelectedSong { get; private set; }

        public event Action<GameState> StateChanged;

        void Awake()
        {
            Application.targetFrameRate = targetFrameRate;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        void Start()
        {
            gestures.IsOverUI = ui.IsPointerOverUI;
            WireHome();
            WireSetup();
            WirePlacement();
            WireHud();
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
        }

        void WireSetup()
        {
            SongSetupScreen setup = ui.Setup;
            setup.BackRequested += EnterHome;
            setup.StageSelected += SelectStage;
            setup.ContinueRequested += EnterPlacing;
        }

        void WirePlacement()
        {
            PlacementScreen placement = ui.Placement;
            placement.CloseRequested += () => { stagePlacer.Clear(); EnterSetup(); };
            placement.SizeSelected += size => { stagePlacer.SetSize(size); placement.SetSize(size); };
            placement.PlaceRequested += StartSession;
            stagePlacer.SurfaceChanged += _ => RefreshPlacement();
            stagePlacer.StagePlaced += RefreshPlacement;
        }

        void WireHud()
        {
            SingingHud hud = ui.Hud;
            hud.CloseRequested += () => EndSession(completed: false);
            hud.MoveRequested += () => { songPlayer.Pause(); EnterPlacing(); };
            hud.MicToggled += muted => mic.Muted = muted;
            hud.PauseToggled += SetPaused;
            hud.FinaleRequested += () => stagePlacer.Placed?.PlayFinale();
        }

        void WireSystems()
        {
            catalog.GenreLoaded += (_, _, _) => RefreshHomeList();
            catalog.GenreFailed += (_, _) => RefreshHomeList();
            songPlayer.StateChanged += OnPlaybackState;
            songPlayer.Failed += message => ui.Hud.ShowNotice(message);
            mic.PermissionDenied += () =>
            {
                const string message = "Microphone access is off. Allow it in Settings so the stage can hear you.";
                ui.Hud.ShowNotice(message);
                ui.Setup.ShowMicResult(MicCheck.Result.NeedsHelp);
            };
        }

        // ---------------- States ----------------

        void EnterHome()
        {
            songPlayer.Stop();
            stagePlacer.Clear();
            SetArActive(false);
            returnHomeAt = -1f;
            ui.Show(ui.Home);
            RefreshHomeList();
            SetState(GameState.Home);
        }

        void EnterSetup()
        {
            if (SelectedSong == null) return;
            SetArActive(false);
            micCheck.Reset();
            mic.StartListening();
            ui.Setup.SetSong(SelectedSong);
            SelectStage(Mathf.Clamp(selectedStage, 0, Mathf.Max(0, stageThemes.Count - 1)));
            ui.Show(ui.Setup);
            SetState(GameState.SongSetup);
        }

        void EnterPlacing()
        {
            SetArActive(true);
            if (selectedStage < stageThemes.Count && stageThemes[selectedStage].IsAvailable)
            {
                stagePlacer.SetStagePrefab(stageThemes[selectedStage].prefab);
                ui.Placement.SetStageName(stageThemes[selectedStage].displayName);
            }
            ui.Placement.SetSize(stagePlacer.Size);
            stagePlacer.BeginPlacement();
            ui.Show(ui.Placement);
            RefreshPlacement();
            SetState(GameState.Placing);
        }

        /// <summary>Lock the stage, start the song and start singing.</summary>
        public void StartSession()
        {
            if (!stagePlacer.ConfirmPlacement()) return;

            bool resuming = songPlayer.Current != null && songPlayer.Current == SelectedSong;
            if (resuming) songPlayer.Resume();
            else songPlayer.Play(SelectedSong);

            ui.Hud.SetSong(SelectedSong.title);
            ui.Hud.ShowPlayer(SelectedSong, songPlayer.ShowsVideo);
            ui.Hud.SetPaused(false);
            ui.Hud.SetMuted(mic.Muted);
            ui.Hud.SetHype(1);
            ui.Hud.ShowNotice(null);
            ui.Show(ui.Hud);
            SetState(GameState.Singing);
        }

        /// <summary>
        /// Finish the performance. A completed song plays the finale before returning home
        /// (iteration 3 replaces this with the results screen).
        /// </summary>
        public void EndSession(bool completed)
        {
            if (completed)
            {
                stagePlacer.Placed?.PlayFinale();
                ui.Hud.ShowNotice("Encore! That's the end of the song.");
                returnHomeAt = Time.time + ReturnHomeDelay;
                SetState(GameState.Results);
            }
            else
            {
                EnterHome();
            }
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
                    ui.Hud.ShowNotice("Tap the video to start the song");
                    break;
                case PlaybackState.Playing:
                    ui.Hud.ShowNotice(null);
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
            if (returnHomeAt > 0f && Time.time >= returnHomeAt) EnterHome();
        }

        void UpdatePerformance()
        {
            bool live = State == GameState.Singing && songPlayer.IsPlaying;
            float loudness = live ? mic.GetLoudness() : 0f;

            Stage stage = stagePlacer.Placed;
            if (stage != null) stage.ReactTo(loudness);

            ui.Hud.Tick(loudness, Time.deltaTime);
            ui.Hud.SetTime(songPlayer.GetTime(), songPlayer.Duration);

            bool showVideo = State == GameState.Singing || State == GameState.Paused;
            songPlayer.SetViewport(ui.PanelToScreen(ui.Hud.PlayerPanelRect), showVideo);
        }

        void HandleBackButton()
        {
            // Android's back button arrives as Escape in the Input System.
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

            switch (State)
            {
                case GameState.SongSetup: EnterHome(); break;
                case GameState.Placing: stagePlacer.Clear(); EnterSetup(); break;
                case GameState.Singing:
                case GameState.Paused: EndSession(completed: false); break;
            }
        }

        // ---------------- Helpers ----------------

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

        void SelectStage(int index)
        {
            if (index < 0 || index >= stageThemes.Count || !stageThemes[index].IsAvailable) return;
            selectedStage = index;
            ui.Setup.SetSelectedStage(index);
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
