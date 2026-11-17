namespace SpotlightAR.Songs.Playback
{
    /// <summary>
    /// The tiny HTML page loaded into the WebView. It embeds the official YouTube IFrame
    /// Player API and reports state and time back to Unity through the plugin's
    /// <c>Unity.call</c> bridge ("ready", "state:N", "time:current:duration", "error:code").
    /// </summary>
    /// <remarks>
    /// YouTube's Required Minimum Functionality rules say an app must identify itself in the
    /// HTTP Referer as https://&lt;app id&gt;. Loading the page with that base URL sets the Referer;
    /// without it the player refuses to play (errors 152/153).
    /// </remarks>
    static class YouTubePlayerPage
    {
        public static string BaseUrl => $"https://{UnityEngine.Application.identifier.ToLowerInvariant()}";

        public static string Build(string videoId, float volume = 1f) => Template
            .Replace("{{VOLUME}}", UnityEngine.Mathf.RoundToInt(UnityEngine.Mathf.Clamp01(volume) * 100f).ToString())
            .Replace("{{VIDEO_ID}}", videoId)
            .Replace("{{ORIGIN}}", BaseUrl);

        const string Template = @"<!DOCTYPE html>
<html>
<head>
<meta name=""viewport"" content=""width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no"">
<style>
  html, body { margin: 0; padding: 0; width: 100%; height: 100%; background: #0C0B0D; overflow: hidden; }
  #player { position: absolute; inset: 0; width: 100%; height: 100%; }
</style>
</head>
<body>
<div id=""player""></div>
<script>
  function send(message) { if (window.Unity) { Unity.call(message); } }
  var player;
  var pendingVolume = {{VOLUME}};
  // Called from Unity: our own controls replace YouTube's, so the video can't be tapped by accident.
  function seekBy(seconds) { if (player && player.getCurrentTime) { player.seekTo(Math.max(0, player.getCurrentTime() + seconds), true); } }
  function setVol(volume) { pendingVolume = volume; if (player && player.setVolume) { player.setVolume(volume); } }
  var tag = document.createElement('script');
  tag.src = 'https://www.youtube.com/iframe_api';
  document.head.appendChild(tag);

  function onYouTubeIframeAPIReady() {
    player = new YT.Player('player', {
      videoId: '{{VIDEO_ID}}',
      playerVars: { playsinline: 1, autoplay: 1, controls: 0, disablekb: 1, rel: 0, fs: 0, iv_load_policy: 3, origin: '{{ORIGIN}}', widget_referrer: '{{ORIGIN}}' },
      events: {
        onReady: function () { send('ready'); player.setVolume(pendingVolume); player.playVideo(); },
        onStateChange: function (e) { send('state:' + e.data); },
        onError: function (e) { send('error:' + e.data); }
      }
    });
    setInterval(function () {
      if (player && player.getCurrentTime) {
        send('time:' + player.getCurrentTime().toFixed(2) + ':' + (player.getDuration() || 0).toFixed(2));
      }
    }, 250);
  }
</script>
</body>
</html>";
    }
}
