using System;
using SpotlightAR.AR;
using SpotlightAR.Audio;
using SpotlightAR.Stages;
using SpotlightAR.UI;
using UnityEngine;

namespace SpotlightAR.Core
{
    /// <summary>
    /// Runs a session and connects the systems together (Mediator): the UI raises events,
    /// the GameManager decides what happens, and the AR, audio and stage systems never
    /// need to know about each other.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        [Header("Systems")]
        [SerializeField] MicInput mic;
        [SerializeField] StagePlacer stagePlacer;
        [SerializeField] TouchGestureInput gestures;
        [SerializeField] UIManager ui;

        [Header("Performance")]
        [Tooltip("ARCore delivers camera frames at 30 FPS, so rendering faster only costs battery.")]
        [SerializeField] int targetFrameRate = 30;

        float sessionTime;

        public GameState State { get; private set; } = GameState.Placing;

        public event Action<GameState> StateChanged;

        void Awake()
        {
            Application.targetFrameRate = targetFrameRate;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        void Start()
        {
            gestures.IsOverUI = ui.IsPointerOverUI;

            PlacementScreen placement = ui.Placement;
            placement.CloseRequested += RestartPlacement;
            placement.SizeSelected += OnSizeSelected;
            placement.PlaceRequested += StartSession;

            SingingHud hud = ui.Hud;
            hud.CloseRequested += EndSession;
            hud.MoveRequested += EnterPlacing;
            hud.MicToggled += muted => mic.Muted = muted;
            hud.PauseToggled += paused => SetState(paused ? GameState.Paused : GameState.Singing);
            hud.FinaleRequested += () => stagePlacer.Placed?.PlayFinale();

            stagePlacer.SurfaceChanged += _ => RefreshPlacement();
            stagePlacer.StagePlaced += RefreshPlacement;
            mic.PermissionDenied += () => hud.ShowNotice("Microphone access is off. Allow it in Settings so the stage can hear you.");

            placement.SetStageName(stagePlacer.StagePrefab.DisplayName);
            placement.SetSize(stagePlacer.Size);
            EnterPlacing();
        }

        void Update()
        {
            bool live = State == GameState.Singing;
            float loudness = live ? mic.GetLoudness() : 0f;

            Stage stage = stagePlacer.Placed;
            if (stage != null) stage.ReactTo(loudness);

            if (State != GameState.Singing && State != GameState.Paused) return;
            if (live) sessionTime += Time.deltaTime;
            ui.Hud.Tick(loudness, Time.deltaTime);
            ui.Hud.SetTime(sessionTime, 0f);
        }

        /// <summary>Lock the stage and start singing.</summary>
        public void StartSession()
        {
            if (!stagePlacer.ConfirmPlacement()) return;

            mic.StartListening();
            ui.Hud.SetSong("Warm-up");
            ui.Hud.SetPaused(false);
            ui.Hud.SetMuted(mic.Muted);
            ui.Hud.SetHype(1);
            ui.Show(ui.Hud);
            SetState(GameState.Singing);
        }

        /// <summary>Finish the performance with the finale, then return to placement.</summary>
        public void EndSession()
        {
            stagePlacer.Placed?.PlayFinale();
            sessionTime = 0f;
            EnterPlacing();
        }

        void EnterPlacing()
        {
            stagePlacer.BeginPlacement();
            ui.Show(ui.Placement);
            RefreshPlacement();
            SetState(GameState.Placing);
        }

        void RestartPlacement()
        {
            stagePlacer.Clear();
            EnterPlacing();
        }

        void OnSizeSelected(StageSize size)
        {
            stagePlacer.SetSize(size);
            ui.Placement.SetSize(size);
        }

        void RefreshPlacement() => ui.Placement.SetSurface(stagePlacer.Surface, stagePlacer.Placed != null);

        void SetState(GameState next)
        {
            if (State == next) return;
            State = next;
            StateChanged?.Invoke(next);
        }
    }
}
