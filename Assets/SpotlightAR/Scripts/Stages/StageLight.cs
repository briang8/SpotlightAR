using UnityEngine;

namespace SpotlightAR.Stages
{
    /// <summary>
    /// A stage spotlight made of a lamp housing, a lens glow and a fake volumetric beam.
    /// No real Light component is used: the beam is a camera-facing quad with an additive
    /// shader, which costs a fraction of a real spot light on mobile.
    /// </summary>
    public sealed class StageLight : MonoBehaviour
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        [SerializeField] Transform beam;
        [SerializeField] Renderer beamRenderer;
        [SerializeField] Renderer lensRenderer;

        [Header("Look")]
        [SerializeField] Color color = new Color(1f, 0.71f, 0.28f);
        [Tooltip("Beam width at the far end, as a fraction of the beam length.")]
        [SerializeField] float spread = 0.45f;
        [SerializeField] Vector2 beamIntensity = new Vector2(0.12f, 0.85f);
        [SerializeField] Vector2 lensIntensity = new Vector2(0.4f, 1.6f);

        [Header("Motion")]
        [SerializeField] float swayAngle = 7f;
        [SerializeField] float swaySpeed = 0.8f;

        Quaternion aimRotation = Quaternion.identity;
        MaterialPropertyBlock block;
        float level;
        float flash;
        float phase;

        public Color Color
        {
            get => color;
            set => color = value;
        }

        void Awake()
        {
            block = new MaterialPropertyBlock();
            phase = Random.value * 10f;
            aimRotation = transform.localRotation;
        }

        /// <summary>Points the lamp at a target and stretches the beam to reach it.</summary>
        public void AimAt(Transform target)
        {
            if (target == null || transform.parent == null) return;

            Vector3 localTarget = transform.parent.InverseTransformPoint(target.position);
            Vector3 direction = localTarget - transform.localPosition;
            if (direction.sqrMagnitude < 1e-6f) return;

            aimRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.localRotation = aimRotation;

            if (beam == null) return;
            float length = direction.magnitude;
            beam.localPosition = new Vector3(0f, 0f, length * 0.5f);
            beam.localScale = new Vector3(length * spread, length, 1f);
        }

        public void SetLevel(float value) => level = Mathf.Clamp01(value);

        public void Flash() => flash = 1f;

        void Update()
        {
            float dt = Time.deltaTime;
            phase += dt * swaySpeed * (0.6f + level);
            flash = Mathf.MoveTowards(flash, 0f, dt * 1.5f);

            float sway = swayAngle * (0.35f + level);
            transform.localRotation = aimRotation * Quaternion.Euler(Mathf.Sin(phase) * sway, Mathf.Cos(phase * 0.7f) * sway, 0f);

            Apply(beamRenderer, Mathf.Lerp(beamIntensity.x, beamIntensity.y, level) + flash);
            Apply(lensRenderer, Mathf.Lerp(lensIntensity.x, lensIntensity.y, level) + flash * 2f);
        }

        void Apply(Renderer target, float intensity)
        {
            if (target == null) return;
            target.GetPropertyBlock(block);
            block.SetColor(ColorId, color);
            block.SetFloat(IntensityId, intensity);
            target.SetPropertyBlock(block);
        }
    }
}
