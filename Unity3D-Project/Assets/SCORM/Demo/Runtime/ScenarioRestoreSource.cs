namespace Scorm.Scenarios
{
    /// <summary>Where <see cref="ScenarioTracker.Initialize"/> took the state from.</summary>
    public enum ScenarioRestoreSource
    {
        /// <summary>Nothing to restore: new attempt.</summary>
        Empty,
        /// <summary>cmi.suspend_data with a JSON of a supported version.</summary>
        SuspendData,
        /// <summary>Rebuilt from the cmi.objectives whose id is a scenario of the catalog.</summary>
        Objectives
    }
}
