using UnityEngine;
using UnityEngine.UIElements;

namespace SpotlightAR.UI
{
    public enum IconGlyph
    {
        None,
        Close,
        Mic,
        MicOff,
        Move,
        Pause,
        Play,
        Star,
        Check,
        Search,
        Home,
        Library,
        Cube,
        Waveform,
        ChevronLeft
    }

    /// <summary>
    /// Crisp vector icons drawn with Painter2D, so they stay sharp on any screen
    /// and need no texture files. Colour comes from the USS "color" property.
    /// Usage in UXML: &lt;SpotlightAR.UI.Icon glyph="Mic" /&gt;
    /// </summary>
    [UxmlElement]
    public partial class Icon : VisualElement
    {
        IconGlyph glyph;
        float stroke = 2f;
        Vector2 origin;
        float unit;

        [UxmlAttribute]
        public IconGlyph Glyph
        {
            get => glyph;
            set { glyph = value; MarkDirtyRepaint(); }
        }

        /// <summary>Line thickness on a 24-unit grid.</summary>
        [UxmlAttribute]
        public float Stroke
        {
            get => stroke;
            set { stroke = value; MarkDirtyRepaint(); }
        }

        public Icon()
        {
            AddToClassList("icon");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(_ => MarkDirtyRepaint());
        }

        Vector2 P(float x, float y) => origin + new Vector2(x, y) * unit;

        void Draw(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            if (glyph == IconGlyph.None || rect.width < 1f || rect.height < 1f) return;

            unit = Mathf.Min(rect.width, rect.height) / 24f;
            origin = new Vector2(rect.x + (rect.width - 24f * unit) * 0.5f, rect.y + (rect.height - 24f * unit) * 0.5f);

            Painter2D p = context.painter2D;
            Color color = resolvedStyle.color;
            p.strokeColor = color;
            p.fillColor = color;
            p.lineWidth = stroke * unit;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;

            switch (glyph)
            {
                case IconGlyph.Close:
                    Line(p, 6, 6, 18, 18);
                    Line(p, 18, 6, 6, 18);
                    break;
                case IconGlyph.Mic:
                    DrawMic(p);
                    break;
                case IconGlyph.MicOff:
                    DrawMic(p);
                    Line(p, 4, 4, 20, 20);
                    break;
                case IconGlyph.Move:
                    Line(p, 12, 3, 12, 21);
                    Line(p, 3, 12, 21, 12);
                    Polyline(p, false, 9, 6, 12, 3, 15, 6);
                    Polyline(p, false, 9, 18, 12, 21, 15, 18);
                    Polyline(p, false, 6, 9, 3, 12, 6, 15);
                    Polyline(p, false, 18, 9, 21, 12, 18, 15);
                    break;
                case IconGlyph.Pause:
                    FillRect(p, 6.5f, 5f, 4f, 14f);
                    FillRect(p, 13.5f, 5f, 4f, 14f);
                    break;
                case IconGlyph.Play:
                    Polyline(p, true, 8, 5, 19, 12, 8, 19);
                    p.Fill();
                    p.Stroke();
                    break;
                case IconGlyph.Star:
                    DrawStar(p);
                    break;
                case IconGlyph.Check:
                    Polyline(p, false, 5, 12.5f, 10, 17.5f, 19, 7);
                    break;
                case IconGlyph.Search:
                    Circle(p, 11, 11, 6.5f);
                    Line(p, 16, 16, 20.5f, 20.5f);
                    break;
                case IconGlyph.Home:
                    Polyline(p, false, 3.5f, 11, 12, 4, 20.5f, 11);
                    Polyline(p, false, 6, 9.5f, 6, 20, 18, 20, 18, 9.5f);
                    Polyline(p, false, 10, 20, 10, 15, 14, 15, 14, 20);
                    break;
                case IconGlyph.Library:
                    Line(p, 5, 4, 5, 20);
                    Line(p, 9.5f, 4, 9.5f, 20);
                    Line(p, 14, 5, 18.5f, 19.5f);
                    break;
                case IconGlyph.Cube:
                    Polyline(p, true, 12, 3, 20, 7.5f, 20, 16.5f, 12, 21, 4, 16.5f, 4, 7.5f);
                    p.Stroke();
                    Polyline(p, false, 4, 7.5f, 12, 12, 20, 7.5f);
                    Line(p, 12, 12, 12, 21);
                    break;
                case IconGlyph.Waveform:
                    Line(p, 4, 10, 4, 14);
                    Line(p, 8, 6, 8, 18);
                    Line(p, 12, 3, 12, 21);
                    Line(p, 16, 7, 16, 17);
                    Line(p, 20, 10, 20, 14);
                    break;
                case IconGlyph.ChevronLeft:
                    Polyline(p, false, 15, 5, 8, 12, 15, 19);
                    break;
            }
        }

        void DrawMic(Painter2D p)
        {
            // Capsule
            p.BeginPath();
            p.MoveTo(P(9, 12));
            p.LineTo(P(9, 6));
            p.Arc(P(12, 6), 3f * unit, Angle.Degrees(180f), Angle.Degrees(360f));
            p.LineTo(P(15, 12));
            p.Arc(P(12, 12), 3f * unit, Angle.Degrees(0f), Angle.Degrees(180f));
            p.ClosePath();
            p.Stroke();

            // Holder, stem and base
            p.BeginPath();
            p.Arc(P(12, 11), 6.5f * unit, Angle.Degrees(180f), Angle.Degrees(0f), ArcDirection.CounterClockwise);
            p.Stroke();
            Line(p, 12, 17.5f, 12, 21);
            Line(p, 8.5f, 21, 15.5f, 21);
        }

        void DrawStar(Painter2D p)
        {
            p.BeginPath();
            for (int i = 0; i < 10; i++)
            {
                float radius = i % 2 == 0 ? 9f : 4f;
                float angle = (-90f + i * 36f) * Mathf.Deg2Rad;
                Vector2 point = P(12f + Mathf.Cos(angle) * radius, 12.8f + Mathf.Sin(angle) * radius);
                if (i == 0) p.MoveTo(point);
                else p.LineTo(point);
            }
            p.ClosePath();
            p.Stroke();
        }

        void Line(Painter2D p, float x1, float y1, float x2, float y2)
        {
            p.BeginPath();
            p.MoveTo(P(x1, y1));
            p.LineTo(P(x2, y2));
            p.Stroke();
        }

        /// <summary>Builds a path through the given x,y pairs. Open paths are stroked here.</summary>
        void Polyline(Painter2D p, bool closed, params float[] xy)
        {
            p.BeginPath();
            p.MoveTo(P(xy[0], xy[1]));
            for (int i = 2; i < xy.Length; i += 2) p.LineTo(P(xy[i], xy[i + 1]));
            if (closed) p.ClosePath();
            else p.Stroke();
        }

        void Circle(Painter2D p, float x, float y, float radius)
        {
            p.BeginPath();
            p.Arc(P(x, y), radius * unit, Angle.Degrees(0f), Angle.Degrees(360f));
            p.Stroke();
        }

        void FillRect(Painter2D p, float x, float y, float width, float height)
        {
            float r = Mathf.Min(width, height) * 0.35f;
            p.BeginPath();
            p.MoveTo(P(x + r, y));
            p.ArcTo(P(x + width, y), P(x + width, y + height), r * unit);
            p.ArcTo(P(x + width, y + height), P(x, y + height), r * unit);
            p.ArcTo(P(x, y + height), P(x, y), r * unit);
            p.ArcTo(P(x, y), P(x + width, y), r * unit);
            p.ClosePath();
            p.Fill();
        }
    }
}
