using System.Collections.Generic;
using UnityEngine;

namespace SpotlightAR.Stages
{
    /// <summary>
    /// One fan in the crowd: a rigged, animated person (CC0 Quaternius models) picked from a
    /// list, sized to the stage and given a skin tone. The idle animation comes from the
    /// model; the reaction to the singer is layered on top in code so every fan reacts to
    /// the same live energy: a bounce, hands in the air when it gets loud, a wave and a
    /// jump on a cheer, and phone torches at x3 hype.
    /// </summary>
    public sealed class CrowdMember : MonoBehaviour
    {
        [Tooltip("The body is spawned under this transform; it bounces and jumps.")]
        [SerializeField] Transform figure;

        [Header("Body (picked per fan from its place in the crowd, so it never changes)")]
        [SerializeField] GameObject[] bodies;
        [Tooltip("Height of a fan in stage units (a 1 m stage).")]
        [SerializeField] float height = 0.28f;
        [Tooltip("Skin tones swapped onto the model's skin materials.")]
        [SerializeField] Color[] skinTones =
        {
            new Color(0.36f, 0.22f, 0.14f), new Color(0.25f, 0.15f, 0.09f), new Color(0.48f, 0.30f, 0.18f),
            new Color(0.62f, 0.42f, 0.27f), new Color(0.76f, 0.55f, 0.38f), new Color(0.88f, 0.68f, 0.55f),
        };

        [Header("Motion")]
        [SerializeField] Vector2 bounceHeight = new Vector2(0.002f, 0.02f);
        [SerializeField] Vector2 bounceSpeed = new Vector2(1.2f, 2.6f);
        [SerializeField] float cheerHeight = 0.05f;
        [Tooltip("Energy at which hands start going up.")]
        [SerializeField, Range(0f, 1f)] float armsUpThreshold = 0.55f;
        [Tooltip("Shown in the raised hand at x3 hype, like phone torches at a concert.")]
        [SerializeField] GameObject phoneLight;

        [Header("Facing")]
        [Tooltip("How much the fan turns toward the phone (0 = faces the stage, 1 = faces the phone).")]
        [SerializeField, Range(0f, 1f)] float faceCamera = 0.75f;
        [SerializeField] float turnSpeed = 3f;

        // One material per (source material, tone), shared by every fan, so the crowd stays batchable.
        static readonly Dictionary<(Material, int), Material> toned = new Dictionary<(Material, int), Material>();

        Animation clips;
        string idleClip, waveClip;
        Transform upperArmL, lowerArmL, upperArmR, lowerArmR, wristR;

        Vector3 figureRest;
        Quaternion restRotation;
        Transform viewer;
        float energy, phase, cheer, armsUp;
        bool phoneShown;

        void Awake()
        {
            if (figure == null) figure = transform;
            figureRest = figure.localPosition;
            restRotation = transform.localRotation;
            phase = Random.value * Mathf.PI * 2f;
            if (Camera.main != null) viewer = Camera.main.transform;
            SpawnBody();
            if (phoneLight != null) phoneLight.SetActive(false);
        }

        void SpawnBody()
        {
            if (bodies == null || bodies.Length == 0) return;
            Vector3 p = transform.localPosition;
            var random = new System.Random(Mathf.RoundToInt(p.x * 1000f) * 73856093 ^ Mathf.RoundToInt(p.z * 1000f) * 19349663);
            GameObject prefab = bodies[random.Next(bodies.Length)];
            if (prefab == null) return;

            GameObject body = Instantiate(prefab, figure, false);
            body.name = "Body";
            FitToHeight(body.transform);
            ApplySkinTone(body, random.Next(skinTones.Length));
            FindBones(body.transform);

            clips = body.GetComponentInChildren<Animation>();
            if (clips != null)
            {
                clips.cullingType = AnimationCullingType.AlwaysAnimate;
                idleClip = FindClip("Idle");
                waveClip = FindClip("Wave");
                if (idleClip != null)
                {
                    AnimationState idle = clips[idleClip];
                    idle.wrapMode = WrapMode.Loop;
                    idle.speed = 0.85f + (float)random.NextDouble() * 0.3f;
                    idle.normalizedTime = (float)random.NextDouble();
                    clips.Play(idleClip);
                }
            }

            foreach (SkinnedMeshRenderer skin in body.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skin.quality = SkinQuality.Bone2; // cheaper skinning, no visible difference at this size
                skin.skinnedMotionVectors = false;
            }
        }

        /// <summary>Scales the model so the fan is <see cref="height"/> tall with feet on the ground.</summary>
        void FitToHeight(Transform body)
        {
            Bounds bounds = WorldBounds(body);
            float worldHeight = bounds.size.y;
            if (worldHeight < 1e-5f) return;
            float wanted = height * figure.lossyScale.y;
            body.localScale *= wanted / worldHeight;

            bounds = WorldBounds(body);
            body.position += Vector3.up * (figure.position.y - bounds.min.y);
        }

        static Bounds WorldBounds(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(root.position, Vector3.zero);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        void ApplySkinTone(GameObject body, int tone)
        {
            if (skinTones == null || skinTones.Length == 0) return;
            foreach (Renderer part in body.GetComponentsInChildren<Renderer>())
            {
                Material[] materials = part.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null || !source.name.StartsWith("Skin")) continue;
                    materials[i] = Toned(source, tone);
                    changed = true;
                }
                if (changed) part.sharedMaterials = materials;
            }
        }

        Material Toned(Material source, int tone)
        {
            if (toned.TryGetValue((source, tone), out Material material) && material != null) return material;
            // "Skin_Darker" (lips, shading) stays a touch darker than the main skin.
            Color color = skinTones[tone] * (source.name.Contains("Dark") ? 0.82f : 1f);
            color.a = 1f;
            material = new Material(source) { name = source.name + " (tone " + tone + ")" };
            if (material.HasProperty("baseColorFactor")) material.SetColor("baseColorFactor", color);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            toned[(source, tone)] = material;
            return material;
        }

        void FindBones(Transform root)
        {
            foreach (Transform bone in root.GetComponentsInChildren<Transform>())
            {
                switch (bone.name)
                {
                    case "UpperArm.L": upperArmL = bone; break;
                    case "LowerArm.L": lowerArmL = bone; break;
                    case "UpperArm.R": upperArmR = bone; break;
                    case "LowerArm.R": lowerArmR = bone; break;
                    case "Wrist.R": wristR = bone; break;
                }
            }
        }

        /// <summary>Clip names come in as "Armature|Idle"; match on the part after the bar.</summary>
        string FindClip(string wanted)
        {
            foreach (AnimationState state in clips)
            {
                string name = state.name;
                int bar = name.LastIndexOf('|');
                if ((bar >= 0 ? name.Substring(bar + 1) : name) == wanted) return name;
            }
            return null;
        }

        public void SetEnergy(float value) => energy = Mathf.Clamp01(value);

        /// <summary>At x3 the fans hold up phone lights.</summary>
        public void SetHype(int hype) => phoneShown = hype >= 3;

        public void Cheer()
        {
            cheer = 1f;
            if (clips == null || waveClip == null || idleClip == null) return;
            clips.CrossFade(waveClip, 0.2f);
            clips.CrossFadeQueued(idleClip, 0.3f, QueueMode.CompleteOthers);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            phase += dt * Mathf.Lerp(bounceSpeed.x, bounceSpeed.y, energy) * Mathf.PI;
            cheer = Mathf.MoveTowards(cheer, 0f, dt * 1.4f);

            float bounce = Mathf.Abs(Mathf.Sin(phase)) * Mathf.Lerp(bounceHeight.x, bounceHeight.y, energy * energy);
            float jump = Mathf.Sin(cheer * Mathf.PI) * cheerHeight;
            figure.localPosition = figureRest + Vector3.up * (bounce + jump);

            float armsTarget = Mathf.InverseLerp(armsUpThreshold, 1f, energy);
            if (phoneShown) armsTarget = Mathf.Max(armsTarget, 0.8f);
            armsUp = Mathf.Lerp(armsUp, armsTarget, 1f - Mathf.Exp(-6f * dt));

            TurnTowardViewer(dt);
        }

        // After the animation has posed the skeleton: lift the arms on top of it.
        void LateUpdate()
        {
            if (armsUp > 0.01f)
            {
                float sway = Mathf.Sin(phase * 0.5f) * 0.35f;
                RaiseArm(upperArmL, lowerArmL, -1f, sway);
                RaiseArm(upperArmR, lowerArmR, 1f, -sway);
            }

            if (phoneLight == null) return;
            bool show = phoneShown && wristR != null;
            if (phoneLight.activeSelf != show) phoneLight.SetActive(show);
            if (!show) return;
            // Held upright in the raised hand, screen toward the fan, torch toward the stage.
            phoneLight.transform.SetPositionAndRotation(
                wristR.position + figure.up * (0.016f * figure.lossyScale.y),
                Quaternion.AngleAxis(Mathf.Sin(phase * 0.5f) * 10f, figure.forward) * figure.rotation);
        }

        /// <summary>
        /// Points the arm up and a little outward. Works from the bone positions alone, so it
        /// doesn't depend on how the rig's bone axes are set up.
        /// </summary>
        void RaiseArm(Transform upper, Transform lower, float side, float sway)
        {
            if (upper == null || lower == null) return;
            Vector3 target = (figure.up + figure.right * side * (0.3f + sway * 0.4f)).normalized;

            Vector3 upperDir = lower.position - upper.position;
            Quaternion upperTurn = Quaternion.FromToRotation(upperDir, target);
            upper.rotation = Quaternion.Slerp(upper.rotation, upperTurn * upper.rotation, armsUp);

            if (lower.childCount == 0) return;
            Vector3 lowerDir = lower.GetChild(0).position - lower.position;
            Quaternion lowerTurn = Quaternion.FromToRotation(lowerDir, target);
            lower.rotation = Quaternion.Slerp(lower.rotation, lowerTurn * lower.rotation, armsUp);
        }

        /// <summary>Fans turn toward the phone as the player walks around the stage.</summary>
        void TurnTowardViewer(float dt)
        {
            if (viewer == null || faceCamera <= 0f || transform.parent == null) return;

            Vector3 toViewer = transform.parent.InverseTransformPoint(viewer.position) - transform.localPosition;
            toViewer.y = 0f;
            if (toViewer.sqrMagnitude < 1e-6f) return;

            Quaternion facing = Quaternion.LookRotation(toViewer.normalized, Vector3.up);
            Quaternion target = Quaternion.Slerp(restRotation, facing, faceCamera);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, target, 1f - Mathf.Exp(-turnSpeed * dt));
        }
    }
}
