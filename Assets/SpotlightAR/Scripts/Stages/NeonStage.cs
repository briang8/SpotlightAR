using UnityEngine;

namespace SpotlightAR.Stages
{
    /// <summary>
    /// Club Neon: crossing lasers that speed up and change colour with loudness,
    /// and a disco ball that spins faster on long notes.
    /// </summary>
    public sealed class NeonStage : Stage
    {
        static readonly int SparkleId = Shader.PropertyToID("_Sparkle");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        [Header("Club Neon")]
        [Tooltip("Leave empty to use every LaserBeam under this stage.")]
        [SerializeField] LaserBeam[] lasers;
        [SerializeField] Transform discoBall;
        [SerializeField] Renderer discoRenderer;
        [Tooltip("Spin speed (degrees/second) when idle and on a long held note.")]
        [SerializeField] Vector2 discoSpin = new Vector2(18f, 160f);
        [Tooltip("Seconds of held note needed for full disco spin.")]
        [SerializeField] float longNoteSeconds = 1.5f;
        [SerializeField] Renderer edgeRing;
        [SerializeField] Vector2 edgeRingIntensity = new Vector2(0.55f, 1.25f);

        MaterialPropertyBlock block;
        float spin;

        protected override void Awake()
        {
            base.Awake();
            if (lasers == null || lasers.Length == 0) lasers = GetComponentsInChildren<LaserBeam>(true);
            block = new MaterialPropertyBlock();
        }

        protected override void Start()
        {
            OnHypeChanged(Hype);
        }

        /// <summary>x1: two lasers. x2: all four. x3: all four, sweeping harder.</summary>
        protected override void OnHypeChanged(int hype)
        {
            for (int i = 0; i < lasers.Length; i++) lasers[i].gameObject.SetActive(hype >= 2 || i < 2);
        }

        protected override void OnReact(float level)
        {
            float boost = Hype == 3 ? 0.25f : 0f;
            foreach (LaserBeam laser in lasers) laser.SetLevel(Mathf.Clamp01(level + boost));

            float longNote = Mathf.Clamp01(Sustain / longNoteSeconds);
            float targetSpin = Mathf.Lerp(discoSpin.x, discoSpin.y, longNote);
            spin = Mathf.Lerp(spin, targetSpin, 1f - Mathf.Exp(-3f * Time.deltaTime));
            if (discoBall != null) discoBall.Rotate(0f, spin * Time.deltaTime, 0f, Space.Self);

            SetFloat(discoRenderer, SparkleId, Mathf.Max(level, longNote));
            SetFloat(edgeRing, IntensityId, Mathf.Lerp(edgeRingIntensity.x, edgeRingIntensity.y, level));
        }

        public override void PlayFinale()
        {
            base.PlayFinale();
            foreach (LaserBeam laser in lasers) laser.Burst();
        }

        void SetFloat(Renderer target, int id, float value)
        {
            if (target == null) return;
            target.GetPropertyBlock(block);
            block.SetFloat(id, value);
            target.SetPropertyBlock(block);
        }
    }
}
