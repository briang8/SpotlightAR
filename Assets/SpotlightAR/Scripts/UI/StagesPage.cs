using System;
using System.Collections.Generic;
using SpotlightAR.Scoring;
using SpotlightAR.Stages;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpotlightAR.UI
{
    /// <summary>
    /// The Stages tab: a big card per stage with a real rendered preview, its vibe, the player's
    /// best score and number of shows there, and a button to make it their stage. Below that,
    /// the colour of the neon sign that shows their stage name.
    /// </summary>
    public sealed class StagesPage
    {
        /// <summary>Neon sign colours. Index 0 keeps each stage's own colour.</summary>
        public static readonly (string name, Color color)[] SignColors =
        {
            ("Stage colour", Color.clear),
            ("Hot pink", new Color(1f, 0.36f, 0.78f)),
            ("Coral", new Color(1f, 0.45f, 0.32f)),
            ("Ice blue", new Color(0.35f, 0.8f, 1f)),
            ("Mint", new Color(0.35f, 1f, 0.7f)),
            ("White", new Color(0.95f, 0.95f, 1f)),
        };

        readonly VisualElement content;
        readonly List<VisualElement> previews = new List<VisualElement>();
        readonly Dictionary<int, Texture> previewTextures = new Dictionary<int, Texture>();

        public event Action<int> StageChosen;
        public event Action<int> SignColorChosen;

        public StagesPage(VisualElement content) => this.content = content;

        public void Show(IReadOnlyList<StageTheme> themes, int selected, PerformanceHistory history, int signColor)
        {
            content.Clear();
            previews.Clear();
            for (int i = 0; i < themes.Count; i++) content.Add(Card(themes[i], i, i == selected, history));
            content.Add(SignCard(signColor, themes.Count > selected && selected >= 0 ? themes[selected] : null));
        }

        /// <summary>A rendered picture of a stage, once it's ready (they're made in the background).</summary>
        public void SetPreview(int index, Texture texture)
        {
            previewTextures[index] = texture;
            if (index < previews.Count) ApplyPreview(previews[index], texture);
        }

        static void ApplyPreview(VisualElement element, Texture texture)
        {
            if (texture == null) return;
            element.style.backgroundImage = Background.FromRenderTexture(texture as RenderTexture);
            element.AddToClassList("stage-tile__preview--ready");
        }

        VisualElement Card(StageTheme theme, int index, bool selected, PerformanceHistory history)
        {
            var card = new VisualElement();
            card.AddToClassList("stage-tile");
            card.EnableInClassList("stage-tile--selected", selected);

            var preview = new VisualElement();
            preview.AddToClassList("stage-tile__preview");
            preview.style.backgroundColor = Color.Lerp(theme.accent, Color.black, 0.75f);
            previews.Add(preview);
            if (previewTextures.TryGetValue(index, out Texture texture)) ApplyPreview(preview, texture);

            if (selected)
            {
                var chip = new VisualElement();
                chip.AddToClassList("stage-tile__chip");
                chip.Add(new Icon { Glyph = IconGlyph.Check });
                var chipText = new Label("YOUR STAGE");
                chipText.AddToClassList("t-caps");
                chip.Add(chipText);
                preview.Add(chip);
            }
            card.Add(preview);

            var info = new VisualElement();
            info.AddToClassList("stage-tile__info");

            var name = new Label(theme.displayName);
            name.AddToClassList("stage-tile__name");
            name.AddToClassList("heading--bold");
            var tagline = new Label(theme.tagline);
            tagline.AddToClassList("stage-tile__tagline");

            int shows = history != null ? history.ShowsOnStage(theme.displayName) : 0;
            int best = history != null ? history.BestOnStage(theme.displayName) : 0;
            var meta = new Label(!theme.IsAvailable ? "Coming soon"
                : shows == 0 ? "No shows here yet"
                : $"Best {best:N0} · {shows} {(shows == 1 ? "show" : "shows")}");
            meta.AddToClassList("stage-tile__meta");
            meta.AddToClassList("t-semibold");

            info.Add(name);
            info.Add(tagline);
            info.Add(meta);

            if (theme.IsAvailable)
            {
                var button = new Button(() => StageChosen?.Invoke(index)) { text = selected ? "Selected" : "Use this stage" };
                button.AddToClassList(selected ? "secondary-button" : "primary-button");
                button.AddToClassList("stage-tile__button");
                button.SetEnabled(!selected);
                info.Add(button);
            }
            card.Add(info);
            return card;
        }

        VisualElement SignCard(int selectedColor, StageTheme stage)
        {
            var card = new VisualElement();
            card.AddToClassList("panel-card");
            card.AddToClassList("sign-card");

            var title = new Label("NEON SIGN COLOUR");
            title.AddToClassList("panel-card__title");
            title.AddToClassList("t-caps");
            card.Add(title);

            var row = new VisualElement();
            row.AddToClassList("swatches");
            for (int i = 0; i < SignColors.Length; i++)
            {
                int index = i;
                var swatch = new Button(() => SignColorChosen?.Invoke(index)) { tooltip = SignColors[i].name };
                swatch.AddToClassList("swatch");
                swatch.EnableInClassList("swatch--selected", i == selectedColor);
                Color shown = i == 0 && stage != null ? stage.accent : SignColors[i].color;
                swatch.style.backgroundColor = shown;
                if (i == 0) swatch.AddToClassList("swatch--auto");
                row.Add(swatch);
            }
            card.Add(row);

            var caption = new Label(selectedColor == 0
                ? "Your stage name glows in each stage's own colour."
                : $"Your stage name glows {SignColors[Mathf.Clamp(selectedColor, 0, SignColors.Length - 1)].name.ToLowerInvariant()} on every stage.");
            caption.AddToClassList("graph-caption");
            card.Add(caption);
            return card;
        }
    }
}
