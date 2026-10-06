using System;

namespace Scorm.Scenarios
{
    /// <summary>State of one scenario as seen by the UI (read only; the <see cref="ScenarioTracker"/> updates it).</summary>
    public sealed class ScenarioProgress
    {
        private readonly ScenarioCatalog _catalog;

        internal ScenarioProgress(ScenarioDefinition definition, ScenarioCatalog catalog, ScenarioSaveEntry entry)
        {
            Definition = definition;
            _catalog = catalog;
            Entry = entry;
        }

        internal ScenarioSaveEntry Entry { get; set; }

        public ScenarioDefinition Definition { get; }
        public string Id => Definition.Id;

        /// <summary>Completed attempts.</summary>
        public int Attempts => Entry.n;

        public bool IsCompleted => Entry.n > 0;

        /// <summary>True between BeginScenario and CompleteScenario.</summary>
        public bool IsAttemptInProgress => Entry.p != 0;

        public float? BestRaw => Entry.n > 0 ? Entry.b : (float?)null;
        public float? LastRaw => Entry.n > 0 ? Entry.l : (float?)null;

        /// <summary>Raw score reported to the LMS according to the catalog policy (best or last attempt).</summary>
        public float? ReportedRaw
        {
            get
            {
                if (Entry.n == 0)
                    return null;
                return _catalog.ScorePolicy == ScenarioScorePolicy.Last ? Entry.l : Entry.b;
            }
        }

        public float? ReportedScaled => ReportedRaw.HasValue ? ScenarioScoring.ToScaled(Definition, ReportedRaw.Value) : (float?)null;

        public bool IsPassed => ReportedScaled.HasValue && ScenarioScoring.IsPassed(Definition, ReportedScaled.Value);

        public ScenarioStatus Status
        {
            get
            {
                if (Entry.n == 0)
                    return Entry.p != 0 ? ScenarioStatus.InProgress : ScenarioStatus.NotStarted;
                return IsPassed ? ScenarioStatus.Passed : ScenarioStatus.Failed;
            }
        }

        public DateTime? FirstStartedUtc => Entry.t0 > 0 ? DateTimeOffset.FromUnixTimeSeconds(Entry.t0).UtcDateTime : (DateTime?)null;
        public DateTime? LastCompletedUtc => Entry.t1 > 0 ? DateTimeOffset.FromUnixTimeSeconds(Entry.t1).UtcDateTime : (DateTime?)null;
    }
}
