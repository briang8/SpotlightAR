using UnityEngine;

namespace SpotlightAR.Stages
{
    /// <summary>
    /// Confetti that bursts up over the stage and flutters down, spinning and swaying.
    /// Each piece gets its own colour through a MaterialPropertyBlock, so all pieces
    /// share one material.
    /// </summary>
    public sealed class ConfettiBurst : MonoBehaviour
    {
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Leave empty to use every child as a confetti piece.")]
        [SerializeField] Transform[] pieces;
        [SerializeField] Color[] palette =
        {
            new Color(1f, 0.71f, 0.28f), new Color(1f, 0.36f, 0.78f),
            new Color(0.4f, 0.85f, 1f), new Color(0.43f, 0.91f, 0.63f), Color.white
        };
        [SerializeField] float launchSpeed = 1.1f;
        [SerializeField] float gravity = 1.6f;
        [SerializeField] float drag = 2.2f;
        [SerializeField] float lifetime = 2.6f;
        [SerializeField] float size = 0.028f;

        Vector3[] velocities;
        Vector3[] spins;
        float[] sway;
        float age = -1f;

        void Awake()
        {
            if (pieces == null || pieces.Length == 0)
            {
                pieces = new Transform[transform.childCount];
                for (int i = 0; i < pieces.Length; i++) pieces[i] = transform.GetChild(i);
            }
            velocities = new Vector3[pieces.Length];
            spins = new Vector3[pieces.Length];
            sway = new float[pieces.Length];

            var block = new MaterialPropertyBlock();
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i].TryGetComponent(out Renderer piece))
                {
                    block.SetColor(ColorId, palette[i % palette.Length]);
                    piece.SetPropertyBlock(block);
                }
                pieces[i].gameObject.SetActive(false);
            }
        }

        public void Burst()
        {
            age = 0f;
            for (int i = 0; i < pieces.Length; i++)
            {
                Vector2 spread = Random.insideUnitCircle * 0.6f;
                velocities[i] = new Vector3(spread.x, 1f, spread.y).normalized * launchSpeed * Random.Range(0.7f, 1.2f);
                spins[i] = Random.insideUnitSphere * 720f;
                sway[i] = Random.value * 10f;
                pieces[i].localPosition = Vector3.zero;
                pieces[i].localRotation = Random.rotation;
                pieces[i].localScale = Vector3.one * size;
                pieces[i].gameObject.SetActive(true);
            }
        }

        void Update()
        {
            if (age < 0f) return;
            float dt = Time.deltaTime;
            age += dt;

            if (age >= lifetime)
            {
                age = -1f;
                foreach (Transform piece in pieces) piece.gameObject.SetActive(false);
                return;
            }

            float fade = 1f - Mathf.Clamp01((age - lifetime * 0.7f) / (lifetime * 0.3f));
            for (int i = 0; i < pieces.Length; i++)
            {
                // Air drag makes paper confetti drift instead of dropping like a stone.
                velocities[i].y -= gravity * dt;
                velocities[i] *= 1f - Mathf.Clamp01(drag * dt);
                Vector3 flutter = new Vector3(Mathf.Sin(age * 5f + sway[i]), 0f, Mathf.Cos(age * 4f + sway[i])) * 0.05f;
                pieces[i].localPosition += (velocities[i] + flutter) * dt;
                pieces[i].localRotation *= Quaternion.Euler(spins[i] * dt);
                pieces[i].localScale = Vector3.one * (size * fade);
            }
        }
    }
}
