using System;

namespace Scorm.Scenarios
{
    /// <summary>Saved state of one scenario. Scores are raw values in the range of the scenario definition.</summary>
    [Serializable]
    public class ScenarioSaveEntry
    {
        /// <summary>Scenario id (= cmi.objectives.n.id).</summary>
        public string id = "";

        /// <summary>Completed attempts.</summary>
        public int n;

        /// <summary>Best raw score (valid when n &gt; 0).</summary>
        public float b;

        /// <summary>Last raw score (valid when n &gt; 0).</summary>
        public float l;

        /// <summary>1 while an attempt has been started and not completed.</summary>
        public int p;

        /// <summary>Unix time (seconds, UTC) of the first start; 0 = never.</summary>
        public long t0;

        /// <summary>Unix time (seconds, UTC) of the last completion; 0 = never.</summary>
        public long t1;

        /// <summary>Attempts started (BeginScenario); numbers the interaction ids "&lt;id&gt;-a&lt;a&gt;-&lt;decision&gt;".</summary>
        public int a;

        /// <summary>Decisions recorded in the current/last attempt (informative).</summary>
        public int d;
    }
}
