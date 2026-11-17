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
    public sealed class MicInput : MonoBehaviour, ILoudnessSource, IPitchSource
    {
        const float MinDb = -80f;
        const int PitchWindow = 2048;

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

        [Header("Pitch")]
        [Tooltip("Only look for a pitch when loudness is above this (saves CPU in silence).")]
        [SerializeField, Range(0f, 1f)] float pitchGate = 0.12f;
        [Tooltip("Ignore pitch readings the detector is unsure about.")]
        [SerializeField, Range(0f, 1f)] float minPitchConfidence = 0.8f;

        [Header("Syllable onsets (rhythm)")]
        [Tooltip("Energy jump (ratio) between short audio blocks that counts as a new syllable.")]
        [SerializeField] float onsetRatio = 2f;
        [Tooltip("Minimum time between two syllables (seconds).")]
        [SerializeField] float onsetMinGap = 0.08f;

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
        float[] pitchSamples;
        PitchDetector pitchDetector;
        float pitch;
        int captureRate;

        // Onset detection works on small consecutive blocks of every new sample.
        const int OnsetHop = 512;
        readonly float[] hopBuffer = new float[OnsetHop];
        int lastReadPosition;
        float previousEnergy;
        float lastOnsetTime;
        float currentFloorDb = -50f;

        /// <summary>
        /// Raised when a new syllable starts. The argument is how many seconds ago it happened,
        /// so listeners can line it up exactly with the song's playback time.
        /// </summary>
        public event Action<float> OnsetDetected;

        /// <summary>Raised when capture starts (true) or stops (false).</summary>
        public event Action<bool> ListeningChanged;

        /// <summary>Raised when the player refuses microphone access.</summary>
#pragma warning disable CS0067 // only raised on Android, where the permission prompt exists
        public event Action PermissionDenied;
#pragma warning restore CS0067

        /// <summary>When muted, loudness reads as zero but capture keeps running.</summary>
        public bool Muted { get; set; }

        /// <summary>Latest unsmoothed level in decibels (useful for debugging).</summary>
        public float Decibels => decibels;

        /// <summary>Recent peak loudness that decays slowly. Used by the mic check.</summary>
        public float Peak => Muted ? 0f : peak;

        public bool HasDevice => Microphone.devices.Length > 0;

        public float GetLoudness() => Muted ? 0f : level;

        public float GetPitch() => Muted ? 0f : pitch;

        /// <summary>Sample rate of the running capture (0 when not listening).</summary>
        public int SampleRate => IsListening() ? captureRate : 0;

        /// <summary>Copies the newest mic samples into buffer. False when nothing is available yet.</summary>
        public bool TryReadLatest(float[] buffer)
        {
            if (!IsListening()) return false;
            int position = Microphone.GetPosition(device);
            if (position <= 0) return false;
            int start = position - buffer.Length;
            if (start < 0) start += clip.samples;
            return clip.GetData(buffer, start);
        }

        public bool IsListening() => clip != null && Microphone.IsRecording(device);

        /// <summary>
        /// Raised when headphones or another audio device are connected or removed. The mic is
        /// restarted automatically; listeners can re-check where the sound is going.
        /// </summary>
        public event Action RouteChanged;

        // Health check: plugging headphones in moves Android's mic to the headset, and the old
        // capture then delivers frozen or perfectly silent audio. Restart it when that happens.
        const float StallSeconds = 0.5f;
        const float DeadSeconds = 1f;
        const float RestartCooldown = 2f;
        int lastPosition = -1;
        float stalledFor;
        float deadFor;
        float lastRestart = -10f;
        bool lastReadWasDead;

        void OnEnable()
        {
            AudioSettings.OnAudioConfigurationChanged += OnAudioDeviceChanged;
            if (startOnEnable) StartListening();
        }

        void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioDeviceChanged;
            StopCapture();
        }

        readonly AudioRouteWatcher routeWatcher = new AudioRouteWatcher();
        float lastRouteChange = -10f;

        void OnAudioDeviceChanged(bool deviceWasChanged)
        {
            if (!deviceWasChanged) return;
            // Unity's own output needs a reset after a device change (Unity docs pattern).
            AudioSettings.Reset(AudioSettings.GetConfiguration());
            HandleRouteChange();
        }

        void HandleRouteChange()
        {
            // Both Unity and the Android watcher may report the same plug-in; act once.
            if (Time.unscaledTime - lastRouteChange < 1.5f) return;
            lastRouteChange = Time.unscaledTime;
            RestartCapture("audio device changed");
            RouteChanged?.Invoke();
        }

        void RestartCapture(string reason)
        {
            if (!wantsToListen) return;
            lastRestart = Time.unscaledTime;
            stalledFor = deadFor = 0f;
            lastPosition = -1;
            Debug.Log($"[MicInput] Restarting the microphone: {reason}");
            StopCapture();
            StartListening();
        }

        void CheckCaptureHealth(float dt)
        {
            int position = Microphone.GetPosition(device);
            stalledFor = position == lastPosition ? stalledFor + dt : 0f;
            lastPosition = position;
            deadFor = lastReadWasDead ? deadFor + dt : 0f;

            if (Time.unscaledTime - lastRestart < RestartCooldown) return;
            if (stalledFor >= StallSeconds) RestartCapture("capture stopped moving");
            else if (deadFor >= DeadSeconds) RestartCapture("capture went silent");
        }

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
            captureRate = rate;
            lastReadPosition = 0;
            previousEnergy = 0f;
            samples = new float[sampleSize];
            pitchSamples = new float[PitchWindow];
            pitchDetector = new PitchDetector(PitchWindow);
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
            pitch = 0f;
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
                currentFloorDb = floor;
                target = Mathf.InverseLerp(floor, ceilingDb, decibels);
                DetectOnsets();
            }
            if (IsListening()) CheckCaptureHealth(dt);
            if (routeWatcher.Poll()) HandleRouteChange();

            float rate = target > level ? attack : release;
            level = Mathf.Lerp(level, target, 1f - Mathf.Exp(-rate * dt));
            peak = Mathf.Max(level, peak - 0.35f * dt);
            pitch = target >= pitchGate ? ReadPitch() : 0f;
        }

        /// <summary>
        /// Walks every new sample in 512-sample blocks (about 12 ms) and fires an onset when a
        /// block's energy jumps well above the previous ones: the start of a syllable.
        /// </summary>
        void DetectOnsets()
        {
            int position = Microphone.GetPosition(device);
            int total = clip.samples;
            int available = position - lastReadPosition;
            if (available < 0) available += total;
            if (available > total / 2)
            {
                lastReadPosition = position; // fell too far behind (app paused): skip ahead
                return;
            }

            float gate = Mathf.Pow(10f, currentFloorDb / 10f); // floor in dB -> energy
            float now = Time.unscaledTime;
            while (available >= OnsetHop)
            {
                clip.GetData(hopBuffer, lastReadPosition);
                lastReadPosition = (lastReadPosition + OnsetHop) % total;
                available -= OnsetHop;

                float energy = 0f;
                for (int i = 0; i < OnsetHop; i++) energy += hopBuffer[i] * hopBuffer[i];
                energy /= OnsetHop;

                float secondsAgo = available / (float)captureRate;
                bool jump = energy > gate && energy > previousEnergy * onsetRatio;
                if (jump && !Muted && now - secondsAgo - lastOnsetTime >= onsetMinGap)
                {
                    lastOnsetTime = now - secondsAgo;
                    OnsetDetected?.Invoke(secondsAgo);
                }
                previousEnergy = Mathf.Lerp(previousEnergy, energy, 0.5f);
            }
        }

        float ReadPitch()
        {
            int position = Microphone.GetPosition(device);
            if (position <= 0 || pitchDetector == null) return 0f;

            int start = position - PitchWindow;
            if (start < 0) start += clip.samples;
            if (!clip.GetData(pitchSamples, start)) return 0f;

            float hz = pitchDetector.Detect(pitchSamples, captureRate, out float confidence);
            return confidence >= minPitchConfidence ? hz : 0f;
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
            // A working mic always picks up a little noise; exact digital silence means a dead route.
            lastReadWasDead = sum <= 0f;
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
