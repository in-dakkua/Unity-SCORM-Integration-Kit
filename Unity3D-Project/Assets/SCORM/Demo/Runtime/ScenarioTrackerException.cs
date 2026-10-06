using System;

namespace Scorm.Scenarios
{
    /// <summary>
    /// Misuse of the <see cref="ScenarioTracker"/> (unknown scenario, closed session, state too large...). The message
    /// is technical (English); use <see cref="Error"/> to show a localized text in the UI.
    /// </summary>
    public class ScenarioTrackerException : Exception
    {
        public ScenarioTrackerException(ScenarioTrackerError error, string message) : base(message)
        {
            Error = error;
        }

        public ScenarioTrackerError Error { get; }
    }
}
