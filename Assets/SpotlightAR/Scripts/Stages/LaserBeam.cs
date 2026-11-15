using UnityEngine;

namespace SpotlightAR.Stages
{
    /// <summary>
    /// A sweeping club laser drawn with a LineRenderer. It sweeps faster, gets wider
    /// and shifts colour as the singer gets louder.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class LaserBeam : MonoBehaviour
    {
        [SerializeField] Color colorA = new Color(1f, 0.36f, 0.78f);
        [SerializeField] Color colorB = new Color(0.4f, 0.85f, 1f);
        [Tooltip("Beam length in stage units.")]
        [SerializeField] float length = 1.5f;
        [Tooltip("Base aim (pitch, yaw) relative to this object.")]
        [SerializeField] Vector2 aim = new Vector2(30f, 0f);
        [SerializeField] float sweepAngle = 30f;
        [SerializeField] Vector2 sweepSpeed = new Vector2(0.35f, 2.4f);
        [SerializeField] Vector2 width = new Vector2(0.006f, 0.016f);
        [SerializeField] Vector2 alpha = new Vector2(0.25f, 1f);
        [SerializeField] float phaseOffset;

        LineRenderer line;
        float level;
        float phase;
        float burst;

        void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            phase = phaseOffset;
        }

        public void SetLevel(float value) => level = Mathf.Clamp01(value);

        /// <summary>Short full-brightness flare, used for the finale.</summary>
        public void Burst() => burst = 1f;

        void Update()
        {
            float dt = Time.deltaTime;
            burst = Mathf.MoveTowards(burst, 0f, dt);
            float energy = Mathf.Clamp01(level + burst);
            phase += dt * Mathf.Lerp(sweepSpeed.x, sweepSpeed.y, energy);

            float yaw = aim.y + Mathf.Sin(phase) * sweepAngle;
            float pitch = aim.x + Mathf.Cos(phase * 0.8f) * sweepAngle * 0.35f;
            Vector3 direction = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;

            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, direction * length);

            // LineRenderer widths are not affected by the transform scale, so scale them by hand.
            line.widthMultiplier = Mathf.Lerp(width.x, width.y, energy) * transform.lossyScale.x;

            Color tint = Color.Lerp(colorA, colorB, 0.5f + 0.5f * Mathf.Sin(phase * 0.5f + energy * 3f));
            tint.a = Mathf.Lerp(alpha.x, alpha.y, energy);
            line.startColor = tint;
            line.endColor = tint;
        }
    }
}
