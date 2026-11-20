using UnityEngine;

namespace SpotlightAR.Stages
{
    /// <summary>
    /// Base class for every stage theme. It owns the shared rig (spotlights, light pool, crowd)
    /// and turns the player's loudness into a smoothed "show level". Each theme adds its own
    /// effects by overriding <see cref="OnReact"/> (Template Method pattern).
    /// </summary>
    public abstract class Stage : MonoBehaviour
    {
        static readonly int GlowColorId = Shader.PropertyToID("_StageGlowColor");
        static readonly int GlowLevelId = Shader.PropertyToID("_StageGlowLevel");
        static readonly int GlowCenterId = Shader.PropertyToID("_StageGlowCenter");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        [SerializeField] string displayName = "Stage";

        [Header("Shared rig")]
        [Tooltip("Leave empty to use every StageLight under this stage.")]
        [SerializeField] protected StageLight[] lights;
        [Tooltip("Leave empty to use the CrowdController under this stage.")]
        [SerializeField] protected CrowdController crowd;
        [Tooltip("Where the spotlights point (usually the centre of the deck).")]
        [SerializeField] protected Transform focusPoint;
        [Tooltip("Additive glow on the deck under the spotlights.")]
        [SerializeField] protected Renderer lightPool;

        [Header("Stage glow (fake lighting on stage materials)")]
        [SerializeField] Color glowColor = new Color(1f, 0.71f, 0.28f);
        [Tooltip("Radius of the glow around the stage centre, in stage units.")]
        [SerializeField] float glowRadius = 1.2f;

        [Header("Response")]
        [Tooltip("Show level when nobody is singing, so the stage never looks dead.")]
        [SerializeField, Range(0f, 0.5f)] float idleLevel = 0.12f;
        [Tooltip("How fast the show follows the voice.")]
        [SerializeField] float responsiveness = 8f;
        [Tooltip("Loudness above which a note counts as held (for long-note effects).")]
        [SerializeField, Range(0f, 1f)] float sustainThreshold = 0.35f;
        [SerializeField] Vector2 poolIntensity = new Vector2(0.15f, 1.1f);

        [Header("Moments")]
        [Tooltip("Confetti fired when hype reaches x3 and at the finale.")]
        [SerializeField] ConfettiBurst confettiPrefab;
        [SerializeField] Vector3 confettiPosition = new Vector3(0f, 0.9f, 0.1f);

        [Header("Name sign")]
        [SerializeField] NameSign nameSignPrefab;
        [Tooltip("Where the sign hangs, in stage units.")]
        [SerializeField] Vector3 signPosition = new Vector3(0f, 1.2f, -0.35f);

        MaterialPropertyBlock poolBlock;
        ConfettiBurst confetti;
        NameSign sign;

        public string DisplayName => displayName;

        /// <summary>Smoothed show level, 0..1.</summary>
        protected float Level { get; private set; }

        /// <summary>Seconds the current note has been held above the sustain threshold.</summary>
        protected float Sustain { get; private set; }

        protected virtual void Awake()
        {
            if (lights == null || lights.Length == 0) lights = GetComponentsInChildren<StageLight>(true);
            if (crowd == null) crowd = GetComponentInChildren<CrowdController>(true);

            Transform target = focusPoint != null ? focusPoint : transform;
            foreach (StageLight stageLight in lights) stageLight.AimAt(target);

            poolBlock = new MaterialPropertyBlock();
            Level = idleLevel;

            if (confettiPrefab != null)
            {
                confetti = Instantiate(confettiPrefab, transform);
                confetti.transform.localPosition = confettiPosition;
            }
            if (nameSignPrefab != null)
            {
                sign = Instantiate(nameSignPrefab, transform);
                sign.transform.localPosition = signPosition;
                sign.SetColor(glowColor);
            }
        }

        /// <summary>Changes the neon sign's glow colour (the Stages tab setting).</summary>
        public void SetSignColor(Color color)
        {
            if (sign != null) sign.SetColor(color);
        }

        /// <summary>Shows the player's chosen stage name on the neon sign.</summary>
        public void SetStageName(string stageName)
        {
            if (sign != null) sign.SetText(stageName);
        }

        protected virtual void Start() { }

        protected virtual void OnDisable()
        {
            Shader.SetGlobalFloat(GlowLevelId, 0f);
        }

        /// <summary>Feed the player's loudness (0..1) into the show. Call once per frame.</summary>
        public void ReactTo(float loudness)
        {
            float dt = Time.deltaTime;
            float target = Mathf.Max(idleLevel, Mathf.Clamp01(loudness));
            Level = Mathf.Lerp(Level, target, 1f - Mathf.Exp(-responsiveness * dt));
            Sustain = loudness > sustainThreshold ? Sustain + dt : Mathf.Max(0f, Sustain - dt * 3f);

            foreach (StageLight stageLight in lights) stageLight.SetLevel(Level);
            if (crowd != null) crowd.SetEnergy(Level);
            UpdateGlow();
            OnReact(Level);
        }

        /// <summary>Current crowd hype multiplier (1 to 3).</summary>
        protected int Hype { get; private set; } = 1;

        /// <summary>Set the crowd hype multiplier (1 to 3). Themes react in <see cref="OnHypeChanged"/>.</summary>
        public void SetHype(int multiplier)
        {
            int clamped = Mathf.Clamp(multiplier, 1, 3);
            if (clamped == Hype) return;
            bool rising = clamped > Hype;
            Hype = clamped;
            if (crowd != null) crowd.SetHype(clamped);
            if (rising) foreach (StageLight stageLight in lights) stageLight.Flash();
            if (rising && clamped == 3)
            {
                // The big hype moment: confetti and the whole crowd jumps.
                if (confetti != null) confetti.Burst();
                if (crowd != null) crowd.Cheer();
            }
            OnHypeChanged(clamped);
        }

        /// <summary>The score passed a milestone: the crowd cheers and the theme can add its own effect.</summary>
        public virtual void OnMilestone()
        {
            if (crowd != null) crowd.Cheer();
            foreach (StageLight stageLight in lights) stageLight.Flash();
        }

        /// <summary>Called when hype changes level. Override to scale up a theme's effects.</summary>
        protected virtual void OnHypeChanged(int hype) { }

        /// <summary>The end-of-song moment: every light flashes and the crowd cheers.</summary>
        public virtual void PlayFinale()
        {
            foreach (StageLight stageLight in lights) stageLight.Flash();
            if (crowd != null) crowd.Cheer();
            if (confetti != null) confetti.Burst();
        }

        /// <summary>Theme-specific effects (lasers, sparks, string lights...).</summary>
        protected abstract void OnReact(float level);

        void UpdateGlow()
        {
            // One set of global shader values lights every stage material at once,
            // which replaces real-time lights (disabled on the mobile URP asset for speed).
            Vector3 centre = transform.position;
            Shader.SetGlobalColor(GlowColorId, glowColor);
            Shader.SetGlobalFloat(GlowLevelId, Level);
            Shader.SetGlobalVector(GlowCenterId, new Vector4(centre.x, centre.y, centre.z, glowRadius * transform.lossyScale.x));

            if (lightPool == null) return;
            lightPool.GetPropertyBlock(poolBlock);
            poolBlock.SetFloat(IntensityId, Mathf.Lerp(poolIntensity.x, poolIntensity.y, Level));
            lightPool.SetPropertyBlock(poolBlock);
        }
    }
}
