using UnityEngine;

namespace SpotlightAR.AR
{
    /// <summary>
    /// The dashed ring on the floor that shows where the stage will go.
    /// It glides to the latest surface hit and gently pulses so it reads as "live", and turns
    /// coral when the stage is too big for the floor that was found.
    /// </summary>
    public sealed class PlacementReticle : MonoBehaviour
    {
        [SerializeField] Transform visual;
        [SerializeField] float followSpeed = 18f;
        [SerializeField] float pulseAmount = 0.04f;
        [SerializeField] float pulseSpeed = 3f;
        [SerializeField] Color okColor = new Color(1f, 0.97f, 0.92f, 1f);
        [SerializeField] Color tooBigColor = new Color(1f, 0.42f, 0.3f, 1f);

        static readonly int ColorId = Shader.PropertyToID("_Color");
        MaterialPropertyBlock block;
        Renderer[] renderers;

        Vector3 targetPosition;
        Quaternion targetRotation = Quaternion.identity;
        float targetSize = 1f;
        bool visible;

        void Awake()
        {
            if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
            renderers = GetComponentsInChildren<Renderer>(true);
            block = new MaterialPropertyBlock();
            SetTooBig(false);
            SetVisible(false);
        }

        /// <summary>Move the reticle to a surface pose, sized to the stage diameter.</summary>
        public void Show(Pose pose, float size)
        {
            targetPosition = pose.position;
            targetRotation = pose.rotation;
            targetSize = size;

            if (!visible)
            {
                transform.SetPositionAndRotation(targetPosition, targetRotation);
                SetVisible(true);
            }
        }

        public void Hide() => SetVisible(false);

        /// <summary>Coral ring when the stage won't fit on the surface found so far.</summary>
        public void SetTooBig(bool tooBig)
        {
            block.SetColor(ColorId, tooBig ? tooBigColor : okColor);
            foreach (Renderer ring in renderers) ring.SetPropertyBlock(block);
        }

        void SetVisible(bool value)
        {
            visible = value;
            if (visual != null) visual.gameObject.SetActive(value);
        }

        void Update()
        {
            if (!visible) return;
            float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, targetPosition, t),
                Quaternion.Slerp(transform.rotation, targetRotation, t));

            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            transform.localScale = Vector3.one * (targetSize * pulse);
        }
    }
}
