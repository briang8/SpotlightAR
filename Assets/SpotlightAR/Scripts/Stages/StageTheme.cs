using System;
using UnityEngine;

namespace SpotlightAR.Stages
{
    /// <summary>A stage the player can pick on the song setup screen.</summary>
    [Serializable]
    public sealed class StageTheme
    {
        public string displayName = "Club Neon";
        [TextArea] public string tagline = "Lasers and a disco ball";
        public Color accent = new Color(1f, 0.48f, 0.84f);
        [Tooltip("Leave empty to show the stage as 'Coming soon'.")]
        public Stage prefab;

        public bool IsAvailable => prefab != null;
    }
}
