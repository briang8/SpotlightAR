using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpotlightAR.UI
{
    /// <summary>
    /// Owns the UI Toolkit document: builds the screen wrappers, switches between them,
    /// keeps content inside the phone's safe area (notches, rounded corners) and tells
    /// the AR input whether a touch landed on the UI.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class UIManager : MonoBehaviour
    {
        readonly List<UIScreen> screens = new List<UIScreen>();

        UIDocument document;
        VisualElement root;
        VisualElement safeArea;
        Rect appliedSafeArea;

        public PlacementScreen Placement { get; private set; }
        public SingingHud Hud { get; private set; }
        public UIScreen Active { get; private set; }

        void OnEnable()
        {
            document = GetComponent<UIDocument>();
            root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            safeArea = root.Q("safe-area") ?? root;

            screens.Clear();
            Placement = Register(new PlacementScreen(root.Q("placement-screen")));
            Hud = Register(new SingingHud(root.Q("singing-hud")));

            root.RegisterCallback<GeometryChangedEvent>(_ => ApplySafeArea(true));
        }

        T Register<T>(T screen) where T : UIScreen
        {
            screen.Hide(true);
            screens.Add(screen);
            return screen;
        }

        void Update()
        {
            if (Screen.safeArea != appliedSafeArea) ApplySafeArea(false);
        }

        /// <summary>Show one screen and hide the rest.</summary>
        public void Show(UIScreen screen)
        {
            foreach (UIScreen candidate in screens)
            {
                if (candidate == screen) candidate.Show();
                else if (candidate.IsVisible) candidate.Hide();
            }
            Active = screen;
        }

        public void UpdateHUD(int score) => Hud.SetScore(score);

        /// <summary>True when a screen position (pixels, origin bottom-left) is over a UI element.</summary>
        public bool IsPointerOverUI(Vector2 screenPosition)
        {
            IPanel panel = root?.panel;
            if (panel == null) return false;

            Vector2 flipped = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            VisualElement picked = panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, flipped));
            return picked != null && picked != root;
        }

        void ApplySafeArea(bool force)
        {
            IPanel panel = root?.panel;
            if (panel == null || float.IsNaN(root.layout.width)) return;

            Rect area = Screen.safeArea;
            if (!force && area == appliedSafeArea) return;
            appliedSafeArea = area;

            Vector2 topLeft = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(area.xMin, Screen.height - area.yMax));
            Vector2 bottomRight = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(area.xMax, Screen.height - area.yMin));

            safeArea.style.paddingLeft = Mathf.Max(0f, topLeft.x);
            safeArea.style.paddingTop = Mathf.Max(0f, topLeft.y);
            safeArea.style.paddingRight = Mathf.Max(0f, root.layout.width - bottomRight.x);
            safeArea.style.paddingBottom = Mathf.Max(0f, root.layout.height - bottomRight.y);
        }
    }
}
