using System;
using UnityEngine;

namespace SpotlightAR.Songs.Playback
{
    /// <summary>
    /// Editor stand-in for the YouTube player: no video, just a clock that runs for the
    /// song's real duration, so the whole session flow can be tested on a PC.
    /// </summary>
    public sealed class SimulatedSongBackend : ISongPlayerBackend
    {
        const float FallbackDuration = 90f;

        PlaybackState state = PlaybackState.Idle;
        float time;
        float duration;

        public event Action<PlaybackState> StateChanged;
        public event Action<string> Failed { add { } remove { } }

        public float CurrentTime => time;
        public float Duration => duration;

        public void Load(Song song)
        {
            time = 0f;
            duration = song != null && song.durationSeconds > 0 ? song.durationSeconds : FallbackDuration;
            SetState(PlaybackState.Paused);
        }

        public void Play()
        {
            if (state != PlaybackState.Ended) SetState(PlaybackState.Playing);
        }

        public void Pause()
        {
            if (state == PlaybackState.Playing) SetState(PlaybackState.Paused);
        }

        public void Stop()
        {
            time = 0f;
            SetState(PlaybackState.Idle);
        }

        public void SetViewport(Rect screenRect, bool visible) { }

        public void Tick(float deltaTime)
        {
            if (state != PlaybackState.Playing) return;
            time = Mathf.Min(duration, time + deltaTime);
            if (time >= duration) SetState(PlaybackState.Ended);
        }

        public void Dispose() { }

        void SetState(PlaybackState next)
        {
            if (state == next) return;
            state = next;
            StateChanged?.Invoke(next);
        }
    }
}
