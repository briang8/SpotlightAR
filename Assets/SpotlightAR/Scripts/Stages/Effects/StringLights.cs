using UnityEngine;

namespace SpotlightAR.Stages
{
    /// <summary>
    /// Rooftop string lights: a sagging wire through a row of glowing bulbs that get
    /// brighter as the singer gets louder, with a gentle twinkle.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class StringLights : MonoBehaviour
    {
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        [Tooltip("Leave empty to use every renderer under this object.")]
        [SerializeField] Renderer[] bulbs;
        [SerializeField] Vector2 intensity = new Vector2(0.35f, 1.5f);
        [SerializeField, Range(0f, 0.5f)] float twinkle = 0.15f;
        [SerializeField] float wireWidth = 0.004f;

        LineRenderer wire;
        MaterialPropertyBlock block;
        float level;

        void Awake()
        {
            if (bulbs == null || bulbs.Length == 0) bulbs = GetComponentsInChildren<MeshRenderer>(true);
            block = new MaterialPropertyBlock();

            // The wire runs through the bulbs, which are already placed along the sag.
            wire = GetComponent<LineRenderer>();
            wire.useWorldSpace = false;
            wire.positionCount = bulbs.Length;
            for (int i = 0; i < bulbs.Length; i++) wire.SetPosition(i, bulbs[i].transform.localPosition);
        }

        public void SetLevel(float value) => level = Mathf.Clamp01(value);

        void Update()
        {
            wire.widthMultiplier = wireWidth * transform.lossyScale.x;
            float time = Time.time;
            float baseIntensity = Mathf.Lerp(intensity.x, intensity.y, level);

            for (int i = 0; i < bulbs.Length; i++)
            {
                float flicker = 1f + Mathf.Sin(time * 2.1f + i * 1.7f) * twinkle;
                bulbs[i].GetPropertyBlock(block);
                block.SetFloat(IntensityId, baseIntensity * flicker);
                bulbs[i].SetPropertyBlock(block);
            }
        }
    }
}
