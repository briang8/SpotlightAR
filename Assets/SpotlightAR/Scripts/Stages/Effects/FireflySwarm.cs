using UnityEngine;

namespace SpotlightAR.Stages
{
    /// <summary>
    /// Fireflies that drift up from the rooftop deck. The longer the singer holds a note,
    /// the more of them rise.
    /// </summary>
    public sealed class FireflySwarm : MonoBehaviour
    {
        [Tooltip("Leave empty to use every child as a firefly.")]
        [SerializeField] Transform[] fireflies;
        [SerializeField] float radius = 0.45f;
        [SerializeField] float riseHeight = 0.7f;
        [SerializeField] float riseSpeed = 0.18f;
        [SerializeField] float size = 0.03f;

        Vector3[] origins;
        float[] life;
        float activity;
        float boostUntil;

        void Awake()
        {
            if (fireflies == null || fireflies.Length == 0)
            {
                fireflies = new Transform[transform.childCount];
                for (int i = 0; i < fireflies.Length; i++) fireflies[i] = transform.GetChild(i);
            }
            origins = new Vector3[fireflies.Length];
            life = new float[fireflies.Length];
            for (int i = 0; i < fireflies.Length; i++)
            {
                life[i] = -1f;
                fireflies[i].gameObject.SetActive(false);
            }
        }

        /// <summary>0 = none rising, 1 = a steady stream.</summary>
        public void SetActivity(float value) => activity = Mathf.Clamp01(value);

        /// <summary>Release a big swarm for a few seconds (finale).</summary>
        public void Celebrate(float seconds = 3f) => boostUntil = Time.time + seconds;

        void Update()
        {
            float dt = Time.deltaTime;
            float spawnChance = (Time.time < boostUntil ? 1f : activity) * 2.5f * dt;

            for (int i = 0; i < fireflies.Length; i++)
            {
                if (life[i] < 0f)
                {
                    if (Random.value < spawnChance) Spawn(i);
                    continue;
                }

                life[i] += dt * riseSpeed / riseHeight;
                if (life[i] >= 1f)
                {
                    life[i] = -1f;
                    fireflies[i].gameObject.SetActive(false);
                    continue;
                }

                float t = Time.time;
                Vector3 wobble = new Vector3(Mathf.Sin(t * 3f + i), 0f, Mathf.Cos(t * 2.3f + i * 2f)) * 0.03f;
                fireflies[i].localPosition = origins[i] + Vector3.up * (life[i] * riseHeight) + wobble;
                fireflies[i].localScale = Vector3.one * (size * Mathf.Sin(life[i] * Mathf.PI));
            }
        }

        void Spawn(int index)
        {
            Vector2 point = Random.insideUnitCircle * radius;
            origins[index] = new Vector3(point.x, 0.06f, point.y);
            life[index] = 0f;
            fireflies[index].localScale = Vector3.zero;
            fireflies[index].gameObject.SetActive(true);
        }
    }
}
