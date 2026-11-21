using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpotlightAR.UI
{
    /// <summary>
    /// The first-launch tutorial: four short cards (what the app is, headphones, placing the
    /// stage, singing) that can be tapped or swiped through, or skipped. It sits on top of the
    /// Home screen instead of replacing it, and can be replayed from the help button on Home.
    /// </summary>
    public sealed class OnboardingOverlay : UIScreen
    {
        const float SwipeDistance = 120f;

        static readonly (IconGlyph icon, string title, string body)[] Cards =
        {
            (IconGlyph.Spotlight, "Your stage, anywhere",
                "Pick a song, drop a stage on your floor or table, and sing to a crowd that reacts to you."),
            (IconGlyph.Headphones, "Plug in headphones",
                "The mic needs to hear you, not the song. Wired or Bluetooth both work."),
            (IconGlyph.Phone, "Find a floor, tap to place",
                "Move your phone slowly over the floor or a table, then tap. Pinch to resize, twist to turn."),
            (IconGlyph.Mic, "Sing out",
                "The louder and longer you sing, the wilder the crowd gets. Hold a note to see it on screen, and pick Sing mode for synced lyrics."),
        };

        readonly Label step;
        readonly VisualElement card;
        readonly Icon icon;
        readonly Label title;
        readonly Label body;
        readonly VisualElement dots;
        readonly Button next;

        int index;
        Vector2 pressStart;
        bool pressing;

        /// <summary>The player finished or skipped the cards.</summary>
        public event Action Finished;

        public OnboardingOverlay(VisualElement root) : base(root)
        {
            step = Require<Label>("onboarding-step");
            card = Require<VisualElement>("onboarding-card");
            icon = Require<Icon>("onboarding-icon");
            title = Require<Label>("onboarding-title");
            body = Require<Label>("onboarding-body");
            dots = Require<VisualElement>("onboarding-dots");
            next = Require<Button>("onboarding-next");

            next.clicked += () => Go(index + 1);
            Require<Button>("onboarding-skip").clicked += Close;

            // Swipe left / right on the card to move between cards.
            card.RegisterCallback<PointerDownEvent>(evt => { pressing = true; pressStart = evt.position; });
            card.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!pressing) return;
                pressing = false;
                float dx = evt.position.x - pressStart.x;
                if (dx < -SwipeDistance) Go(index + 1);
                else if (dx > SwipeDistance && index > 0) Go(index - 1);
            });
            card.RegisterCallback<PointerLeaveEvent>(_ => pressing = false);

            for (int i = 0; i < Cards.Length; i++)
            {
                var dot = new VisualElement();
                dot.AddToClassList("dot");
                dots.Add(dot);
            }
        }

        protected override void OnShown() => Go(0, animate: false);

        void Go(int target, bool animate = true)
        {
            if (target >= Cards.Length)
            {
                Close();
                return;
            }

            index = Mathf.Clamp(target, 0, Cards.Length - 1);
            var (glyph, heading, text) = Cards[index];
            icon.Glyph = glyph;
            title.text = heading;
            body.text = text;
            step.text = $"{index + 1} of {Cards.Length}";
            next.text = index == Cards.Length - 1 ? "Let's go" : "Next";
            for (int i = 0; i < dots.childCount; i++) dots[i].EnableInClassList("dot--active", i == index);

            if (!animate) return;
            // A small slide-in so each card feels like a new page.
            card.AddToClassList("onboarding__card--enter");
            card.schedule.Execute(() => card.RemoveFromClassList("onboarding__card--enter")).ExecuteLater(16);
        }

        /// <summary>Close the cards (Skip, the last card, or the back button) and remember they were seen.</summary>
        public void Close()
        {
            Hide();
            Finished?.Invoke();
        }
    }
}
