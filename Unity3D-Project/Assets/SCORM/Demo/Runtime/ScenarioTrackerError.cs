namespace Scorm.Scenarios
{
    /// <summary>Kind of <see cref="ScenarioTrackerException"/>, so a UI can show its own (localized) message.</summary>
    public enum ScenarioTrackerError
    {
        InvalidCatalog,
        ScormNotInitialized,
        NotInitialized,
        SessionClosed,
        UnknownScenario,
        SuspendDataTooLarge
    }
}
