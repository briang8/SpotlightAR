using UnityEngine;

namespace SpotlightAR.Stages
{
    /// <summary>
    /// A burst of glowing sparks fired at score milestones. The sparks are a handful of
    /// camera-facing glow quads moved by a tiny ballistic simulation: far cheaper than a
    /// particle system for a few dozen sparks, and it scales with the stage automatically.
    /// </summary>
    public sealed class SparkBurst : MonoBehaviour
    {
        [Tooltip("Leave empty to use every child as a spark.")]
        [SerializeField] Transform[] sparks;
        [SerializeField] float speed = 0.9f;
        [SerializeField] float gravity = 2.2f;
        [SerializeField] float lifetime = 1.1f;
        [SerializeField] float size = 0.035f;

        Vector3[] velocities;
        float age = -1f;

        void Awake()
        {
            if (sparks == null || sparks.Length == 0)
            {
                sparks = new Transform[transform.childCount];
                for (int i = 0; i < sparks.Length; i++) sparks[i] = transform.GetChild(i);
            }
            velocities = new Vector3[sparks.Length];
            foreach (Transform spark in sparks) spark.gameObject.SetActive(false);
        }

        public void Burst()
        {
            age = 0f;
            for (int i = 0; i < sparks.Length; i++)
            {
                Vector3 direction = Random.insideUnitSphere;
                direction.y = Mathf.Abs(direction.y) + 0.6f;
                velocities[i] = direction.normalized * speed * Random.Range(0.6f, 1.1f);
                sparks[i].localPosition = Vector3.zero;
                sparks[i].gameObject.SetActive(true);
            }
        }

        void Update()
        {
            if (age < 0f) return;
            float dt = Time.deltaTime;
            age += dt;
            float t = age / lifetime;

            if (t >= 1f)
            {
                age = -1f;
                foreach (Transform spark in sparks) spark.gameObject.SetActive(false);
                return;
            }

            for (int i = 0; i < sparks.Length; i++)
            {
                velocities[i].y -= gravity * dt;
                sparks[i].localPosition += velocities[i] * dt;
                sparks[i].localScale = Vector3.one * (size * (1f - t));
            }
        }
    }
}
