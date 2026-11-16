namespace SpotlightAR.Audio
{
    /// <summary>Anything that can report the pitch of the singer's voice.</summary>
    public interface IPitchSource
    {
        /// <summary>Current pitch in Hz, or 0 when no clear note is being sung.</summary>
        float GetPitch();
    }
}
