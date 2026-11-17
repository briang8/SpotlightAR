using TMPro;
using UnityEngine;

namespace SpotlightAR.Stages
{
    /// <summary>
    /// A neon sign above the stage showing the name the player chose for their stage.
    /// Uses TextMeshPro's signed-distance-field glow, so the text stays sharp at any
    /// stage size and glows without extra lights or textures.
    /// </summary>
    public sealed class NameSign : MonoBehaviour
    {
        [SerializeField] TMP_FontAsset font;
        [Tooltip("Width and height of the text area in sign units.")]
        [SerializeField] Vector2 textArea = new Vector2(0.62f, 0.13f);
        [SerializeField] Color glowColor = new Color(1f, 0.36f, 0.78f);
        [SerializeField, Range(0f, 1f)] float glowPower = 0.55f;
        [Tooltip("Small random dips in brightness, like real neon.")]
        [SerializeField, Range(0f, 1f)] float flicker = 0.08f;

        TextMeshPro text;
        TextMeshPro backText;
        Material material;
        float nextFlicker;

        void Awake()
        {
            // Double-sided: the crowd reads it from the front, and the player standing behind
            // the stage (performer view) reads it from the back. Both faces share one material.
            text = CreateText("Neon Text (crowd side)", 0.012f, 180f);
            material = text.fontMaterial; // per-sign instance
            material.EnableKeyword(ShaderUtilities.Keyword_Glow);
            backText = CreateText("Neon Text (performer side)", -0.012f, 0f);
            backText.fontSharedMaterial = material;
            ApplyColor();
        }

        TextMeshPro CreateText(string objectName, float z, float yaw)
        {
            var textObject = new GameObject(objectName);
            textObject.transform.SetParent(transform, false);
            textObject.transform.localPosition = new Vector3(0f, 0f, z);
            textObject.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            var label = textObject.AddComponent<TextMeshPro>();
            if (font != null) label.font = font;
            label.rectTransform.sizeDelta = textArea;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.05f;
            label.fontSizeMax = 1.2f;
            label.fontStyle = FontStyles.UpperCase;
            label.characterSpacing = 4f;
            return label;
        }

        public void SetText(string value)
        {
            string shown = string.IsNullOrWhiteSpace(value) ? "Spotlight" : value.Trim();
            text.text = shown;
            backText.text = shown;
        }

        public void SetColor(Color color)
        {
            glowColor = color;
            if (material != null) ApplyColor();
        }

        void ApplyColor()
        {
            text.color = Color.Lerp(glowColor, Color.white, 0.65f); // hot white core like a neon tube
            if (backText != null) backText.color = text.color;
            material.SetColor(ShaderUtilities.ID_GlowColor, glowColor);
            material.SetFloat(ShaderUtilities.ID_GlowPower, glowPower);
            material.SetFloat(ShaderUtilities.ID_GlowOuter, 0.7f);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.12f);
            material.SetColor(ShaderUtilities.ID_OutlineColor, glowColor);
        }

        void Update()
        {
            if (flicker <= 0f || Time.time < nextFlicker) return;
            bool dip = Random.value < 0.15f;
            material.SetFloat(ShaderUtilities.ID_GlowPower, dip ? glowPower * (1f - flicker * 4f) : glowPower);
            nextFlicker = Time.time + (dip ? 0.06f : Random.Range(0.3f, 1.5f));
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
