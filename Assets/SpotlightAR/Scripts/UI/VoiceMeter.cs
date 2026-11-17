using UnityEngine;
using UnityEngine.UIElements;

namespace SpotlightAR.UI
{
    /// <summary>
    /// The scrolling "Your voice" bar meter from the singing screen mockup.
    /// Newest level appears on the right; loud bars turn amber.
    /// Usage in UXML: &lt;SpotlightAR.UI.VoiceMeter bar-count="28" /&gt;
    /// </summary>
    [UxmlElement]
    public partial class VoiceMeter : VisualElement
    {
        float[] levels = new float[28];
        int head;

        [UxmlAttribute]
        public int BarCount
        {
            get => levels.Length;
            set
            {
                levels = new float[Mathf.Clamp(value, 4, 64)];
                head = 0;
                MarkDirtyRepaint();
            }
        }

        [UxmlAttribute] public Color QuietColor { get; set; } = new Color(0.43f, 0.91f, 0.63f);
        [UxmlAttribute] public Color LoudColor { get; set; } = new Color(1f, 0.71f, 0.28f);
        [UxmlAttribute] public float LoudThreshold { get; set; } = 0.7f;

        public VoiceMeter()
        {
            AddToClassList("voice-meter");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Push(float level)
        {
            levels[head] = Mathf.Clamp01(level);
            head = (head + 1) % levels.Length;
            MarkDirtyRepaint();
        }

        public void ClearLevels()
        {
            System.Array.Clear(levels, 0, levels.Length);
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            int count = levels.Length;
            if (rect.width < 1f || rect.height < 1f) return;

            float slot = rect.width / count;
            float barWidth = Mathf.Max(1f, slot * 0.55f);
            float bottom = rect.yMax - barWidth * 0.5f;
            float usable = rect.height - barWidth;

            Painter2D p = context.painter2D;
            p.lineWidth = barWidth;
            p.lineCap = LineCap.Round;

            for (int i = 0; i < count; i++)
            {
                float level = levels[(head + i) % count]; // oldest on the left
                float x = rect.x + slot * (i + 0.5f);
                float height = Mathf.Max(barWidth * 0.6f, level * usable);

                Color color = level >= LoudThreshold ? LoudColor : QuietColor;
                if (level < 0.04f) color.a = 0.55f; // silent bars read as small dashes

                p.strokeColor = color;
                p.BeginPath();
                p.MoveTo(new Vector2(x, bottom));
                p.LineTo(new Vector2(x, bottom - height + barWidth * 0.5f));
                p.Stroke();
            }
        }
    }
}
