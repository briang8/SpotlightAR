using System;
using UnityEngine;

namespace SpotlightAR.Songs.Playback
{
    /// <summary>
    /// Plays the selected song. On an Android phone it streams the YouTube karaoke video
    /// in a WebView; in the Editor it uses a simulated clock so the session can be tested.
    /// The rest of the game only ever talks to this class.
    /// </summary>
    public sealed class SongPlayer : MonoBehaviour
    {
        ISongPlayerBackend backend;

        public event Action<PlaybackState> StateChanged;
        public event Action<string> Failed;

        public Song Current { get; private set; }
        public PlaybackState State { get; private set; } = PlaybackState.Idle;
        public bool IsPlaying => State == PlaybackState.Playing;
        public float Duration => backend?.Duration ?? 0f;

        /// <summary>True when a real video is shown (false for the Editor simulation).</summary>
        public bool ShowsVideo { get; private set; }

        void Awake()
        {
#if SPOTLIGHT_WEBVIEW && UNITY_ANDROID && !UNITY_EDITOR
            backend = new YouTubeWebViewBackend(transform);
            ShowsVideo = true;
#else
            backend = new SimulatedSongBackend();
            ShowsVideo = false;
#endif
            backend.StateChanged += OnBackendState;
            backend.Failed += message => Failed?.Invoke(message);
        }

        void OnDestroy() => backend?.Dispose();

        void Update() => backend.Tick(Time.unscaledDeltaTime);

        public void Play(Song song)
        {
            Current = song ?? throw new ArgumentNullException(nameof(song));
            backend.Load(song);
            backend.Play();
        }

        public void Pause() => backend.Pause();

        public void Resume() => backend.Play();

        public void Stop()
        {
            backend.Stop();
            Current = null;
        }

        public float GetTime() => backend.CurrentTime;

        public void SetViewport(Rect screenRect, bool visible) => backend.SetViewport(screenRect, visible);

        void OnBackendState(PlaybackState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
