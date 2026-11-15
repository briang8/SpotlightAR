using System;
using UnityEngine.UIElements;

namespace SpotlightAR.UI
{
    /// <summary>
    /// Base for every screen. Wraps one root element from the UXML and handles
    /// the fade in / fade out (the transition itself lives in the USS).
    /// </summary>
    public abstract class UIScreen
    {
        const string HiddenClass = "screen--hidden";
        const long FadeMs = 220;

        protected UIScreen(VisualElement root)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root), $"{GetType().Name}: screen root not found in UXML.");
            Root.AddToClassList("screen");
        }

        protected VisualElement Root { get; }

        public bool IsVisible { get; private set; }

        public void Show()
        {
            IsVisible = true;
            Root.style.display = DisplayStyle.Flex;
            // Remove the hidden class on the next frame so the opacity transition runs.
            Root.schedule.Execute(() => { if (IsVisible) Root.RemoveFromClassList(HiddenClass); });
            OnShown();
        }

        public void Hide(bool instant = false)
        {
            IsVisible = false;
            Root.AddToClassList(HiddenClass);
            if (instant) Root.style.display = DisplayStyle.None;
            else Root.schedule.Execute(() => { if (!IsVisible) Root.style.display = DisplayStyle.None; }).ExecuteLater(FadeMs);
        }

        protected virtual void OnShown() { }

        /// <summary>Find a required element and fail loudly if the UXML was renamed.</summary>
        protected T Require<T>(string name) where T : VisualElement
        {
            return Root.Q<T>(name) ?? throw new InvalidOperationException($"{GetType().Name}: missing '{name}' ({typeof(T).Name}) in UXML.");
        }
    }
}
