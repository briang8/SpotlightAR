using System;
using System.Collections.Generic;
using SpotlightAR.AR;
using UnityEngine.UIElements;

namespace SpotlightAR.UI
{
    /// <summary>
    /// "Tap to place your stage": surface status, the placement hint card and the
    /// bottom sheet with size presets and the start button.
    /// </summary>
    public sealed class PlacementScreen : UIScreen
    {
        const string SelectedClass = "segment--selected";
        const string FoundClass = "status-chip--found";

        readonly VisualElement statusChip;
        readonly Label statusLabel;
        readonly Label hintTitle;
        readonly Label hintBody;
        readonly Label stageLabel;
        readonly Label sizeLabel;
        readonly Button placeButton;
        readonly Dictionary<StageSize, Button> sizeButtons = new Dictionary<StageSize, Button>();

        public event Action CloseRequested;
        public event Action<StageSize> SizeSelected;
        public event Action PlaceRequested;

        public PlacementScreen(VisualElement root) : base(root)
        {
            statusChip = Require<VisualElement>("status-chip");
            statusLabel = Require<Label>("status-label");
            hintTitle = Require<Label>("hint-title");
            hintBody = Require<Label>("hint-body");
            stageLabel = Require<Label>("stage-label");
            sizeLabel = Require<Label>("size-label");
            placeButton = Require<Button>("place-button");

            Require<Button>("close-button").clicked += () => CloseRequested?.Invoke();
            placeButton.clicked += () => PlaceRequested?.Invoke();

            BindSize(StageSize.Tabletop, "size-tabletop");
            BindSize(StageSize.Room, "size-room");
            BindSize(StageSize.LifeSize, "size-life");
        }

        void BindSize(StageSize size, string name)
        {
            Button button = Require<Button>(name);
            button.clicked += () => SizeSelected?.Invoke(size);
            sizeButtons[size] = button;
        }

        public void SetStageName(string stageName) => stageLabel.text = $"Stage: {stageName}";

        public void SetSize(StageSize size)
        {
            sizeLabel.text = $"Size: {Describe(size)}";
            foreach (var pair in sizeButtons) pair.Value.EnableInClassList(SelectedClass, pair.Key == size);
        }

        public void SetSurface(SurfaceKind surface, bool stagePlaced)
        {
            bool found = surface != SurfaceKind.None;
            statusChip.EnableInClassList(FoundClass, found);
            statusLabel.text = surface switch
            {
                SurfaceKind.Floor => "Floor found",
                SurfaceKind.Table => "Table found",
                _ => "Looking for a surface"
            };

            if (stagePlaced)
            {
                hintTitle.text = "Looking good!";
                hintBody.text = "Tap somewhere else to move it. Pinch to resize, twist to turn.";
            }
            else if (found)
            {
                hintTitle.text = "Tap to place your stage";
                hintBody.text = "Pinch to resize. Twist with two fingers to turn it.";
            }
            else
            {
                hintTitle.text = "Scan the room";
                hintBody.text = "Move your phone slowly over a floor or table.";
            }

            placeButton.SetEnabled(found || stagePlaced);
        }

        static string Describe(StageSize size) => size switch
        {
            StageSize.Room => "room",
            StageSize.LifeSize => "life size",
            _ => "tabletop"
        };
    }
}
