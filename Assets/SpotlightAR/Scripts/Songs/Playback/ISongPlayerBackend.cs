using System;
using UnityEngine;

namespace SpotlightAR.Songs.Playback
{
    /// <summary>Playback states reported by every backend.</summary>
    public enum PlaybackState
    {
        Idle,
        Loading,
        WaitingForTap,
        Playing,
        Paused,
        Ended,
        Failed
    }

    /// <summary>
    /// A way of playing a song. <see cref="SongPlayer"/> picks one at runtime:
    /// the YouTube WebView on the phone, or a simulated player in the Editor (Strategy pattern).
    /// </summary>
    public interface ISongPlayerBackend : IDisposable
    {
        event Action<PlaybackState> StateChanged;
        event Action<string> Failed;

        float CurrentTime { get; }
        float Duration { get; }

        void Load(Song song);
        void Play();
        void Pause();
        void Stop();

        /// <summary>Jump forward (positive) or back (negative) by this many seconds.</summary>
        void SeekBy(float seconds);

        /// <summary>Song volume, 0-1. Kept across songs.</summary>
        void SetVolume(float volume);

        /// <summary>Where the video should appear, in screen pixels (origin top-left).</summary>
        void SetViewport(Rect screenRect, bool visible);

        /// <summary>Called every frame by the owner.</summary>
        void Tick(float deltaTime);
    }
}
