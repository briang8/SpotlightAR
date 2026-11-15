namespace SpotlightAR.Audio
{
    /// <summary>
    /// Anything that can report how loud the player is singing, normalised to 0..1.
    /// Gameplay systems (stage, score) depend on this abstraction instead of the
    /// concrete microphone, so they can be driven by a test source or a recording.
    /// </summary>
    public interface ILoudnessSource
    {
        /// <summary>Smoothed loudness, 0 (silence) to 1 (very loud).</summary>
        float GetLoudness();

        /// <summary>True while audio is actively being captured.</summary>
        bool IsListening();
    }
}
