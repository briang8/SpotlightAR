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

        [Tooltip("One row of the song list (UI/Templates/SongRow.uxml).")]
        [SerializeField] VisualTreeAsset songRowTemplate;

        UIDocument document;
        VisualElement root;
        VisualElement safeArea;
        Rect appliedSafeArea;

        public HomeScreen Home { get; private set; }
        public SongSetupScreen Setup { get; private set; }
        public PlacementScreen Placement { get; private set; }
        public SingingHud Hud { get; private set; }
        public ResultsScreen Results { get; private set; }
        public UIScreen Active { get; private set; }

        void OnEnable()
        {
            document = GetComponent<UIDocument>();
            root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            safeArea = root.Q("safe-area") ?? root;

            screens.Clear();
            Home = Register(new HomeScreen(root.Q("home-screen"), songRowTemplate));
            Setup = Register(new SongSetupScreen(root.Q("setup-screen")));
            Placement = Register(new PlacementScreen(root.Q("placement-screen")));
            Hud = Register(new SingingHud(root.Q("singing-hud")));
            Results = Register(new ResultsScreen(root.Q("results-screen")));

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

        /// <summary>Converts a rect in panel units to screen pixels (origin top-left).</summary>
        public Rect PanelToScreen(Rect panelRect)
        {
            if (root == null || root.layout.width <= 0f) return Rect.zero;
            float scale = Screen.width / root.layout.width;
            return new Rect(panelRect.x * scale, panelRect.y * scale, panelRect.width * scale, panelRect.height * scale);
        }

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
