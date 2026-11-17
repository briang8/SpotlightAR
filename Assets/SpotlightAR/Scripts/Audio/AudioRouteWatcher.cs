using UnityEngine;

namespace SpotlightAR.Audio
{
    /// <summary>
    /// Notices headphones (wired, USB or Bluetooth) being connected or removed on Android by
    /// asking the system AudioManager for its list of output devices about once a second.
    /// Backs up Unity's AudioSettings.OnAudioConfigurationChanged, which not every phone reports.
    /// </summary>
    public sealed class AudioRouteWatcher
    {
        const float PollSeconds = 1f;
        const int GetDevicesOutputs = 2; // AudioManager.GET_DEVICES_OUTPUTS

        float nextPoll;
        string lastSignature;

#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject audioManager;
#endif

        /// <summary>Call every frame. Returns true once when the set of output devices changes.</summary>
        public bool Poll()
        {
            if (Time.unscaledTime < nextPoll) return false;
            nextPoll = Time.unscaledTime + PollSeconds;

            string signature = ReadSignature();
            if (signature == null) return false;
            bool changed = lastSignature != null && signature != lastSignature;
            lastSignature = signature;
            return changed;
        }

        string ReadSignature()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (audioManager == null)
                {
                    using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    audioManager = activity.Call<AndroidJavaObject>("getSystemService", "audio");
                }

                AndroidJavaObject[] devices = audioManager.Call<AndroidJavaObject[]>("getDevices", GetDevicesOutputs);
                var ids = new System.Text.StringBuilder();
                foreach (AndroidJavaObject device in devices)
                {
                    ids.Append(device.Call<int>("getId")).Append(',');
                    device.Dispose();
                }
                return ids.ToString();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AudioRouteWatcher] {e.Message}");
                return null;
            }
#else
            return null; // the Editor has no headphone events to watch
#endif
        }
    }
}
