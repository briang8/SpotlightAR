using System;
using System.Collections;
using UnityEngine;

namespace SpotlightAR.Audio
{
    /// <summary>
    /// Checks that the song will not leak into the mic. It plays three short beeps through
    /// whatever audio output is active and listens for them. If the mic hears the beeps, the
    /// sound is coming from a speaker (phone, Bluetooth or wired) and the player is asked to
    /// connect headphones; if not, the output is headphones. Testing the actual sound path is
    /// more reliable than asking Android what is connected, which cannot tell a Bluetooth
    /// speaker from Bluetooth headphones.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class SpeakerCheck : MonoBehaviour
    {
        public enum Result { NotRun, Running, Headphones, Speaker, NoMic }

        const int Window = 1024;

        [SerializeField] MicInput mic;
        [Tooltip("Beep pitch (Hz). Narrow-band detection at this exact frequency ignores most other sounds.")]
        [SerializeField] float toneHz = 1760f;
        [SerializeField, Range(0f, 1f)] float toneVolume = 0.5f;
        [Tooltip("How much louder the beep frequency must get (power ratio) to count as heard.")]
        [SerializeField] float detectionRatio = 8f;
        [Tooltip("Extra listening time after the beeps, for Bluetooth delay.")]
        [SerializeField] float latencyAllowance = 0.4f;

        readonly float[] buffer = new float[Window];
        AudioSource source;
        AudioClip beeps;
        Coroutine running;

        public Result Current { get; private set; } = Result.NotRun;
        public bool Passed => Current == Result.Headphones;

        public event Action<Result> Changed;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        void OnDestroy()
        {
            if (beeps != null) Destroy(beeps);
        }

        public void Run()
        {
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(Check());
        }

        IEnumerator Check()
        {
            SetResult(Result.Running);
            mic.StartListening();

            float waited = 0f;
            while (mic.SampleRate == 0 && waited < 4f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (mic.SampleRate == 0)
            {
                SetResult(Result.NoMic);
                yield break;
            }

            yield return new WaitForSecondsRealtime(0.4f);
            float ambient = 0f;
            yield return Measure(0.4f, power => ambient = Mathf.Max(ambient, power));

            if (beeps == null) beeps = CreateBeeps(AudioSettings.outputSampleRate);
            source.PlayOneShot(beeps, toneVolume);

            float heard = 0f;
            yield return Measure(beeps.length + latencyAllowance, power => heard = Mathf.Max(heard, power));

            bool speaker = heard > ambient * detectionRatio && heard > 1e-6f;
            SetResult(speaker ? Result.Speaker : Result.Headphones);
            running = null;
        }

        IEnumerator Measure(float seconds, Action<float> onPower)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end)
            {
                if (mic.TryReadLatest(buffer)) onPower(Goertzel(buffer, mic.SampleRate, toneHz));
                yield return null;
            }
        }

        /// <summary>Signal power at one frequency (Goertzel algorithm, much cheaper than a full FFT).</summary>
        static float Goertzel(float[] samples, int sampleRate, float frequency)
        {
            float coefficient = 2f * Mathf.Cos(2f * Mathf.PI * frequency / sampleRate);
            float previous = 0f, previous2 = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float current = samples[i] + coefficient * previous - previous2;
                previous2 = previous;
                previous = current;
            }
            float power = previous2 * previous2 + previous * previous - coefficient * previous * previous2;
            return power / (samples.Length * samples.Length);
        }

        AudioClip CreateBeeps(int sampleRate)
        {
            const float beepSeconds = 0.18f, gapSeconds = 0.12f;
            const int beepCount = 3;
            int length = Mathf.CeilToInt(sampleRate * (beepSeconds + gapSeconds) * beepCount);
            var data = new float[length];
            int beepLength = Mathf.CeilToInt(sampleRate * beepSeconds);
            int stride = Mathf.CeilToInt(sampleRate * (beepSeconds + gapSeconds));
            int fade = sampleRate / 200; // 5 ms fades avoid clicks

            for (int b = 0; b < beepCount; b++)
            {
                for (int i = 0; i < beepLength && b * stride + i < length; i++)
                {
                    float envelope = Mathf.Min(1f, Mathf.Min(i, beepLength - i) / (float)fade);
                    data[b * stride + i] = Mathf.Sin(2f * Mathf.PI * toneHz * i / sampleRate) * envelope;
                }
            }

            AudioClip clip = AudioClip.Create("Speaker Check", length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void SetResult(Result result)
        {
            Current = result;
            Changed?.Invoke(result);
        }
    }
}
