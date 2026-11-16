using UnityEngine;

namespace SpotlightAR.Stages
{
    /// <summary>
    /// Arena: a big concert stage with a lighting truss, speaker stacks and a full crowd.
    /// The speakers thump with the voice, and sparks fire at score milestones and at x3 hype.
    /// </summary>
    public sealed class ArenaStage : Stage
    {
        [Header("Arena")]
        [SerializeField] SparkBurst[] sparks;
        [Tooltip("Speaker cones that pulse with the singer's voice.")]
        [SerializeField] Transform[] speakerCones;
        [SerializeField] float speakerPulse = 0.12f;
        [SerializeField] float pulseSpeed = 9f;

        Vector3[] coneScales;

        protected override void Awake()
        {
            base.Awake();
            if (sparks == null || sparks.Length == 0) sparks = GetComponentsInChildren<SparkBurst>(true);
            coneScales = new Vector3[speakerCones.Length];
            for (int i = 0; i < speakerCones.Length; i++) coneScales[i] = speakerCones[i].localScale;
        }

        protected override void OnReact(float level)
        {
            float thump = 1f + Mathf.Max(0f, Mathf.Sin(Time.time * pulseSpeed)) * speakerPulse * level;
            for (int i = 0; i < speakerCones.Length; i++)
            {
                Vector3 rest = coneScales[i];
                speakerCones[i].localScale = new Vector3(rest.x * thump, rest.y, rest.z * thump);
            }
        }

        public override void OnMilestone()
        {
            base.OnMilestone();
            FireSparks();
        }

        protected override void OnHypeChanged(int hype)
        {
            if (hype == 3) FireSparks();
        }

        public override void PlayFinale()
        {
            base.PlayFinale();
            FireSparks();
        }

        void FireSparks()
        {
            foreach (SparkBurst burst in sparks) burst.Burst();
        }
    }
}
