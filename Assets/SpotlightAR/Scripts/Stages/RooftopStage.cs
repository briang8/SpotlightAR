using UnityEngine;

namespace SpotlightAR.Stages
{
    /// <summary>
    /// Rooftop: a calm night set on a wooden deck with a city skyline behind.
    /// String lights glow brighter as you sing and fireflies drift up on long notes.
    /// </summary>
    public sealed class RooftopStage : Stage
    {
        [Header("Rooftop")]
        [SerializeField] StringLights stringLights;
        [SerializeField] FireflySwarm fireflies;
        [Tooltip("Seconds of held note for the full firefly stream.")]
        [SerializeField] float longNoteSeconds = 1.2f;

        protected override void OnReact(float level)
        {
            if (stringLights != null) stringLights.SetLevel(Mathf.Clamp01(level + (Hype - 1) * 0.15f));
            if (fireflies != null) fireflies.SetActivity(Mathf.Clamp01(Sustain / longNoteSeconds));
        }

        public override void OnMilestone()
        {
            base.OnMilestone();
            if (fireflies != null) fireflies.Celebrate(1.5f);
        }

        public override void PlayFinale()
        {
            base.PlayFinale();
            if (fireflies != null) fireflies.Celebrate(4f);
        }
    }
}
