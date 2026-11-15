using UnityEngine;

namespace SpotlightAR.AR
{
    /// <summary>
    /// The dashed ring on the floor that shows where the stage will go.
    /// It glides to the latest surface hit and gently pulses so it reads as "live".
    /// </summary>
    public sealed class PlacementReticle : MonoBehaviour
    {
        [SerializeField] Transform visual;
        [SerializeField] float followSpeed = 18f;
        [SerializeField] float pulseAmount = 0.04f;
        [SerializeField] float pulseSpeed = 3f;

        Vector3 targetPosition;
        Quaternion targetRotation = Quaternion.identity;
        float targetSize = 1f;
        bool visible;

        void Awake()
        {
            if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
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
