using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scorm.Scenarios
{
    /// <summary>
    /// Tracks the scenarios of a single SCO on top of the ScormManager scenario API.
    /// </summary>
    /// <remarks>
    /// <para>LMS mapping:</para>
    /// <list type="bullet">
    /// <item>each scenario = cmi.objectives.n with the stable id of its <see cref="ScenarioDefinition"/>, its own score
    /// (raw in the scenario range, scaled 0..1), success_status (scenario threshold), completion_status and progress;</item>
    /// <item>each decision = cmi.interactions.m linked to the objective of its scenario;</item>
    /// <item>global result = weighted average in cmi.score.raw (0..100) / min / max / scaled, cmi.success_status
    /// (passed/failed once every scenario is completed, unknown before), cmi.completion_status and cmi.progress_measure;</item>
    /// <item>own state (attempts, best/last score, timestamps) = versioned JSON in cmi.suspend_data; cmi.location = last
    /// scenario started.</item>
    /// </list>
    /// <para>Usage: create it after "Scorm_Initialize_Complete" (see <see cref="ScenarioTrackerHost"/>), call
    /// <see cref="Initialize"/>, then BeginScenario / RecordDecision / CompleteScenario and finally Suspend or Finish.
    /// Nothing is written on Initialize; afterwards only values that changed are written. The local state keeps working
    /// when there is no LMS (writes fail and are reported in <see cref="LastOperationFailures"/>).</para>
    /// </remarks>
    public sealed class ScenarioTracker
    {
        /// <summary>SCORM 2004 4th Edition SPM of cmi.suspend_data (hard limit of the tracker).</summary>
        public const int MaxSuspendDataLength = 64000;

        /// <summary>SCORM 2004 2nd/3rd Edition SPM of cmi.suspend_data: the only size every 3rd Edition LMS must keep.</summary>
        public const int PortableSuspendDataLength = 4000;

        private readonly ScenarioCatalog _catalog;
        private readonly Func<DateTime> _utcNow;
        private readonly List<ScenarioProgress> _scenarios = new List<ScenarioProgress>();
        private readonly List<ScormCallInfo> _failures = new List<ScormCallInfo>();
        private ScenarioSaveData _save = new ScenarioSaveData();
        private string _knownSuspendData;
        private string _knownLocation;
        private DateTime _sessionStartUtc;
        private bool _capturing;
        private bool _warnedSuspendDataSize;
        private readonly HashSet<string> _startedThisSession = new HashSet<string>();

        /// <param name="catalog">Scenarios and scoring rules.</param>
        /// <param name="utcNow">Clock (UTC) used for session_time and timestamps; DateTime.UtcNow by default.</param>
        /// <exception cref="ScenarioTrackerException">The catalog is missing or invalid.</exception>
        public ScenarioTracker(ScenarioCatalog catalog, Func<DateTime> utcNow = null)
        {
            if (catalog == null)
                throw new ScenarioTrackerException(ScenarioTrackerError.InvalidCatalog, "ScenarioTracker needs a ScenarioCatalog.");
            List<string> errors = catalog.Validate();
            if (errors.Count > 0)
                throw new ScenarioTrackerException(ScenarioTrackerError.InvalidCatalog, "Invalid ScenarioCatalog '" + catalog.name + "': " + string.Join(" ", errors.ToArray()));
            _catalog = catalog;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            AutoCommit = true;
        }

        /// <summary>Raised after every change of the local state (also after Initialize and after closing the session).</summary>
        public event Action Changed;

        /// <summary>Raised after CompleteScenario.</summary>
        public event Action<ScenarioProgress> ScenarioCompleted;

        /// <summary>Raised after Suspend (exit = suspend) or Finish (exit = normal).</summary>
        public event Action<StudentRecord.ExitType> SessionClosed;

        public ScenarioCatalog Catalog => _catalog;
        public IReadOnlyList<ScenarioProgress> Scenarios => _scenarios;
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// True after a successful Suspend or Finish: the SCORM session is terminated and nothing else can be written.
        /// If the LMS rejects the Commit or the Terminate, the tracker stays open so the call can be retried.
        /// </summary>
        public bool IsClosed { get; private set; }

        /// <summary>Commit automatically after CompleteScenario and ResetLocalState (Moodle persists only on Commit).</summary>
        public bool AutoCommit { get; set; }

        /// <summary>Where the state was loaded from on Initialize.</summary>
        public ScenarioRestoreSource RestoreSource { get; private set; }

        /// <summary>Technical token of RestoreSource: "suspend_data", "objectives" (rebuilt) or "empty".</summary>
        public string RestoredFrom
        {
            get
            {
                switch (RestoreSource)
                {
                    case ScenarioRestoreSource.SuspendData: return "suspend_data";
                    case ScenarioRestoreSource.Objectives: return "objectives";
                    default: return "empty";
                }
            }
        }

        /// <summary>Why cmi.suspend_data was ignored on Initialize (None when it was used or empty).</summary>
        public ScenarioRestoreIssue RestoreIssue { get; private set; }

        /// <summary>Technical (English) description of RestoreIssue, or null. Localize RestoreIssue for the UI.</summary>
        public string RestoreWarning { get; private set; }

        public string LastScenarioId => string.IsNullOrEmpty(_save.cur) ? null : _save.cur;

        /// <summary>SetValue / Commit / Terminate calls the LMS rejected during the last public operation.</summary>
        public IReadOnlyList<ScormCallInfo> LastOperationFailures => _failures;

        public bool LastOperationSucceeded => _failures.Count == 0;

        public float? GlobalScaled => ScenarioScoring.GlobalScaled(_catalog.GlobalScoreMethod, _scenarios);

        public float? GlobalRaw => GlobalScaled.HasValue ? ScenarioScoring.ToGlobalRaw(GlobalScaled.Value) : (float?)null;

        public StudentRecord.SuccessStatusType GlobalSuccess => ScenarioScoring.GlobalSuccess(_catalog, _scenarios, GlobalScaled);

        public StudentRecord.CompletionStatusType GlobalCompletion => ScenarioScoring.GlobalCompletion(_scenarios);

        public float ProgressMeasure => ScenarioScoring.ProgressMeasure(_scenarios);

        public int CompletedCount
        {
            get
            {
                int count = 0;
                foreach (ScenarioProgress scenario in _scenarios)
                    if (scenario.IsCompleted)
                        count++;
                return count;
            }
        }

        public TimeSpan SessionDuration => IsInitialized ? _utcNow() - _sessionStartUtc : TimeSpan.Zero;

        /// <summary>The JSON that is (or will be) stored in cmi.suspend_data.</summary>
        public string SuspendDataJson
        {
            get
            {
                // "v" defaults to 0 so a JSON without it is detected as unversioned on load; it is set when saving.
                _save.v = ScenarioSaveData.CurrentVersion;
                return JsonUtility.ToJson(_save);
            }
        }

        public ScenarioProgress Get(string scenarioId)
        {
            foreach (ScenarioProgress scenario in _scenarios)
                if (scenario.Id == scenarioId)
                    return scenario;
            return null;
        }

        /// <summary>
        /// Loads the state of the current attempt: cmi.suspend_data if it holds a valid JSON of this format, otherwise it
        /// is rebuilt from the cmi.objectives whose id is a scenario of the catalog. Writes nothing to the LMS.
        /// </summary>
        /// <exception cref="ScenarioTrackerException">ScormManager has not been initialized.</exception>
        public void Initialize()
        {
            if (!ScormManager.IsInitialized)
                throw new ScenarioTrackerException(ScenarioTrackerError.ScormNotInitialized, "ScenarioTracker.Initialize: wait for Scorm_Initialize_Complete first.");

            _failures.Clear();
            IsClosed = false;
            RestoreWarning = null;
            RestoreIssue = ScenarioRestoreIssue.None;
            _startedThisSession.Clear();
            _sessionStartUtc = _utcNow();
            _knownSuspendData = ScormManager.GetSuspendData() ?? "";
            _knownLocation = ScormManager.GetLocation() ?? "";

            ScenarioSaveData loaded = TryParse(_knownSuspendData, out ScenarioRestoreIssue issue, out string warning);
            RestoreIssue = issue;
            RestoreWarning = warning;
            if (loaded != null)
            {
                _save = loaded;
                RestoreSource = ScenarioRestoreSource.SuspendData;
            }
            else
            {
                _save = new ScenarioSaveData();
                RestoreSource = RebuildFromObjectives() ? ScenarioRestoreSource.Objectives : ScenarioRestoreSource.Empty;
            }

            _scenarios.Clear();
            foreach (ScenarioDefinition definition in _catalog.Scenarios)
                _scenarios.Add(new ScenarioProgress(definition, _catalog, FindEntry(definition.Id) ?? new ScenarioSaveEntry { id = definition.Id }));

            IsInitialized = true;
            RaiseChanged();
        }

        /// <summary>
        /// Marks an attempt of the scenario as started: objective with completion_status = incomplete (if it was never
        /// completed), cmi.completion_status = incomplete while scenarios are pending, cmi.location and cmi.suspend_data.
        /// </summary>
        /// <exception cref="ScenarioTrackerException">Not initialized, session closed or unknown scenario id.</exception>
        public void BeginScenario(string scenarioId)
        {
            ScenarioProgress scenario = Require(scenarioId);
            using (Capture())
            {
                StartAttempt(scenario);
                WriteState();
            }
            RaiseChanged();
        }

        /// <summary>
        /// Records one decision of the current attempt as a cmi.interactions entry linked to the scenario objective
        /// (starts a new attempt if BeginScenario was not called in this session, e.g. after resuming a half-played attempt).
        /// Interaction id: "&lt;scenarioId&gt;-a&lt;attempt started&gt;-&lt;decisionId&gt;".
        /// </summary>
        /// <param name="scenarioId">Scenario id.</param>
        /// <param name="decisionId">Short id of the decision, without spaces (e.g. "d1" or "extintor").</param>
        /// <param name="type">Interaction type; the response and pattern must use its SCORM format (true-false: "true"/"false",
        /// choice: ids joined with "[,]").</param>
        /// <param name="response">cmi.interactions.m.learner_response.</param>
        /// <param name="result">correct, incorrect, neutral...</param>
        /// <param name="correctPattern">cmi.interactions.m.correct_responses.0.pattern (null = none).</param>
        /// <param name="latencySeconds">Time to answer (0 = not measured).</param>
        /// <param name="description">Text of the question (cut to 250 characters).</param>
        /// <returns>Index of the interaction in the LMS, or -1 if the LMS rejected it.</returns>
        public int RecordDecision(string scenarioId, string decisionId, StudentRecord.InteractionType type, string response,
            StudentRecord.ResultType result, string correctPattern, float latencySeconds, string description = null)
        {
            ScenarioProgress scenario = Require(scenarioId);
            if (string.IsNullOrEmpty(decisionId))
                throw new ArgumentException("decisionId is empty.", nameof(decisionId));
            foreach (char c in decisionId)
                if (char.IsWhiteSpace(c))
                    throw new ArgumentException("decisionId '" + decisionId + "' contains whitespace.", nameof(decisionId));

            int index;
            using (Capture())
            {
                if (!scenario.IsAttemptInProgress || !_startedThisSession.Contains(scenario.Id))
                {
                    StartAttempt(scenario);
                    WriteState();
                }
                scenario.Entry.d++;

                StudentRecord.LearnerInteractionRecord interaction = new StudentRecord.LearnerInteractionRecord();
                interaction.id = scenario.Id + "-a" + Math.Max(1, scenario.Entry.a) + "-" + decisionId;
                interaction.type = type;
                interaction.timeStamp = DateTime.SpecifyKind(_utcNow(), DateTimeKind.Utc).ToLocalTime();
                interaction.weighting = 1f;
                interaction.response = response;
                interaction.result = result;
                interaction.latency = latencySeconds > 0f ? latencySeconds : 0f;
                interaction.description = Truncate(description, ScenarioDefinition.MaxDescriptionLength);
                interaction.objectives = new List<StudentRecord.LearnerInteractionObjective>();
                interaction.correctResponses = new List<StudentRecord.LearnerInteractionCorrectResponse>();
                if (!string.IsNullOrEmpty(correctPattern))
                    interaction.correctResponses.Add(new StudentRecord.LearnerInteractionCorrectResponse { pattern = correctPattern });

                index = ScormManager.RecordInteraction(interaction, scenario.Id);
            }
            RaiseChanged();
            return index;
        }

        /// <summary>
        /// Completes the current attempt with rawScore (in the scenario range): updates attempts and best/last score,
        /// writes the scenario objective with the score chosen by the catalog policy, recomputes the global result
        /// (cmi.score.*, success/completion, progress_measure), saves suspend_data and commits if AutoCommit.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">rawScore is outside min..max of the scenario.</exception>
        public ScenarioProgress CompleteScenario(string scenarioId, float rawScore)
        {
            ScenarioProgress scenario = Require(scenarioId);
            ScenarioDefinition definition = scenario.Definition;
            if (float.IsNaN(rawScore) || float.IsInfinity(rawScore) || rawScore < definition.MinScore || rawScore > definition.MaxScore)
                throw new ArgumentOutOfRangeException(nameof(rawScore), rawScore,
                    "Scenario '" + scenarioId + "': the score must be between " + definition.MinScore + " and " + definition.MaxScore + ".");

            using (Capture())
            {
                ScenarioSaveEntry entry = scenario.Entry;
                EnsureSaved(entry);
                if (entry.t0 == 0)
                    entry.t0 = UnixNow();
                entry.b = entry.n == 0 ? rawScore : Math.Max(entry.b, rawScore);
                entry.l = rawScore;
                entry.n++;
                entry.p = 0;
                entry.d = 0;
                entry.t1 = UnixNow();
                _save.cur = scenario.Id;

                ScormManager.UpsertObjective(CompletedObjective(scenario));

                WriteGlobals();
                WriteState();
                if (AutoCommit)
                    ScormManager.Commit();
            }
            ScenarioCompleted?.Invoke(scenario);
            RaiseChanged();
            return scenario;
        }

        /// <summary>
        /// Demo/testing helper: forgets attempts and scores in cmi.suspend_data (writes an empty state of the current
        /// format). It cannot delete cmi.objectives, cmi.interactions nor cmi.score in the LMS: they keep their values
        /// until a scenario is completed again.
        /// </summary>
        public void ResetLocalState()
        {
            RequireOpen();
            using (Capture())
            {
                _save = new ScenarioSaveData();
                foreach (ScenarioProgress scenario in _scenarios)
                    scenario.Entry = FindEntry(scenario.Id) ?? new ScenarioSaveEntry { id = scenario.Id };
                WriteIfChanged("cmi.suspend_data", SuspendDataJson, ref _knownSuspendData, ScormManager.SetSuspendData);
                if (AutoCommit)
                    ScormManager.Commit();
            }
            RaiseChanged();
        }

        /// <summary>Commits the data to the LMS.</summary>
        /// <returns>False if the LMS rejected the commit (see LastOperationFailures).</returns>
        public bool Commit()
        {
            RequireOpen();
            using (Capture())
                ScormManager.Commit();
            RaiseChanged();
            return LastOperationSucceeded;
        }

        /// <summary>
        /// Leaves the SCO to come back later: rewrites what the LMS rejected before (objectives, global result, state),
        /// session_time, exit = suspend, Commit, Terminate. If the Commit is rejected, Terminate is not called and the
        /// tracker stays open (IsClosed = false) so the app can retry.
        /// </summary>
        /// <returns>False if the LMS rejected any of those calls (see LastOperationFailures).</returns>
        public bool Suspend()
        {
            return Close(StudentRecord.ExitType.suspend);
        }

        /// <summary>Ends the attempt: same as Suspend with exit = normal.</summary>
        /// <returns>False if the LMS rejected any of those calls (see LastOperationFailures).</returns>
        public bool Finish()
        {
            return Close(StudentRecord.ExitType.normal);
        }

        private bool Close(StudentRecord.ExitType exit)
        {
            RequireOpen();
            bool terminated = false;
            using (Capture())
            {
                // Retry writes the LMS rejected earlier; values it already has are skipped by WriteIfChanged.
                if (CompletedCount > 0)
                {
                    foreach (ScenarioProgress scenario in _scenarios)
                        if (scenario.IsCompleted)
                            ScormManager.UpsertObjective(CompletedObjective(scenario));
                    WriteGlobals();
                }
                WriteState();
                ScormManager.SetSessionTime((float)Math.Max(0d, SessionDuration.TotalSeconds));
                ScormManager.SetExit(exit);
                if (!Failed(ScormCallOperation.Commit, () => ScormManager.Commit()))
                    terminated = !Failed(ScormCallOperation.Terminate, () => ScormManager.Terminate());
            }
            if (terminated)
            {
                IsClosed = true;
                SessionClosed?.Invoke(exit);
            }
            RaiseChanged();
            return LastOperationSucceeded && terminated;
        }

        private bool Failed(ScormCallOperation operation, Action call)
        {
            int before = _failures.Count;
            call();
            for (int i = before; i < _failures.Count; i++)
                if (_failures[i].Operation == operation)
                    return true;
            return false;
        }

        private static ScormObjectiveData CompletedObjective(ScenarioProgress scenario)
        {
            ScenarioDefinition definition = scenario.Definition;
            float reportedRaw = scenario.ReportedRaw.Value;
            ScormObjectiveData objective = new ScormObjectiveData(scenario.Id);
            objective.score = new ScormScoreData(reportedRaw, definition.MinScore, definition.MaxScore,
                (float)Math.Round(ScenarioScoring.ToScaled(definition, reportedRaw), 4));
            objective.successStatus = scenario.IsPassed ? StudentRecord.SuccessStatusType.passed : StudentRecord.SuccessStatusType.failed;
            objective.completionStatus = StudentRecord.CompletionStatusType.completed;
            objective.progressMeasure = 1f;
            objective.description = definition.ScormDescription;
            return objective;
        }

        private void StartAttempt(ScenarioProgress scenario)
        {
            _startedThisSession.Add(scenario.Id);
            ScenarioSaveEntry entry = scenario.Entry;
            EnsureSaved(entry);
            entry.p = 1;
            entry.a++;
            entry.d = 0;
            if (entry.t0 == 0)
                entry.t0 = UnixNow();
            _save.cur = scenario.Id;

            if (!scenario.IsCompleted)
            {
                ScormObjectiveData objective = new ScormObjectiveData(scenario.Id);
                objective.completionStatus = StudentRecord.CompletionStatusType.incomplete;
                objective.description = scenario.Definition.ScormDescription;
                ScormManager.UpsertObjective(objective);
            }
            if (GlobalCompletion != StudentRecord.CompletionStatusType.completed)
                ScormManager.UpdateStatus(StudentRecord.SuccessStatusType.not_set, StudentRecord.CompletionStatusType.incomplete);
        }

        private void WriteGlobals()
        {
            float? scaled = GlobalScaled;
            if (scaled.HasValue)
            {
                float raw = ScenarioScoring.ToGlobalRaw(scaled.Value);
                float roundedScaled = (float)Math.Round(raw / (ScenarioScoring.GlobalMax - ScenarioScoring.GlobalMin), 4);
                ScormManager.UpdateScore(new ScormScoreData(raw, ScenarioScoring.GlobalMin, ScenarioScoring.GlobalMax, roundedScaled));
            }
            ScormManager.UpdateStatus(GlobalSuccess, GlobalCompletion);
            ScormManager.UpdateProgressMeasure(ProgressMeasure);
        }

        private void WriteState()
        {
            string json = SuspendDataJson;
            if (json.Length > MaxSuspendDataLength)
                throw new ScenarioTrackerException(ScenarioTrackerError.SuspendDataTooLarge, "cmi.suspend_data would be " + json.Length + " characters (max " + MaxSuspendDataLength + ").");
            if (json.Length > PortableSuspendDataLength && !_warnedSuspendDataSize)
            {
                _warnedSuspendDataSize = true;
                Debug.LogWarning("[SCORM] cmi.suspend_data is " + json.Length + " characters: above the 4000 a SCORM 2004 3rd Edition LMS must keep (4th Edition: 64000).");
            }
            WriteIfChanged("cmi.location", _save.cur, ref _knownLocation, ScormManager.SetLocation);
            WriteIfChanged("cmi.suspend_data", json, ref _knownSuspendData, ScormManager.SetSuspendData);
        }

        private void WriteIfChanged(string element, string value, ref string known, Action<string> setter)
        {
            if (string.IsNullOrEmpty(value) || value == known)
                return;
            int failuresBefore = _failures.Count;
            setter(value);
            bool rejected = false;
            for (int i = failuresBefore; i < _failures.Count; i++)
                if (_failures[i].Element == element)
                    rejected = true;
            if (!rejected)
                known = value;
        }

        private bool RebuildFromObjectives()
        {
            List<StudentRecord.Objectives> objectives = ScormManager.GetObjectives();
            if (objectives == null)
                return false;
            bool any = false;
            foreach (ScenarioDefinition definition in _catalog.Scenarios)
            {
                StudentRecord.Objectives objective = objectives.Find(o => o != null && o.id == definition.Id);
                if (objective == null)
                    continue;
                ScenarioSaveEntry entry = new ScenarioSaveEntry { id = definition.Id };
                if (objective.completionStatus == StudentRecord.CompletionStatusType.completed && objective.score != null)
                {
                    float raw = Mathf.Clamp(objective.score.raw, definition.MinScore, definition.MaxScore);
                    entry.n = 1;
                    entry.a = 1;
                    entry.b = raw;
                    entry.l = raw;
                }
                else if (objective.completionStatus == StudentRecord.CompletionStatusType.incomplete)
                {
                    entry.p = 1;
                    entry.a = 1;
                }
                else
                {
                    continue;
                }
                _save.s.Add(entry);
                any = true;
            }
            string location = ScormManager.GetLocation();
            if (any && !string.IsNullOrEmpty(location) && _catalog.Find(location) != null)
                _save.cur = location;
            return any;
        }

        private static ScenarioSaveData TryParse(string json, out ScenarioRestoreIssue issue, out string warning)
        {
            issue = ScenarioRestoreIssue.None;
            warning = null;
            if (string.IsNullOrEmpty(json))
                return null;
            if (json.TrimStart().Length == 0 || json.TrimStart()[0] != '{')
            {
                issue = ScenarioRestoreIssue.NotJson;
                warning = "cmi.suspend_data is not JSON of this tracker; state rebuilt from cmi.objectives.";
                return null;
            }
            ScenarioSaveData data;
            try
            {
                data = JsonUtility.FromJson<ScenarioSaveData>(json);
            }
            catch (ArgumentException e)
            {
                issue = ScenarioRestoreIssue.ParseError;
                warning = "cmi.suspend_data could not be parsed (" + e.Message + "); state rebuilt from cmi.objectives.";
                return null;
            }
            if (data == null || data.v <= 0)
            {
                issue = ScenarioRestoreIssue.NoVersion;
                warning = "cmi.suspend_data has no format version; state rebuilt from cmi.objectives.";
                return null;
            }
            if (data.v > ScenarioSaveData.CurrentVersion)
            {
                issue = ScenarioRestoreIssue.NewerVersion;
                warning = "cmi.suspend_data has format version " + data.v + " (this build reads up to " + ScenarioSaveData.CurrentVersion + "); state rebuilt from cmi.objectives.";
                return null;
            }
            if (data.s == null)
                data.s = new List<ScenarioSaveEntry>();
            data.s.RemoveAll(e => e == null || string.IsNullOrEmpty(e.id));
            if (data.cur == null)
                data.cur = "";
            return data;
        }

        private ScenarioSaveEntry FindEntry(string id)
        {
            foreach (ScenarioSaveEntry entry in _save.s)
                if (entry.id == id)
                    return entry;
            return null;
        }

        private void EnsureSaved(ScenarioSaveEntry entry)
        {
            if (!_save.s.Contains(entry))
                _save.s.Add(entry);
        }

        private ScenarioProgress Require(string scenarioId)
        {
            RequireOpen();
            ScenarioProgress scenario = Get(scenarioId);
            if (scenario == null)
                throw new ScenarioTrackerException(ScenarioTrackerError.UnknownScenario, "Unknown scenario id '" + scenarioId + "' (not in catalog '" + _catalog.name + "').");
            return scenario;
        }

        private void RequireOpen()
        {
            if (!IsInitialized)
                throw new ScenarioTrackerException(ScenarioTrackerError.NotInitialized, "ScenarioTracker is not initialized.");
            if (IsClosed)
                throw new ScenarioTrackerException(ScenarioTrackerError.SessionClosed, "The SCORM session is closed (Suspend/Finish was called); relaunch the SCO.");
        }

        private long UnixNow()
        {
            DateTime now = _utcNow();
            return new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc)).ToUnixTimeSeconds();
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text))
                return null;
            return text.Length <= max ? text : text.Substring(0, max);
        }

        private void RaiseChanged()
        {
            Changed?.Invoke();
        }

        private IDisposable Capture()
        {
            if (_capturing)
                return NoopScope.Instance;
            _failures.Clear();
            _capturing = true;
            ScormManager.ScormCall += OnScormCall;
            return new CaptureScope(this);
        }

        private void EndCapture()
        {
            ScormManager.ScormCall -= OnScormCall;
            _capturing = false;
        }

        private void OnScormCall(ScormCallInfo info)
        {
            if (info.Operation != ScormCallOperation.GetValue && !info.Succeeded)
                _failures.Add(info);
        }

        private sealed class CaptureScope : IDisposable
        {
            private ScenarioTracker _owner;

            public CaptureScope(ScenarioTracker owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                if (_owner == null)
                    return;
                _owner.EndCapture();
                _owner = null;
            }
        }

        private sealed class NoopScope : IDisposable
        {
            public static readonly NoopScope Instance = new NoopScope();

            public void Dispose() { }
        }
    }
}
