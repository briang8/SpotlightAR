using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace SpotlightAR.AR
{
    /// <summary>
    /// Matches the virtual stage to the real room. ARCore estimates the camera image's
    /// brightness and colour each frame; this sets the scene's main light from that and
    /// darkens the stage's ambient fill in dim rooms, so the stage looks like it belongs
    /// in the space instead of floating on top of it. Uses the cheap ambient estimation
    /// mode rather than full environmental HDR.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class LightEstimationApplier : MonoBehaviour
    {
        static readonly int RoomDarknessId = Shader.PropertyToID("_SpotlightRoomDarkness");

        [SerializeField] ARCameraManager cameraManager;
        [Tooltip("Main light intensity in a very dark and a very bright room.")]
        [SerializeField] Vector2 intensityRange = new Vector2(0.35f, 1.3f);
        [Tooltip("How much the stage's ambient fill can dim in a dark room (0-1).")]
        [SerializeField, Range(0f, 1f)] float maxDarkening = 0.6f;
        [SerializeField] float smoothing = 3f;

        Light mainLight;
        float targetIntensity = 1f;
        float targetDarkness;
        float darkness;
        Color targetColor = Color.white;

        void Awake() => mainLight = GetComponent<Light>();

        void OnEnable()
        {
            if (cameraManager == null) return;
            cameraManager.requestedLightEstimation = LightEstimation.AmbientIntensity | LightEstimation.AmbientColor;
            cameraManager.frameReceived += OnFrame;
        }

        void OnDisable()
        {
            if (cameraManager != null) cameraManager.frameReceived -= OnFrame;
            Shader.SetGlobalFloat(RoomDarknessId, 0f);
        }

        void OnFrame(ARCameraFrameEventArgs args)
        {
            ARLightEstimationData estimate = args.lightEstimation;
            if (estimate.averageBrightness.HasValue)
            {
                // ARCore reports roughly 0.1 (dim) to 0.6 (bright) indoors.
                float brightness = Mathf.Clamp01(estimate.averageBrightness.Value * 2f);
                targetIntensity = Mathf.Lerp(intensityRange.x, intensityRange.y, brightness);
                targetDarkness = (1f - brightness) * maxDarkening;
            }

            if (estimate.colorCorrection.HasValue)
            {
                Color tint = estimate.colorCorrection.Value;
                float max = Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b));
                if (max > 0.001f) targetColor = new Color(tint.r / max, tint.g / max, tint.b / max);
            }
        }

        void Update()
        {
            float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            mainLight.intensity = Mathf.Lerp(mainLight.intensity, targetIntensity, t);
            mainLight.color = Color.Lerp(mainLight.color, targetColor, t);
            darkness = Mathf.Lerp(darkness, targetDarkness, t);
            Shader.SetGlobalFloat(RoomDarknessId, darkness);
        }
    }
}
