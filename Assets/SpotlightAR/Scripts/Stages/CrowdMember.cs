using UnityEngine;

namespace SpotlightAR.Stages
{
    /// <summary>
    /// One silhouette in the crowd, animated with simple scripted motion (no Animator),
    /// which keeps the crowd cheap enough for mid-range phones.
    /// Quiet: gentle sway. Loud: bouncing with arms up. Cheer: a jump.
    /// </summary>
    public sealed class CrowdMember : MonoBehaviour
    {
        [SerializeField] Transform figure;
        [SerializeField] Transform leftArm;
        [SerializeField] Transform rightArm;

        [Header("Motion")]
        [SerializeField] Vector2 bounceHeight = new Vector2(0.004f, 0.03f);
        [SerializeField] Vector2 bounceSpeed = new Vector2(1.2f, 2.6f);
        [SerializeField] float cheerHeight = 0.06f;
        [Tooltip("Arm angle when hanging down / raised (degrees around Z).")]
        [SerializeField] Vector2 armAngles = new Vector2(168f, 28f);
        [Tooltip("Energy at which arms start going up.")]
        [SerializeField, Range(0f, 1f)] float armsUpThreshold = 0.55f;

        Vector3 figureRest;
        float energy;
        float phase;
        float cheer;
        float armsUp;

        void Awake()
        {
            if (figure == null) figure = transform;
            figureRest = figure.localPosition;
            phase = Random.value * Mathf.PI * 2f;
        }

        public void SetEnergy(float value) => energy = Mathf.Clamp01(value);

        public void Cheer() => cheer = 1f;

        void Update()
        {
            float dt = Time.deltaTime;
            phase += dt * Mathf.Lerp(bounceSpeed.x, bounceSpeed.y, energy) * Mathf.PI;
            cheer = Mathf.MoveTowards(cheer, 0f, dt * 1.4f);

            float bounce = Mathf.Abs(Mathf.Sin(phase)) * Mathf.Lerp(bounceHeight.x, bounceHeight.y, energy * energy);
            float jump = Mathf.Sin(cheer * Mathf.PI) * cheerHeight;
            figure.localPosition = figureRest + Vector3.up * (bounce + jump);

            float armsTarget = Mathf.Max(Mathf.InverseLerp(armsUpThreshold, 1f, energy), cheer);
            armsUp = Mathf.Lerp(armsUp, armsTarget, 1f - Mathf.Exp(-6f * dt));
            float wave = Mathf.Sin(phase * 0.5f) * 12f * armsUp;
            float angle = Mathf.Lerp(armAngles.x, armAngles.y, armsUp);

            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(0f, 0f, angle + wave);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler(0f, 0f, -angle - wave);
        }
    }
}
