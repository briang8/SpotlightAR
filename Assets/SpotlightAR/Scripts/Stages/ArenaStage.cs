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

        [Tooltip("Used when no cones are set: the whole cabinets under \"Speakers\" thump gently instead.")]
        [SerializeField] float cabinetPulse = 0.035f;

        Vector3[] coneScales;
        bool wholeCabinets;

        protected override void Awake()
        {
            base.Awake();
            if (sparks == null || sparks.Length == 0) sparks = GetComponentsInChildren<SparkBurst>(true);

            speakerCones = System.Array.FindAll(speakerCones ?? new Transform[0], cone => cone != null);
            if (speakerCones.Length == 0)
            {
                Transform cabinets = transform.Find("Speakers");
                if (cabinets != null)
                {
                    speakerCones = new Transform[cabinets.childCount];
                    for (int i = 0; i < cabinets.childCount; i++) speakerCones[i] = cabinets.GetChild(i);
                    wholeCabinets = true;
                }
            }
            coneScales = new Vector3[speakerCones.Length];
            for (int i = 0; i < speakerCones.Length; i++) coneScales[i] = speakerCones[i].localScale;
        }

        protected override void OnReact(float level)
        {
            float beat = Mathf.Max(0f, Mathf.Sin(Time.time * pulseSpeed));
            float thump = 1f + beat * (wholeCabinets ? cabinetPulse : speakerPulse) * level;
            for (int i = 0; i < speakerCones.Length; i++)
            {
                Vector3 rest = coneScales[i];
                speakerCones[i].localScale = wholeCabinets
                    ? rest * thump
                    : new Vector3(rest.x * thump, rest.y, rest.z * thump);
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
