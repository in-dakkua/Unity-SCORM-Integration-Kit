namespace Scorm.Scenarios
{
    /// <summary>Which attempt of a scenario is reported to the LMS.</summary>
    public enum ScenarioScorePolicy
    {
        /// <summary>Highest score of all the attempts.</summary>
        Best,
        /// <summary>Score of the most recent attempt.</summary>
        Last
    }
}
