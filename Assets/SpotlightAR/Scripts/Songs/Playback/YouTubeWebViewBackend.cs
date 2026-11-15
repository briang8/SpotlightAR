#if SPOTLIGHT_WEBVIEW
using System;
using System.Globalization;
using Gree.UnityWebView;
using UnityEngine;

namespace SpotlightAR.Songs.Playback
{
    /// <summary>
    /// Plays the karaoke video with the official YouTube IFrame player inside a native
    /// Android WebView (gree/unity-webview) laid over the AR view.
    /// </summary>
    public sealed class YouTubeWebViewBackend : ISongPlayerBackend
    {
        const float TapHintDelay = 2.5f;
        const int CornerRadius = 28;

        readonly WebViewObject webView;
        PlaybackState state = PlaybackState.Idle;
        float currentTime;
        float duration;
        float loadingFor;
        bool ready;

        public event Action<PlaybackState> StateChanged;
        public event Action<string> Failed;

        public float CurrentTime => currentTime;
        public float Duration => duration;

        public YouTubeWebViewBackend(Transform parent)
        {
            var host = new GameObject("YouTube WebView");
            host.transform.SetParent(parent, false);
            webView = host.AddComponent<WebViewObject>();
            webView.Init(
                cb: OnMessage,
                err: message => Fail($"Could not load the player ({message}). Check your internet connection."),
                httpErr: message => Fail($"Player connection error ({message})."),
                transparent: false,
                zoom: false,
                radius: CornerRadius,
                androidForceDarkMode: 1);
            webView.SetVisibility(false);
        }

        public void Load(Song song)
        {
            ready = false;
            currentTime = 0f;
            duration = song.durationSeconds;
            loadingFor = 0f;
            SetState(PlaybackState.Loading);
            webView.LoadHTML(YouTubePlayerPage.Build(song.videoId), YouTubePlayerPage.BaseUrl);
        }

        public void Play()
        {
            if (ready) webView.EvaluateJS("player.playVideo();");
        }

        public void Pause()
        {
            if (ready) webView.EvaluateJS("player.pauseVideo();");
        }

        public void Stop()
        {
            if (ready) webView.EvaluateJS("player.stopVideo();");
            webView.LoadHTML("<html><body style='background:#0E0F16'></body></html>", YouTubePlayerPage.BaseUrl);
            ready = false;
            SetState(PlaybackState.Idle);
        }

        public void SetViewport(Rect screenRect, bool visible)
        {
            webView.SetMargins(
                Mathf.RoundToInt(screenRect.xMin),
                Mathf.RoundToInt(screenRect.yMin),
                Mathf.RoundToInt(Screen.width - screenRect.xMax),
                Mathf.RoundToInt(Screen.height - screenRect.yMax));
            webView.SetVisibility(visible);
        }

        public void Tick(float deltaTime)
        {
            // Some phones block autoplay until the user touches the video.
            if (state != PlaybackState.Loading) return;
            loadingFor += deltaTime;
            if (loadingFor > TapHintDelay && ready) SetState(PlaybackState.WaitingForTap);
        }

        public void Dispose()
        {
            if (webView != null) UnityEngine.Object.Destroy(webView.gameObject);
        }

        void OnMessage(string message)
        {
            if (message == "ready")
            {
                ready = true;
                return;
            }

            int colon = message.IndexOf(':');
            if (colon < 0) return;
            string kind = message.Substring(0, colon);
            string value = message.Substring(colon + 1);

            switch (kind)
            {
                case "time":
                    string[] parts = value.Split(':');
                    float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out currentTime);
                    if (parts.Length > 1 && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float d) && d > 0f) duration = d;
                    break;
                case "state":
                    OnYouTubeState(value);
                    break;
                case "error":
                    Fail(DescribeError(value));
                    break;
            }
        }

        void OnYouTubeState(string code)
        {
            switch (code)
            {
                case "1": SetState(PlaybackState.Playing); break;
                case "2": SetState(PlaybackState.Paused); break;
                case "0": SetState(PlaybackState.Ended); break;
                case "-1":
                case "5":
                    if (state == PlaybackState.Idle) SetState(PlaybackState.Loading);
                    break;
            }
        }

        static string DescribeError(string code) => code switch
        {
            "2" => "This video link is invalid.",
            "5" => "This video can't play on this phone.",
            "100" => "This video was removed or made private.",
            "101" or "150" or "153" => "The uploader doesn't allow this video to play in other apps. Pick another song.",
            _ => $"The video couldn't play (error {code})."
        };

        void Fail(string message)
        {
            SetState(PlaybackState.Failed);
            Failed?.Invoke(message);
        }

        void SetState(PlaybackState next)
        {
            if (state == next) return;
            state = next;
            StateChanged?.Invoke(next);
        }
    }
}
#endif
