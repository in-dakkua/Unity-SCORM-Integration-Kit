using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Scorm.Scenarios;
using UnityEngine;

/// <summary>
/// ScenarioTracker (one SCO, one cmi.objectives entry per scenario, weighted global score) against the in-memory
/// Editor backend, which behaves like a strict LMS (id first, contiguous indexes, resume after exit = suspend).
/// </summary>
public class ScenarioTrackerTests
{
    private CultureInfo _previousCulture;
    private readonly List<ScormCallInfo> _calls = new List<ScormCallInfo>();
    private readonly List<UnityEngine.Object> _assets = new List<UnityEngine.Object>();
    private DateTime _now;

    [SetUp]
    public void SetUp()
    {
        _previousCulture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo("es-ES");
        _now = new DateTime(2026, 10, 6, 9, 0, 0, DateTimeKind.Utc);
        ScormEditorBackend.Reset();
        ScormManager.ScormCall += Record;
        ScormManager.Initialize();
        _calls.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        ScormManager.ScormCall -= Record;
        ScormEditorBackend.ClearInjectedErrors();
        Thread.CurrentThread.CurrentCulture = _previousCulture;
        foreach (UnityEngine.Object asset in _assets)
            UnityEngine.Object.DestroyImmediate(asset);
        _assets.Clear();
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------------------------------

    private void Record(ScormCallInfo info)
    {
        _calls.Add(info);
    }

    private List<ScormCallInfo> Sets()
    {
        return _calls.Where(c => c.Operation == ScormCallOperation.SetValue).ToList();
    }

    private static string Value(string key)
    {
        return ScormEditorBackend.Data.TryGetValue(key, out string v) ? v : null;
    }

    private ScenarioDefinition Def(string id, float weight = 1f, float min = 0f, float max = 100f, float passing = 0.7f)
    {
        ScenarioDefinition definition = ScriptableObject.CreateInstance<ScenarioDefinition>();
        definition.name = id;
        definition.Configure(id, "Título " + id, "Descripción de " + id, weight, min, max, passing, 3);
        _assets.Add(definition);
        return definition;
    }

    private ScenarioCatalog Catalog(ScenarioScorePolicy policy = ScenarioScorePolicy.Best,
        GlobalScoreMethod method = GlobalScoreMethod.WeightedAverageAllScenarios, float globalPassing = 0.7f,
        bool requireAll = false, params ScenarioDefinition[] scenarios)
    {
        if (scenarios.Length == 0)
            scenarios = new[] { Def("scenario-01", 1f), Def("scenario-02", 2f, 0f, 10f, 0.8f), Def("scenario-03", 1f) };
        ScenarioCatalog catalog = ScriptableObject.CreateInstance<ScenarioCatalog>();
        catalog.name = "TestCatalog";
        catalog.Configure(scenarios, policy, method, globalPassing, requireAll);
        _assets.Add(catalog);
        return catalog;
    }

    private ScenarioTracker NewTracker(ScenarioCatalog catalog)
    {
        ScenarioTracker tracker = new ScenarioTracker(catalog, () => _now);
        tracker.Initialize();
        return tracker;
    }

    private static int ObjectiveIndex(string id)
    {
        int count = int.Parse(Value("cmi.objectives._count"), CultureInfo.InvariantCulture);
        for (int i = 0; i < count; i++)
            if (Value("cmi.objectives." + i + ".id") == id)
                return i;
        return -1;
    }

    private static string Objective(string id, string field)
    {
        int index = ObjectiveIndex(id);
        Assert.GreaterOrEqual(index, 0, "objective " + id + " not found");
        return Value("cmi.objectives." + index + "." + field);
    }

    private void AssertNoEmptySets()
    {
        foreach (ScormCallInfo set in Sets())
            Assert.IsFalse(string.IsNullOrEmpty(set.Value), "Empty value sent to " + set.Element);
    }

    private static int CountObjectivesWithId(string id)
    {
        int count = int.Parse(Value("cmi.objectives._count"), CultureInfo.InvariantCulture);
        int found = 0;
        for (int i = 0; i < count; i++)
            if (Value("cmi.objectives." + i + ".id") == id)
                found++;
        return found;
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Initialize
    // ---------------------------------------------------------------------------------------------------------------

    [Test]
    public void Initialize_WritesNothing_AndIgnoresSeedSuspendDataThatIsNotJson()
    {
        ScenarioTracker tracker = NewTracker(Catalog());

        CollectionAssert.IsEmpty(Sets(), "Initialize must not write");
        Assert.AreEqual(0, _calls.Count(c => c.Operation == ScormCallOperation.Commit));
        Assert.AreEqual("empty", tracker.RestoredFrom);
        Assert.AreEqual(ScenarioRestoreSource.Empty, tracker.RestoreSource);
        Assert.AreEqual(ScenarioRestoreIssue.NotJson, tracker.RestoreIssue);
        Assert.IsNotNull(tracker.RestoreWarning);
        Assert.AreEqual(3, tracker.Scenarios.Count);
        Assert.IsTrue(tracker.Scenarios.All(s => s.Status == ScenarioStatus.NotStarted));
        Assert.IsNull(tracker.GlobalRaw);
        Assert.AreEqual(StudentRecord.SuccessStatusType.unknown, tracker.GlobalSuccess);
        Assert.AreEqual(StudentRecord.CompletionStatusType.incomplete, tracker.GlobalCompletion);
    }

    [Test]
    public void Constructor_InvalidCatalog_Throws()
    {
        Assert.Throws<ScenarioTrackerException>(() => new ScenarioTracker(null));
        ScenarioCatalog duplicated = Catalog(ScenarioScorePolicy.Best, GlobalScoreMethod.WeightedAverageAllScenarios, 0.7f, false,
            Def("scenario-01"), Def("scenario-01"));
        Assert.Throws<ScenarioTrackerException>(() => new ScenarioTracker(duplicated));
        ScenarioCatalog withSpaces = Catalog(ScenarioScorePolicy.Best, GlobalScoreMethod.WeightedAverageAllScenarios, 0.7f, false,
            Def("scenario 01"));
        Assert.Throws<ScenarioTrackerException>(() => new ScenarioTracker(withSpaces));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Scenario objectives
    // ---------------------------------------------------------------------------------------------------------------

    [Test]
    public void BeginScenario_WritesIncompleteObjectiveIdFirst_LocationAndSuspendData()
    {
        ScenarioTracker tracker = NewTracker(Catalog());

        tracker.BeginScenario("scenario-01");

        List<ScormCallInfo> sets = Sets();
        Assert.AreEqual("cmi.objectives.2.id", sets[0].Element);
        Assert.AreEqual("scenario-01", sets[0].Value);
        Assert.AreEqual("incomplete", Objective("scenario-01", "completion_status"));
        Assert.AreEqual("Descripción de scenario-01", Objective("scenario-01", "description"));
        Assert.AreEqual("scenario-01", Value("cmi.location"));
        StringAssert.StartsWith("{", Value("cmi.suspend_data"));
        Assert.IsFalse(sets.Any(s => s.Element == "cmi.completion_status"), "seed is already incomplete: no redundant write");
        Assert.AreEqual(ScenarioStatus.InProgress, tracker.Get("scenario-01").Status);
        Assert.IsTrue(sets.All(s => s.Succeeded));
        AssertNoEmptySets();
    }

    [Test]
    public void CompleteScenario_WritesObjectiveWithStableIdScoreAndStatuses_AndCommits()
    {
        ScenarioTracker tracker = NewTracker(Catalog());
        tracker.BeginScenario("scenario-01");
        int commitsBefore = ScormEditorBackend.CommitCount;

        tracker.CompleteScenario("scenario-01", 80f);

        Assert.AreEqual(2, ObjectiveIndex("scenario-01"), "seed objectives keep indexes 0 and 1");
        Assert.AreEqual("80", Objective("scenario-01", "score.raw"));
        Assert.AreEqual("0", Objective("scenario-01", "score.min"));
        Assert.AreEqual("100", Objective("scenario-01", "score.max"));
        Assert.AreEqual("0.8", Objective("scenario-01", "score.scaled"));
        Assert.AreEqual("passed", Objective("scenario-01", "success_status"));
        Assert.AreEqual("completed", Objective("scenario-01", "completion_status"));
        Assert.AreEqual("1", Objective("scenario-01", "progress_measure"));
        Assert.AreEqual("Objective1", Value("cmi.objectives.0.id"));
        Assert.AreEqual("45.0", Value("cmi.objectives.0.score.raw"), "seed objective untouched");
        Assert.AreEqual(commitsBefore + 1, ScormEditorBackend.CommitCount, "AutoCommit after completing a scenario");
        Assert.IsTrue(tracker.LastOperationSucceeded);
        AssertNoEmptySets();
    }

    [Test]
    public void CompleteScenario_OwnScoreRange_ScaledIsNormalizedAndUsesScenarioThreshold()
    {
        ScenarioTracker tracker = NewTracker(Catalog());

        tracker.CompleteScenario("scenario-02", 7.5f);

        Assert.AreEqual("7.5", Objective("scenario-02", "score.raw"));
        Assert.AreEqual("10", Objective("scenario-02", "score.max"));
        Assert.AreEqual("0.75", Objective("scenario-02", "score.scaled"));
        Assert.AreEqual("failed", Objective("scenario-02", "success_status"), "threshold of scenario-02 is 0.8");
        Assert.AreEqual(ScenarioStatus.Failed, tracker.Get("scenario-02").Status);
    }

    [Test]
    public void ScorePolicyBest_KeepsHighestAttemptInLms()
    {
        ScenarioTracker tracker = NewTracker(Catalog(ScenarioScorePolicy.Best));
        tracker.CompleteScenario("scenario-01", 80f);
        _calls.Clear();

        tracker.CompleteScenario("scenario-01", 60f);

        ScenarioProgress progress = tracker.Get("scenario-01");
        Assert.AreEqual(2, progress.Attempts);
        Assert.AreEqual(80f, progress.BestRaw);
        Assert.AreEqual(60f, progress.LastRaw);
        Assert.AreEqual(80f, progress.ReportedRaw);
        Assert.AreEqual("80", Objective("scenario-01", "score.raw"));
        Assert.IsFalse(Sets().Any(s => s.Element.StartsWith("cmi.objectives.", StringComparison.Ordinal)), "best score unchanged: nothing to write");
        Assert.IsFalse(Sets().Any(s => s.Element.StartsWith("cmi.score.", StringComparison.Ordinal)), "global unchanged: nothing to write");
        Assert.IsTrue(Sets().Any(s => s.Element == "cmi.suspend_data"), "attempt count changed");
    }

    [Test]
    public void ScorePolicyLast_ReportsMostRecentAttempt()
    {
        ScenarioTracker tracker = NewTracker(Catalog(ScenarioScorePolicy.Last));
        tracker.CompleteScenario("scenario-01", 80f);

        tracker.CompleteScenario("scenario-01", 60f);

        Assert.AreEqual(60f, tracker.Get("scenario-01").ReportedRaw);
        Assert.AreEqual("60", Objective("scenario-01", "score.raw"));
        Assert.AreEqual("0.6", Objective("scenario-01", "score.scaled"));
        Assert.AreEqual("failed", Objective("scenario-01", "success_status"));
        Assert.AreEqual(1, CountObjectivesWithId("scenario-01"));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Global result
    // ---------------------------------------------------------------------------------------------------------------

    [Test]
    public void GlobalScore_WeightedAverageOverAllScenarios_RawIs0To100()
    {
        // weights 1, 2, 1
        ScenarioTracker tracker = NewTracker(Catalog());

        tracker.CompleteScenario("scenario-01", 100f);

        Assert.AreEqual("25", Value("cmi.score.raw"), "1*1 / (1+2+1); pending scenarios count as 0; seed objectives ignored");
        Assert.AreEqual(0f, ScormFormat.ParseReal(Value("cmi.score.min")), "seed 0.0 is numerically equal: not rewritten");
        Assert.AreEqual(100f, ScormFormat.ParseReal(Value("cmi.score.max")));
        Assert.IsFalse(Sets().Any(s => s.Element == "cmi.score.min" || s.Element == "cmi.score.max"), "no redundant writes");
        Assert.AreEqual("0.25", Value("cmi.score.scaled"));
        Assert.AreEqual("unknown", Value("cmi.success_status"));
        Assert.AreEqual("incomplete", Value("cmi.completion_status"));
        Assert.AreEqual("0.3333", Value("cmi.progress_measure"));

        tracker.CompleteScenario("scenario-02", 5f);
        tracker.CompleteScenario("scenario-03", 70f);

        Assert.AreEqual("67.5", Value("cmi.score.raw"), "(1*1 + 2*0.5 + 1*0.7) / 4");
        Assert.AreEqual("0.675", Value("cmi.score.scaled"));
        Assert.AreEqual("failed", Value("cmi.success_status"), "every scenario completed and 0.675 < 0.7");
        Assert.AreEqual("completed", Value("cmi.completion_status"));
        Assert.AreEqual("1", Value("cmi.progress_measure"));
        Assert.AreEqual(67.5f, tracker.GlobalRaw);
        AssertNoEmptySets();
    }

    [Test]
    public void GlobalSuccess_PassedOnlyWhenAllCompletedAndAboveThreshold()
    {
        ScenarioTracker tracker = NewTracker(Catalog());
        tracker.CompleteScenario("scenario-01", 100f);
        tracker.CompleteScenario("scenario-02", 10f);
        Assert.AreEqual("unknown", Value("cmi.success_status"), "75 >= 70 but scenario-03 is pending");

        tracker.CompleteScenario("scenario-03", 40f);

        Assert.AreEqual("85", Value("cmi.score.raw"));
        Assert.AreEqual("passed", Value("cmi.success_status"));
        Assert.AreEqual("completed", Value("cmi.completion_status"));
    }

    [Test]
    public void GlobalSuccess_RequireAllScenariosPassed_FailsWithAFailedScenario()
    {
        ScenarioTracker tracker = NewTracker(Catalog(ScenarioScorePolicy.Best, GlobalScoreMethod.WeightedAverageAllScenarios, 0.7f, true));
        tracker.CompleteScenario("scenario-01", 100f);
        tracker.CompleteScenario("scenario-02", 10f);

        tracker.CompleteScenario("scenario-03", 40f);

        Assert.AreEqual("85", Value("cmi.score.raw"));
        Assert.AreEqual("failed", Value("cmi.success_status"), "scenario-03 (40 < 70) is failed");
    }

    [Test]
    public void GlobalScore_CompletedOnly_AveragesCompletedScenarios()
    {
        ScenarioTracker tracker = NewTracker(Catalog(ScenarioScorePolicy.Best, GlobalScoreMethod.WeightedAverageCompletedOnly));

        tracker.CompleteScenario("scenario-01", 80f);
        Assert.AreEqual("80", Value("cmi.score.raw"));

        tracker.CompleteScenario("scenario-02", 5f);
        Assert.AreEqual("60", Value("cmi.score.raw"), "(1*0.8 + 2*0.5) / 3");
    }

    [Test]
    public void ScenarioScoring_AllWeightsZero_ScenariosCountEqually()
    {
        ScenarioTracker tracker = NewTracker(Catalog(ScenarioScorePolicy.Best, GlobalScoreMethod.WeightedAverageAllScenarios, 0.7f, false,
            Def("scenario-01", 0f), Def("scenario-02", 0f)));

        tracker.CompleteScenario("scenario-01", 50f);

        Assert.AreEqual(25f, tracker.GlobalRaw);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Interactions
    // ---------------------------------------------------------------------------------------------------------------

    [Test]
    public void RecordDecision_WritesInteractionLinkedToScenarioObjective()
    {
        ScenarioTracker tracker = NewTracker(Catalog());
        tracker.BeginScenario("scenario-01");

        int index = tracker.RecordDecision("scenario-01", "d1", StudentRecord.InteractionType.true_false, "false",
            StudentRecord.ResultType.incorrect, "true", 12.5f, "¿Es obligatorio el casco?");
        int second = tracker.RecordDecision("scenario-01", "d2", StudentRecord.InteractionType.choice, "opt-b",
            StudentRecord.ResultType.correct, "opt-b", 4f);

        Assert.AreEqual(1, index, "seed has one interaction");
        Assert.AreEqual(2, second);
        string p = "cmi.interactions.1.";
        Assert.AreEqual("scenario-01-a1-d1", Value(p + "id"));
        Assert.AreEqual("true-false", Value(p + "type"));
        Assert.AreEqual("scenario-01", Value(p + "objectives.0.id"));
        Assert.AreEqual("1", Value(p + "objectives._count"));
        Assert.AreEqual("false", Value(p + "learner_response"));
        Assert.AreEqual("incorrect", Value(p + "result"));
        Assert.AreEqual("true", Value(p + "correct_responses.0.pattern"));
        Assert.AreEqual("P0DT0H0M12.5S", Value(p + "latency"));
        Assert.AreEqual("¿Es obligatorio el casco?", Value(p + "description"));
        Assert.AreEqual("scenario-01-a1-d2", Value("cmi.interactions.2.id"));
        Assert.AreEqual("choice", Value("cmi.interactions.2.type"));
        Assert.AreEqual("scenario-01", Value("cmi.interactions.2.objectives.0.id"));
        Assert.IsTrue(Sets().All(s => s.Succeeded), string.Join("\n", Sets().Where(s => !s.Succeeded).Select(s => s.ToString()).ToArray()));
        AssertNoEmptySets();
    }

    [Test]
    public void RecordDecision_SecondAttempt_UsesNewAttemptNumberInId()
    {
        ScenarioTracker tracker = NewTracker(Catalog());
        tracker.RecordDecision("scenario-01", "d1", StudentRecord.InteractionType.true_false, "true", StudentRecord.ResultType.correct, "true", 1f);
        tracker.CompleteScenario("scenario-01", 90f);

        tracker.RecordDecision("scenario-01", "d1", StudentRecord.InteractionType.true_false, "true", StudentRecord.ResultType.correct, "true", 1f);

        Assert.AreEqual("scenario-01-a1-d1", Value("cmi.interactions.1.id"));
        Assert.AreEqual("scenario-01-a2-d1", Value("cmi.interactions.2.id"));
        Assert.IsTrue(tracker.Get("scenario-01").IsAttemptInProgress, "RecordDecision starts the attempt");
    }

    [Test]
    public void RecordDecision_AttemptRestartedAfterResume_DoesNotReuseInteractionIds()
    {
        ScenarioCatalog catalog = Catalog();
        ScenarioTracker first = NewTracker(catalog);
        first.BeginScenario("scenario-01");
        first.RecordDecision("scenario-01", "d1", StudentRecord.InteractionType.true_false, "true", StudentRecord.ResultType.correct, "true", 1f);
        first.Suspend();
        ScormManager.Initialize();
        ScenarioTracker second = NewTracker(catalog);

        second.BeginScenario("scenario-01");
        second.RecordDecision("scenario-01", "d1", StudentRecord.InteractionType.true_false, "true", StudentRecord.ResultType.correct, "true", 1f);

        Assert.AreEqual("scenario-01-a1-d1", Value("cmi.interactions.1.id"));
        Assert.AreEqual("scenario-01-a2-d1", Value("cmi.interactions.2.id"));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // suspend_data, resume, exit
    // ---------------------------------------------------------------------------------------------------------------

    [Test]
    public void SuspendData_IsVersionedCompactJsonWithInvariantNumbers()
    {
        ScenarioTracker tracker = NewTracker(Catalog());
        tracker.CompleteScenario("scenario-02", 7.5f);

        string json = Value("cmi.suspend_data");

        StringAssert.StartsWith("{\"v\":1", json);
        StringAssert.Contains("\"id\":\"scenario-02\"", json);
        StringAssert.Contains("\"b\":7.5", json);
        StringAssert.DoesNotContain("7,5", json);
        StringAssert.DoesNotContain("\n", json);
        Assert.AreEqual(tracker.SuspendDataJson, json);
        ScenarioSaveData parsed = JsonUtility.FromJson<ScenarioSaveData>(json);
        Assert.AreEqual(ScenarioSaveData.CurrentVersion, parsed.v);
        Assert.AreEqual(1, parsed.s.Count);
        Assert.AreEqual(1, parsed.s[0].n);
        Assert.AreEqual(new DateTimeOffset(_now).ToUnixTimeSeconds(), parsed.s[0].t1);
    }

    [Test]
    public void SuspendData_200Scenarios_StaysBelow64000AndCompact()
    {
        List<ScenarioDefinition> many = new List<ScenarioDefinition>();
        for (int i = 1; i <= 200; i++)
            many.Add(Def("scenario-" + i.ToString("000", CultureInfo.InvariantCulture)));
        ScenarioTracker tracker = NewTracker(Catalog(ScenarioScorePolicy.Best, GlobalScoreMethod.WeightedAverageAllScenarios, 0.7f, false, many.ToArray()));
        tracker.AutoCommit = false;

        foreach (ScenarioDefinition definition in many)
            tracker.CompleteScenario(definition.Id, 66.6f);

        string json = Value("cmi.suspend_data");
        Assert.Less(json.Length, ScenarioTracker.MaxSuspendDataLength);
        Assert.Less(json.Length / 200, 140, "per-scenario entry should stay compact (floats such as 66.6 serialize as 66.5999984741211): " + json.Length);
        Assert.AreEqual("completed", Value("cmi.completion_status"));
    }

    [Test]
    public void Suspend_WritesSessionTimeExitCommitTerminateInOrder_AndClosesTheTracker()
    {
        ScenarioTracker tracker = NewTracker(Catalog());
        tracker.BeginScenario("scenario-01");
        _now = _now.AddSeconds(125.5);
        _calls.Clear();

        bool ok = tracker.Suspend();

        Assert.IsTrue(ok);
        Assert.IsTrue(tracker.IsClosed);
        string[] sequence = _calls.Select(c => c.Operation + " " + c.Element).ToArray();
        CollectionAssert.AreEqual(new[] { "SetValue cmi.session_time", "SetValue cmi.exit", "Commit ", "Terminate " }, sequence);
        Assert.AreEqual("P0DT0H2M5.5S", _calls[0].Value);
        Assert.AreEqual("suspend", _calls[1].Value);
        ScenarioTrackerException closed = Assert.Throws<ScenarioTrackerException>(() => tracker.BeginScenario("scenario-02"));
        Assert.AreEqual(ScenarioTrackerError.SessionClosed, closed.Error);
        Assert.Throws<ScenarioTrackerException>(() => tracker.Commit());
    }

    [Test]
    public void SuspendAndRelaunch_RestoresStateWithoutDuplicatingObjectives()
    {
        ScenarioCatalog catalog = Catalog();
        ScenarioTracker first = NewTracker(catalog);
        first.CompleteScenario("scenario-01", 80f);
        first.CompleteScenario("scenario-01", 70f);
        first.BeginScenario("scenario-02");
        first.Suspend();

        ScormManager.Initialize();
        _calls.Clear();
        ScenarioTracker second = NewTracker(catalog);

        Assert.AreEqual(StudentRecord.EntryType.resume, ScormManager.GetEntry());
        CollectionAssert.IsEmpty(Sets(), "Initialize after resume must not write");
        Assert.AreEqual("suspend_data", second.RestoredFrom);
        Assert.IsNull(second.RestoreWarning);
        ScenarioProgress s1 = second.Get("scenario-01");
        Assert.AreEqual(2, s1.Attempts);
        Assert.AreEqual(80f, s1.BestRaw);
        Assert.AreEqual(70f, s1.LastRaw);
        Assert.AreEqual(ScenarioStatus.InProgress, second.Get("scenario-02").Status);
        Assert.AreEqual("scenario-02", second.LastScenarioId);
        Assert.AreEqual(20f, second.GlobalRaw);

        second.CompleteScenario("scenario-01", 95f);
        second.CompleteScenario("scenario-02", 9f);

        Assert.AreEqual(1, CountObjectivesWithId("scenario-01"));
        Assert.AreEqual(1, CountObjectivesWithId("scenario-02"));
        Assert.AreEqual("4", Value("cmi.objectives._count"), "2 seed + 2 scenarios");
        Assert.AreEqual("95", Objective("scenario-01", "score.raw"));
        Assert.AreEqual(3, second.Get("scenario-01").Attempts);
        AssertNoEmptySets();
    }

    [Test]
    public void FinishAndRelaunch_StartsNewAttempt()
    {
        ScenarioCatalog catalog = Catalog();
        ScenarioTracker first = NewTracker(catalog);
        first.CompleteScenario("scenario-01", 80f);
        _calls.Clear();

        Assert.IsTrue(first.Finish());
        Assert.AreEqual("normal", _calls.First(c => c.Element == "cmi.exit").Value);

        ScormManager.Initialize();
        ScenarioTracker second = NewTracker(catalog);

        Assert.AreEqual(StudentRecord.EntryType.start, ScormManager.GetEntry());
        Assert.AreEqual(0, second.Get("scenario-01").Attempts);
        Assert.AreEqual(-1, ObjectiveIndex("scenario-01"));
    }

    [Test]
    public void Resume_SuspendDataNotJson_RebuildsFromObjectives()
    {
        ScenarioCatalog catalog = Catalog();
        ScenarioTracker first = NewTracker(catalog);
        first.CompleteScenario("scenario-01", 80f);
        first.BeginScenario("scenario-03");
        Assert.IsTrue(ScormEditorBackend.SetValue("cmi.suspend_data", "lost"));
        first.Suspend();

        ScormManager.Initialize();
        ScenarioTracker second = NewTracker(catalog);

        Assert.AreEqual("objectives", second.RestoredFrom);
        Assert.IsNotNull(second.RestoreWarning);
        Assert.AreEqual(1, second.Get("scenario-01").Attempts);
        Assert.AreEqual(80f, second.Get("scenario-01").BestRaw);
        Assert.AreEqual(ScenarioStatus.InProgress, second.Get("scenario-03").Status);
        Assert.AreEqual(ScenarioStatus.NotStarted, second.Get("scenario-02").Status);
    }

    [Test]
    public void Resume_SuspendDataFromNewerVersion_IsNotTrusted()
    {
        ScenarioCatalog catalog = Catalog();
        ScenarioTracker first = NewTracker(catalog);
        first.CompleteScenario("scenario-01", 80f);
        Assert.IsTrue(ScormEditorBackend.SetValue("cmi.suspend_data", "{\"v\":99,\"s\":[]}"));
        first.Suspend();

        ScormManager.Initialize();
        ScenarioTracker second = NewTracker(catalog);

        StringAssert.Contains("99", second.RestoreWarning);
        Assert.AreEqual(ScenarioRestoreIssue.NewerVersion, second.RestoreIssue);
        Assert.AreEqual("objectives", second.RestoredFrom);
        Assert.AreEqual(1, second.Get("scenario-01").Attempts);
    }

    [Test]
    public void ResetLocalState_WritesEmptyVersionedStateAndKeepsLmsObjectives()
    {
        ScenarioTracker tracker = NewTracker(Catalog());
        tracker.CompleteScenario("scenario-01", 80f);
        _calls.Clear();

        tracker.ResetLocalState();

        string json = Value("cmi.suspend_data");
        StringAssert.StartsWith("{\"v\":1", json);
        StringAssert.DoesNotContain("scenario-01", json);
        Assert.AreEqual(ScenarioStatus.NotStarted, tracker.Get("scenario-01").Status);
        Assert.AreEqual("80", Objective("scenario-01", "score.raw"), "the LMS keeps the objective");
        AssertNoEmptySets();
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Errors
    // ---------------------------------------------------------------------------------------------------------------

    [Test]
    public void InvalidCalls_ThrowWithContext()
    {
        ScenarioTracker tracker = new ScenarioTracker(Catalog(), () => _now);
        Assert.Throws<ScenarioTrackerException>(() => tracker.BeginScenario("scenario-01"), "not initialized");
        tracker.Initialize();

        ScenarioTrackerException unknown = Assert.Throws<ScenarioTrackerException>(() => tracker.BeginScenario("scenario-99"));
        Assert.AreEqual(ScenarioTrackerError.UnknownScenario, unknown.Error);
        Assert.Throws<ArgumentOutOfRangeException>(() => tracker.CompleteScenario("scenario-02", 11f));
        Assert.Throws<ArgumentOutOfRangeException>(() => tracker.CompleteScenario("scenario-01", float.NaN));
        Assert.Throws<ArgumentException>(() => tracker.RecordDecision("scenario-01", "", StudentRecord.InteractionType.true_false,
            "true", StudentRecord.ResultType.correct, "true", 0f));
        Assert.Throws<ArgumentException>(() => tracker.RecordDecision("scenario-02", "d 1", StudentRecord.InteractionType.true_false,
            "true", StudentRecord.ResultType.correct, "true", 0f));
        Assert.AreEqual(-1, ObjectiveIndex("scenario-02"), "nothing written for a rejected score or decision id");
        Assert.IsFalse(tracker.Get("scenario-02").IsAttemptInProgress, "a rejected decision does not start the attempt");
    }

    [Test]
    public void LmsRejectingSuspendData_IsReportedAndRetriedNextTime()
    {
        ScenarioTracker tracker = NewTracker(Catalog());
        ScormEditorBackend.InjectSetError("cmi.suspend_data", 406);

        tracker.CompleteScenario("scenario-01", 80f);

        Assert.IsFalse(tracker.LastOperationSucceeded);
        Assert.AreEqual(1, tracker.LastOperationFailures.Count);
        Assert.AreEqual("cmi.suspend_data", tracker.LastOperationFailures[0].Element);
        Assert.AreEqual(406, tracker.LastOperationFailures[0].ErrorCode);

        ScormEditorBackend.ClearInjectedErrors();
        _calls.Clear();
        Assert.IsTrue(tracker.Commit());
        tracker.BeginScenario("scenario-02");

        Assert.IsTrue(tracker.LastOperationSucceeded);
        StringAssert.Contains("scenario-01", Value("cmi.suspend_data"));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Review round 2
    // ---------------------------------------------------------------------------------------------------------------

    [TestCase("{}")]
    [TestCase("{\"foo\":1}")]
    [TestCase("{\"cur\":\"scenario-01\",\"s\":[]}")]
    public void Resume_SuspendDataJsonWithoutVersion_RebuildsFromObjectives(string foreignJson)
    {
        ScenarioCatalog catalog = Catalog();
        ScenarioTracker first = NewTracker(catalog);
        first.CompleteScenario("scenario-01", 80f);
        Assert.IsTrue(ScormEditorBackend.SetValue("cmi.suspend_data", foreignJson));
        first.Suspend();

        ScormManager.Initialize();
        ScenarioTracker second = NewTracker(catalog);

        Assert.AreEqual(ScenarioRestoreIssue.NoVersion, second.RestoreIssue);
        Assert.AreEqual(ScenarioRestoreSource.Objectives, second.RestoreSource);
        Assert.AreEqual(1, second.Get("scenario-01").Attempts, "progress rebuilt from cmi.objectives, not lost");
        Assert.AreEqual(80f, second.Get("scenario-01").BestRaw);
    }

    [Test]
    public void SuspendData_SavedJsonCarriesCurrentVersion()
    {
        ScenarioTracker tracker = NewTracker(Catalog());
        tracker.BeginScenario("scenario-01");

        Assert.AreEqual(ScenarioSaveData.CurrentVersion, JsonUtility.FromJson<ScenarioSaveData>(Value("cmi.suspend_data")).v);
        Assert.AreEqual(0, JsonUtility.FromJson<ScenarioSaveData>("{}").v, "absent version must not default to CurrentVersion");
    }

    [Test]
    public void Close_CommitRejected_KeepsSessionOpenWithoutTerminate_AndCanRetry()
    {
        ScenarioTracker tracker = NewTracker(Catalog());
        tracker.CompleteScenario("scenario-01", 80f);
        ScormEditorBackend.Terminate();   // the LMS session is gone: writes fail with 133, Commit with 143
        _calls.Clear();

        bool ok = tracker.Suspend();

        Assert.IsFalse(ok);
        Assert.IsFalse(tracker.IsClosed, "a rejected Commit must not close the tracker");
        Assert.IsTrue(tracker.LastOperationFailures.Any(f => f.Operation == ScormCallOperation.Commit && f.ErrorCode == 143));
        Assert.IsFalse(_calls.Any(c => c.Operation == ScormCallOperation.Terminate), "no Terminate after a failed Commit");

        ScormEditorBackend.Initialize();
        Assert.IsTrue(tracker.Suspend(), "retry once the LMS accepts calls again");
        Assert.IsTrue(tracker.IsClosed);
    }

    [Test]
    public void Close_RewritesObjectiveAndGlobalScoreTheLmsRejectedBefore()
    {
        ScenarioTracker tracker = NewTracker(Catalog());
        ScormEditorBackend.InjectSetError("cmi.objectives.2.score.raw", 406);
        ScormEditorBackend.InjectSetError("cmi.score.raw", 406);
        tracker.CompleteScenario("scenario-01", 80f);
        Assert.IsFalse(tracker.LastOperationSucceeded);
        Assert.AreEqual("75.5", Value("cmi.score.raw"), "seed value still there");
        ScormEditorBackend.ClearInjectedErrors();

        Assert.IsTrue(tracker.Suspend());

        Assert.AreEqual("80", Objective("scenario-01", "score.raw"));
        Assert.AreEqual("20", Value("cmi.score.raw"));
    }

    [Test]
    public void RecordDecision_ResumedHalfPlayedAttemptWithoutBegin_StartsNewAttempt()
    {
        ScenarioCatalog catalog = Catalog();
        ScenarioTracker first = NewTracker(catalog);
        first.RecordDecision("scenario-01", "d1", StudentRecord.InteractionType.true_false, "true", StudentRecord.ResultType.correct, "true", 1f);
        first.Suspend();
        ScormManager.Initialize();
        ScenarioTracker second = NewTracker(catalog);
        Assert.IsTrue(second.Get("scenario-01").IsAttemptInProgress);

        second.RecordDecision("scenario-01", "d1", StudentRecord.InteractionType.true_false, "true", StudentRecord.ResultType.correct, "true", 1f);

        Assert.AreEqual("scenario-01-a1-d1", Value("cmi.interactions.1.id"));
        Assert.AreEqual("scenario-01-a2-d1", Value("cmi.interactions.2.id"));
    }

    [Test]
    public void RecordDecision_TimestampComesFromInjectedClock()
    {
        ScenarioTracker tracker = NewTracker(Catalog());

        tracker.RecordDecision("scenario-01", "d1", StudentRecord.InteractionType.true_false, "true", StudentRecord.ResultType.correct, "true", 1f);

        Assert.AreEqual(ScormFormat.ToTimestamp(_now.ToLocalTime()), Value("cmi.interactions.1.timestamp"));
    }
}
