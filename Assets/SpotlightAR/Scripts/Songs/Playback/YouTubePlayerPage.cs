namespace SpotlightAR.Songs.Playback
{
    /// <summary>
    /// The tiny HTML page loaded into the WebView. It embeds the official YouTube IFrame
    /// Player API and reports state and time back to Unity through the plugin's
    /// <c>Unity.call</c> bridge ("ready", "state:N", "time:current:duration", "error:code").
    /// </summary>
    static class YouTubePlayerPage
    {
        public const string BaseUrl = "https://www.youtube.com/";

        public static string Build(string videoId) => Template.Replace("{{VIDEO_ID}}", videoId);

        const string Template = @"<!DOCTYPE html>
<html>
<head>
<meta name=""viewport"" content=""width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no"">
<style>
  html, body { margin: 0; padding: 0; width: 100%; height: 100%; background: #0E0F16; overflow: hidden; }
  #player { position: absolute; inset: 0; width: 100%; height: 100%; }
</style>
</head>
<body>
<div id=""player""></div>
<script>
  function send(message) { if (window.Unity) { Unity.call(message); } }
  var player;
  var tag = document.createElement('script');
  tag.src = 'https://www.youtube.com/iframe_api';
  document.head.appendChild(tag);

  function onYouTubeIframeAPIReady() {
    player = new YT.Player('player', {
      videoId: '{{VIDEO_ID}}',
      playerVars: { playsinline: 1, autoplay: 1, controls: 1, rel: 0, modestbranding: 1, fs: 0, iv_load_policy: 3 },
      events: {
        onReady: function () { send('ready'); player.playVideo(); },
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
