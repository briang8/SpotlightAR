using System;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace SpotlightAR.Audio
{
    /// <summary>
    /// Captures the phone microphone and turns it into a smoothed 0..1 loudness value.
    /// Only loudness is measured (RMS of the latest samples, converted to decibels);
    /// the voice is never played back, which avoids echo through the speaker.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MicInput : MonoBehaviour, ILoudnessSource
    {
        const float MinDb = -80f;

        [Header("Capture")]
        [Tooltip("Preferred sample rate. Clamped to what the device supports.")]
        [SerializeField] int sampleRate = 44100;
        [Tooltip("How many of the newest samples are measured each frame.")]
        [SerializeField, Range(256, 4096)] int sampleSize = 1024;
        [SerializeField] bool startOnEnable = true;

        [Header("Loudness mapping (decibels)")]
        [Tooltip("Anything quieter than this counts as silence.")]
        [SerializeField] float noiseFloorDb = -50f;
        [Tooltip("Anything louder than this counts as full loudness.")]
        [SerializeField] float ceilingDb = -12f;
        [Tooltip("Raise the silence threshold automatically in noisy rooms.")]
        [SerializeField] bool adaptiveNoiseFloor = true;
        [Tooltip("How far above the measured room noise the voice has to be (dB).")]
        [SerializeField] float noiseMarginDb = 6f;

        [Header("Smoothing")]
        [Tooltip("How quickly loudness rises when singing starts.")]
        [SerializeField] float attack = 18f;
        [Tooltip("How quickly loudness falls when singing stops.")]
        [SerializeField] float release = 5f;

        AudioClip clip;
        string device;
        float[] samples;
        float level;
        float peak;
        float decibels = MinDb;
        float ambientDb = -60f;
        bool wantsToListen;

        /// <summary>Raised when capture starts (true) or stops (false).</summary>
        public event Action<bool> ListeningChanged;

        /// <summary>Raised when the player refuses microphone access.</summary>
        public event Action PermissionDenied;

        /// <summary>When muted, loudness reads as zero but capture keeps running.</summary>
        public bool Muted { get; set; }

        /// <summary>Latest unsmoothed level in decibels (useful for debugging).</summary>
        public float Decibels => decibels;

        /// <summary>Recent peak loudness that decays slowly. Used by the mic check.</summary>
        public float Peak => Muted ? 0f : peak;

        public bool HasDevice => Microphone.devices.Length > 0;

        public float GetLoudness() => Muted ? 0f : level;

        public bool IsListening() => clip != null && Microphone.IsRecording(device);

        void OnEnable()
        {
            if (startOnEnable) StartListening();
        }

        void OnDisable() => StopCapture();

        void OnApplicationPause(bool paused)
        {
            // Release the mic while the app is in the background, resume afterwards.
            if (paused) StopCapture();
            else if (wantsToListen) StartListening();
        }

        public void StartListening()
        {
            wantsToListen = true;
            if (IsListening()) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => BeginCapture();
                callbacks.PermissionDenied += _ => PermissionDenied?.Invoke();
                Permission.RequestUserPermission(Permission.Microphone, callbacks);
                return;
            }
#endif
            BeginCapture();
        }

        public void StopListening()
        {
            wantsToListen = false;
            StopCapture();
        }

        void BeginCapture()
        {
            if (!HasDevice)
            {
                Debug.LogWarning("[MicInput] No microphone found.");
                return;
            }

            device = null; // default microphone
            Microphone.GetDeviceCaps(device, out int minRate, out int maxRate);
            int rate = (minRate == 0 && maxRate == 0) ? sampleRate : Mathf.Clamp(sampleRate, minRate, maxRate);

            // A 1-second looping buffer is plenty: we only ever read the newest samples.
            clip = Microphone.Start(device, true, 1, rate);
            samples = new float[sampleSize];
            ListeningChanged?.Invoke(true);
        }

        void StopCapture()
        {
            if (clip == null) return;
            Microphone.End(device);
            Destroy(clip);
            clip = null;
            level = 0f;
            peak = 0f;
            ListeningChanged?.Invoke(false);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float target = 0f;

            if (IsListening() && TryReadDecibels(out decibels))
            {
                float floor = noiseFloorDb;
                if (adaptiveNoiseFloor)
                {
                    TrackAmbient(decibels, dt);
                    floor = Mathf.Clamp(ambientDb + noiseMarginDb, noiseFloorDb, ceilingDb - 12f);
                }
                target = Mathf.InverseLerp(floor, ceilingDb, decibels);
            }

            float rate = target > level ? attack : release;
            level = Mathf.Lerp(level, target, 1f - Mathf.Exp(-rate * dt));
            peak = Mathf.Max(level, peak - 0.35f * dt);
        }

        bool TryReadDecibels(out float db)
        {
            db = MinDb;
            int position = Microphone.GetPosition(device);
            if (position <= 0) return false; // the device has not delivered audio yet

            int start = position - sampleSize;
            if (start < 0) start += clip.samples; // GetData wraps around the looping buffer
            if (!clip.GetData(samples, start)) return false;

            float sum = 0f;
            for (int i = 0; i < samples.Length; i++) sum += samples[i] * samples[i];
            float rms = Mathf.Sqrt(sum / samples.Length);
            db = rms > 1e-5f ? 20f * Mathf.Log10(rms) : MinDb;
            return true;
        }

        /// <summary>
        /// Follows the quietest recent level (the room's background noise): drops instantly,
        /// rises slowly, so sustained singing never gets mistaken for background noise.
        /// </summary>
        void TrackAmbient(float db, float dt)
        {
            if (db < ambientDb) ambientDb = db;
            else ambientDb += 0.75f * dt;
        }
    }
}
