using System;
using UnityEngine;

namespace Scorm.Scenarios
{
    /// <summary>
    /// Creates the <see cref="ScenarioTracker"/> once the SCORM data is loaded. Put it on the ScormManager GameObject
    /// (or a child): ScormManager announces the end of Initialize with BroadcastMessage("Scorm_Initialize_Complete").
    /// </summary>
    public class ScenarioTrackerHost : MonoBehaviour
    {
        [SerializeField] private ScenarioCatalog _catalog;

        [Tooltip("Commit after each completed scenario (recommended: Moodle only persists data on Commit).")]
        [SerializeField] private bool _autoCommit = true;

        public ScenarioCatalog Catalog => _catalog;

        /// <summary>Null until Scorm_Initialize_Complete.</summary>
        public ScenarioTracker Tracker { get; private set; }

        /// <summary>Error message when the tracker could not be created (invalid catalog, SCORM not initialized).</summary>
        public string InitializationError { get; private set; }

        /// <summary>Kind of InitializationError (for a localized message), or null when there is no error.</summary>
        public ScenarioTrackerError? InitializationErrorKind { get; private set; }

        /// <summary>Raised every time a tracker is ready (also after a relaunch with ScormManager.Initialize).</summary>
        public event Action<ScenarioTracker> Ready;

        public void SetCatalog(ScenarioCatalog catalog)
        {
            _catalog = catalog;
        }

        private void Start()
        {
            // ScormManager.Start may have run first (same frame, undefined order): catch up.
            if (Tracker == null && ScormManager.IsInitialized)
                Scorm_Initialize_Complete();
        }

        // Called by name through BroadcastMessage from ScormManager.Initialize.
        [UnityEngine.Scripting.Preserve]
        public void Scorm_Initialize_Complete()
        {
            try
            {
                ScenarioTracker tracker = new ScenarioTracker(_catalog);
                tracker.AutoCommit = _autoCommit;
                tracker.Initialize();
                Tracker = tracker;
                InitializationError = null;
                InitializationErrorKind = null;
                if (!string.IsNullOrEmpty(tracker.RestoreWarning))
                    Debug.LogWarning("[SCORM] " + tracker.RestoreWarning);
            }
            catch (ScenarioTrackerException e)
            {
                Tracker = null;
                InitializationError = e.Message;
                InitializationErrorKind = e.Error;
                Debug.LogError("[SCORM] " + e.Message);
                return;
            }
            Ready?.Invoke(Tracker);
        }
    }
}
