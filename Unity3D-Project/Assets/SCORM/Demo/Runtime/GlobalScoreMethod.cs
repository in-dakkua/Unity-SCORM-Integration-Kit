namespace Scorm.Scenarios
{
    /// <summary>How the global score (cmi.score.*) is computed from the scenario scores.</summary>
    public enum GlobalScoreMethod
    {
        /// <summary>Weighted average over every scenario of the catalog; a scenario not completed yet counts as 0.</summary>
        WeightedAverageAllScenarios,
        /// <summary>Weighted average over the completed scenarios only.</summary>
        WeightedAverageCompletedOnly
    }
}
