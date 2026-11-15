namespace SpotlightAR.Audio
{
    /// <summary>
    /// The "Mic hears you?" step from the flowchart. The player passes once their voice
    /// stays above a threshold briefly; if nothing is heard for a while, tips are shown.
    /// Pure logic with no Unity dependencies, so it is easy to reason about and test.
    /// </summary>
    public sealed class MicCheck
    {
        public enum Result { Listening, Heard, NeedsHelp }

        readonly float threshold;
        readonly float holdSeconds;
        readonly float helpAfterSeconds;
        float heardFor;
        float elapsed;

        public MicCheck(float threshold = 0.3f, float holdSeconds = 0.35f, float helpAfterSeconds = 6f)
        {
            this.threshold = threshold;
            this.holdSeconds = holdSeconds;
            this.helpAfterSeconds = helpAfterSeconds;
        }

        public Result Current { get; private set; } = Result.Listening;

        public void Reset()
        {
            heardFor = 0f;
            elapsed = 0f;
            Current = Result.Listening;
        }

        public Result Update(float loudness, float deltaTime)
        {
            if (Current == Result.Heard) return Current;

            elapsed += deltaTime;
            heardFor = loudness >= threshold ? heardFor + deltaTime : 0f;

            if (heardFor >= holdSeconds) Current = Result.Heard;
            else if (elapsed >= helpAfterSeconds) Current = Result.NeedsHelp;
            return Current;
        }
    }
}
