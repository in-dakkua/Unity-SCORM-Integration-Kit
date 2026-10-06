using System;
using System.Collections.Generic;

namespace Scorm.Scenarios
{
    /// <summary>
    /// JSON stored in cmi.suspend_data (JsonUtility, short field names to stay far below the 64000 characters of
    /// SCORM 2004). Bump <see cref="CurrentVersion"/> when the format changes and migrate in ScenarioTracker.
    /// </summary>
    [Serializable]
    public class ScenarioSaveData
    {
        public const int CurrentVersion = 1;

        /// <summary>
        /// Format version. 0 (default) means "absent": JsonUtility keeps field initializers for missing keys, so a
        /// default of CurrentVersion would accept any foreign JSON object as version 1. ScenarioTracker sets it on save.
        /// </summary>
        public int v;

        /// <summary>Id of the last scenario started (also written to cmi.location).</summary>
        public string cur = "";

        /// <summary>One entry per scenario that has been started at least once (unknown ids are kept untouched).</summary>
        public List<ScenarioSaveEntry> s = new List<ScenarioSaveEntry>();
    }
}
