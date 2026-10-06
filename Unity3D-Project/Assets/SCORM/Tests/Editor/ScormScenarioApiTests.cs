using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Multi-scenario API (one objective per scenario, upsert by id), interaction-objective links, no empty/redundant
/// writes, resume, and the ScormCall log event. Runs against the in-memory Editor backend.
/// </summary>
public class ScormScenarioApiTests {

	CultureInfo _previousCulture;
	readonly List<ScormCallInfo> _calls = new List<ScormCallInfo>();

	[SetUp]
	public void SetUp() {
		_previousCulture = Thread.CurrentThread.CurrentCulture;
		Thread.CurrentThread.CurrentCulture = new CultureInfo("es-ES");
		ScormEditorBackend.Reset();
		ScormManager.ScormCall += Record;
		ScormManager.Initialize();
		_calls.Clear();
	}

	[TearDown]
	public void TearDown() {
		ScormManager.ScormCall -= Record;
		Thread.CurrentThread.CurrentCulture = _previousCulture;
	}

	void Record(ScormCallInfo info) {
		_calls.Add(info);
	}

	List<ScormCallInfo> Sets() {
		return _calls.Where(c => c.Operation == ScormCallOperation.SetValue).ToList();
	}

	static string Value(string key) {
		string v;
		return ScormEditorBackend.Data.TryGetValue(key, out v) ? v : null;
	}

	static ScormObjectiveData Scenario(string id, float raw) {
		ScormObjectiveData data = new ScormObjectiveData(id);
		data.score = ScormScoreData.FromRaw(raw, 0f, 100f);
		data.successStatus = raw >= 70f ? StudentRecord.SuccessStatusType.passed : StudentRecord.SuccessStatusType.failed;
		data.completionStatus = StudentRecord.CompletionStatusType.completed;
		data.progressMeasure = 1f;
		data.description = "Scenario " + id;
		return data;
	}

	void AssertNoEmptySets() {
		foreach (ScormCallInfo set in Sets())
			Assert.IsFalse(string.IsNullOrEmpty(set.Value), "Empty value sent to " + set.Element);
	}

	// ---------------------------------------------------------------------------------------------------------------
	// UpsertObjective
	// ---------------------------------------------------------------------------------------------------------------

	[Test]
	public void UpsertObjective_NewId_AppendsWithOwnIdWrittenFirst() {
		int index = ScormManager.UpsertObjective(Scenario("scenario-01", 75f));

		Assert.AreEqual(2, index);
		List<ScormCallInfo> sets = Sets();
		Assert.AreEqual("cmi.objectives.2.id", sets[0].Element);
		Assert.AreEqual("scenario-01", sets[0].Value);
		CollectionAssert.AreEqual(new[] {
			"cmi.objectives.2.id", "cmi.objectives.2.score.min", "cmi.objectives.2.score.max", "cmi.objectives.2.score.raw",
			"cmi.objectives.2.score.scaled", "cmi.objectives.2.success_status", "cmi.objectives.2.completion_status",
			"cmi.objectives.2.progress_measure", "cmi.objectives.2.description" }, sets.Select(s => s.Element).ToArray());
		Assert.IsTrue(sets.All(s => s.Succeeded), string.Join("\n", sets.Select(s => s.ToString()).ToArray()));
		Assert.AreEqual("3", Value("cmi.objectives._count"));
		Assert.AreEqual("75", Value("cmi.objectives.2.score.raw"));
		Assert.AreEqual("0.75", Value("cmi.objectives.2.score.scaled"));
		Assert.AreEqual("passed", Value("cmi.objectives.2.success_status"));
		Assert.AreEqual("completed", Value("cmi.objectives.2.completion_status"));
		Assert.AreEqual("1", Value("cmi.objectives.2.progress_measure"));

		StudentRecord.Objectives record = ScormManager.GetObjective("scenario-01");
		Assert.IsNotNull(record);
		Assert.AreEqual(75f, record.score.raw);
		Assert.AreEqual(StudentRecord.SuccessStatusType.passed, record.successStatus);
		Assert.AreEqual(2, ScormManager.FindObjectiveIndex("scenario-01"));
	}

	[Test]
	public void UpsertObjective_SameDataTwice_SecondCallWritesNothing() {
		ScormManager.UpsertObjective(Scenario("scenario-01", 75f));
		_calls.Clear();

		int index = ScormManager.UpsertObjective(Scenario("scenario-01", 75f));

		Assert.AreEqual(2, index);
		CollectionAssert.IsEmpty(Sets());
		Assert.AreEqual("3", Value("cmi.objectives._count"));
	}

	[Test]
	public void UpsertObjective_ChangedScore_WritesOnlyChangedFields() {
		ScormManager.UpsertObjective(Scenario("scenario-01", 75f));
		_calls.Clear();

		ScormManager.UpsertObjective(Scenario("scenario-01", 80f));

		CollectionAssert.AreEquivalent(new[] { "cmi.objectives.2.score.raw", "cmi.objectives.2.score.scaled" }, Sets().Select(s => s.Element).ToArray());
		Assert.AreEqual("80", Value("cmi.objectives.2.score.raw"));
		Assert.AreEqual("0.8", Value("cmi.objectives.2.score.scaled"));
	}

	[Test]
	public void UpsertObjective_IdLoadedFromLms_UpdatesThatIndexAndComparesRealsNumerically() {
		// Seed: Objective2 is index 1 with raw "80.0", min "0.0", max "100.0", scaled "0.80", passed, completed, progress "1".
		ScormObjectiveData data = new ScormObjectiveData("Objective2");
		data.score = ScormScoreData.FromRaw(80f, 0f, 100f);
		data.successStatus = StudentRecord.SuccessStatusType.passed;
		data.progressMeasure = 1f;

		int index = ScormManager.UpsertObjective(data);

		Assert.AreEqual(1, index);
		CollectionAssert.IsEmpty(Sets(), "Values equal to the LMS ones (\"80.0\" vs \"80\") must not be re-sent");
		Assert.AreEqual("2", Value("cmi.objectives._count"));

		data.score = ScormScoreData.FromRaw(90f, 0f, 100f);
		ScormManager.UpsertObjective(data);
		CollectionAssert.AreEquivalent(new[] { "cmi.objectives.1.score.raw", "cmi.objectives.1.score.scaled" }, Sets().Select(s => s.Element).ToArray());
		Assert.AreEqual("Objective2", Value("cmi.objectives.1.id"));
	}

	[Test]
	public void UpsertObjective_OnlyStatus_WritesIdAndStatusOnly() {
		ScormObjectiveData data = new ScormObjectiveData("scenario-02");
		data.completionStatus = StudentRecord.CompletionStatusType.incomplete;

		ScormManager.UpsertObjective(data);

		CollectionAssert.AreEqual(new[] { "cmi.objectives.2.id", "cmi.objectives.2.completion_status" }, Sets().Select(s => s.Element).ToArray());
		Assert.IsNull(Value("cmi.objectives.2.score.raw"));
		Assert.IsNull(Value("cmi.objectives.2.success_status"));
		Assert.IsNull(Value("cmi.objectives.2.description"));
		AssertNoEmptySets();
	}

	[Test]
	public void UpsertObjective_InvalidValues_ThrowBeforeAnyWrite() {
		ScormObjectiveData scaledOut = new ScormObjectiveData("scenario-03");
		scaledOut.score = new ScormScoreData(50f, 0f, 100f, 1.5f);
		Assert.Throws<ArgumentOutOfRangeException>(() => ScormManager.UpsertObjective(scaledOut));

		ScormObjectiveData rawAboveMax = new ScormObjectiveData("scenario-03");
		rawAboveMax.score = new ScormScoreData(120f, 0f, 100f, null);
		Assert.Throws<ArgumentOutOfRangeException>(() => ScormManager.UpsertObjective(rawAboveMax));

		// max known from the LMS (Objective2 max "100.0") is used when the update does not carry it.
		ScormObjectiveData rawAboveKnownMax = new ScormObjectiveData("Objective2");
		rawAboveKnownMax.score = new ScormScoreData(120f, null, null, null);
		Assert.Throws<ArgumentOutOfRangeException>(() => ScormManager.UpsertObjective(rawAboveKnownMax));

		ScormObjectiveData progressOut = new ScormObjectiveData("scenario-03");
		progressOut.progressMeasure = 1.2f;
		Assert.Throws<ArgumentOutOfRangeException>(() => ScormManager.UpsertObjective(progressOut));

		Assert.Throws<ArgumentException>(() => ScormManager.UpsertObjective(new ScormObjectiveData("  ")));
		Assert.Throws<ArgumentNullException>(() => ScormManager.UpsertObjective(null));

		CollectionAssert.IsEmpty(Sets());
		Assert.AreEqual(-1, ScormManager.FindObjectiveIndex("scenario-03"));
		Assert.AreEqual("2", Value("cmi.objectives._count"));
	}

	[Test]
	public void UpsertObjective_Resume_FindsObjectivesLoadedFromLmsInsteadOfDuplicating() {
		ScormManager.UpsertObjective(Scenario("scenario-01", 75f));
		ScormManager.UpsertObjective(Scenario("scenario-02", 40f));
		ScormManager.SetExit(StudentRecord.ExitType.suspend);
		ScormManager.Commit();
		ScormManager.Terminate();

		ScormManager.Initialize();	// relaunch of the suspended attempt
		_calls.Clear();

		Assert.AreEqual(StudentRecord.EntryType.resume, ScormManager.GetEntry());
		Assert.AreEqual(4, ScormManager.GetObjectives().Count);
		Assert.AreEqual(3, ScormManager.FindObjectiveIndex("scenario-02"));
		Assert.AreEqual(40f, ScormManager.GetObjective("scenario-02").score.raw);

		int index = ScormManager.UpsertObjective(Scenario("scenario-02", 90f));

		Assert.AreEqual(3, index);
		Assert.AreEqual("4", Value("cmi.objectives._count"));
		Assert.IsFalse(Sets().Any(s => s.Element.EndsWith(".id")), "The id must not be re-sent on resume");
		CollectionAssert.AreEquivalent(new[] { "cmi.objectives.3.score.raw", "cmi.objectives.3.score.scaled", "cmi.objectives.3.success_status" },
			Sets().Select(s => s.Element).ToArray());
		Assert.AreEqual("passed", Value("cmi.objectives.3.success_status"));
	}

	[Test]
	public void Backend_NotSuspended_RelaunchStartsANewAttempt() {
		ScormManager.UpsertObjective(Scenario("scenario-01", 75f));
		ScormManager.SetExit(StudentRecord.ExitType.normal);
		ScormManager.Terminate();

		ScormManager.Initialize();

		Assert.AreEqual(StudentRecord.EntryType.start, ScormManager.GetEntry());
		Assert.AreEqual(-1, ScormManager.FindObjectiveIndex("scenario-01"));
	}

	[Test]
	public void Backend_RejectsFieldBeforeIdAndChangedObjectiveId() {
		ScormAPIWrapper wrapper = new ScormAPIWrapper(null, null);
		wrapper.Initialize();
		Assert.IsFalse(wrapper.SetValue("cmi.objectives.2.score.raw", "1"));
		Assert.AreEqual(ScormEditorBackend.ErrorDependencyNotEstablished, wrapper.LastErrorCode);
		Assert.IsFalse(wrapper.SetValue("cmi.objectives.0.id", "other-id"));
		Assert.AreEqual(ScormEditorBackend.ErrorGeneralSetFailure, wrapper.LastErrorCode);
		Assert.IsFalse(wrapper.SetValue("cmi.objectives.5.id", "gap"));
		Assert.AreEqual(ScormEditorBackend.ErrorGeneralSetFailure, wrapper.LastErrorCode);
		Assert.IsFalse(wrapper.SetValue("cmi.objectives.2.id", "Objective1"), "duplicated objective id");
	}

	// ---------------------------------------------------------------------------------------------------------------
	// Global score / status
	// ---------------------------------------------------------------------------------------------------------------

	[Test]
	public void UpdateScoreAndStatus_WriteOnlyChangedAndNeverEmpty() {
		// Seed: cmi.score.min "0.0", max "100.0", raw "75.5", scaled "0.755"; success_status "unknown"; completion "incomplete".
		Assert.IsTrue(ScormManager.UpdateScore(ScormScoreData.FromRaw(80f, 0f, 100f)));
		Assert.IsTrue(ScormManager.UpdateStatus(StudentRecord.SuccessStatusType.passed, StudentRecord.CompletionStatusType.not_set));
		Assert.IsTrue(ScormManager.UpdateProgressMeasure(0.5f));
		ScormManager.SetSuccessStatus(StudentRecord.SuccessStatusType.not_set);
		ScormManager.SetCompletionStatus(StudentRecord.CompletionStatusType.not_set);

		CollectionAssert.AreEqual(new[] { "cmi.score.raw", "cmi.score.scaled", "cmi.success_status", "cmi.progress_measure" }, Sets().Select(s => s.Element).ToArray());
		Assert.AreEqual("0.8", Value("cmi.score.scaled"));
		Assert.AreEqual("passed", Value("cmi.success_status"));
		Assert.AreEqual("incomplete", Value("cmi.completion_status"));
		Assert.AreEqual(StudentRecord.SuccessStatusType.passed, ScormManager.GetSuccessStatus());
		Assert.AreEqual(80f, ScormManager.GetScoreRaw());

		Assert.Throws<ArgumentOutOfRangeException>(() => ScormManager.UpdateScore(new ScormScoreData(null, null, null, -1.5f)));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScormManager.UpdateProgressMeasure(2f));
	}

	[Test]
	public void ScoreData_FromRaw_ComputesClampedScaled() {
		Assert.AreEqual(0.75f, ScormScoreData.FromRaw(75f, 0f, 100f).scaled.Value, 1e-6f);
		Assert.AreEqual(0.5f, ScormScoreData.FromRaw(15f, 10f, 20f).scaled.Value, 1e-6f);
		Assert.AreEqual(0f, ScormScoreData.FromRaw(5f, 5f, 5f).scaled.Value);
		Assert.AreEqual(1f, ScormScoreData.FromRaw(150f, 0f, 100f).scaled.Value);
	}

	// ---------------------------------------------------------------------------------------------------------------
	// Interactions
	// ---------------------------------------------------------------------------------------------------------------

	[Test]
	public void RecordInteraction_KeepsOwnIdAndLinksObjectivesById() {
		ScormManager.UpsertObjective(Scenario("scenario-01", 75f));
		_calls.Clear();

		StudentRecord.LearnerInteractionRecord decision = new StudentRecord.LearnerInteractionRecord();
		decision.id = "scenario-01-decision-1";
		decision.type = StudentRecord.InteractionType.choice;
		decision.response = "b";
		decision.result = StudentRecord.ResultType.correct;
		decision.weighting = 1f;
		decision.latency = 4.5f;

		int index = ScormManager.RecordInteraction(decision, "scenario-01", "", "scenario-01");

		Assert.AreEqual(1, index);
		string p = "cmi.interactions.1.";
		Assert.AreEqual(p + "id", Sets()[0].Element);
		Assert.AreEqual(p + "type", Sets()[1].Element);
		Assert.AreEqual("scenario-01-decision-1", Value(p + "id"));
		Assert.AreEqual("choice", Value(p + "type"));
		Assert.AreEqual("scenario-01", Value(p + "objectives.0.id"));
		Assert.AreEqual("1", Value(p + "objectives._count"));
		Assert.AreEqual("b", Value(p + "learner_response"));
		Assert.AreEqual("correct", Value(p + "result"));
		Assert.AreEqual("P0DT0H0M4.5S", Value(p + "latency"));
		Assert.AreEqual("2", Value("cmi.interactions._count"));
		Assert.AreEqual(1, ScormManager.GetInteractions()[1].objectives.Count);
		Assert.AreEqual("scenario-01", ScormManager.GetInteractions()[1].objectives[0].id);
		AssertNoEmptySets();
	}

	[Test]
	public void RecordInteraction_WithoutIdAndWithUnsetValues_GeneratesIdAndSendsNoEmptyValues() {
		StudentRecord.LearnerInteractionRecord interaction = new StudentRecord.LearnerInteractionRecord();
		interaction.type = StudentRecord.InteractionType.not_set;
		interaction.result = StudentRecord.ResultType.not_set;
		interaction.response = "ignored without a type";

		int index = ScormManager.RecordInteraction(interaction);

		Assert.AreEqual(1, index);
		Assert.AreEqual("urn:STALS:interaction-id-1", Value("cmi.interactions.1.id"));
		CollectionAssert.AreEquivalent(new[] { "cmi.interactions.1.id", "cmi.interactions.1.timestamp", "cmi.interactions.1.weighting" },
			Sets().Select(s => s.Element).ToArray());
		AssertNoEmptySets();
	}

	// ---------------------------------------------------------------------------------------------------------------
	// Legacy API (M3)
	// ---------------------------------------------------------------------------------------------------------------

	[Test]
	public void LegacyAddAndUpdateObjective_SkipEmptyAndUnchangedValues() {
		StudentRecord.Objectives objective = new StudentRecord.Objectives();
		objective.score = new StudentRecord.LearnerScore { min = 0f, max = 100f, raw = 10f, scaled = 0.1f };
		objective.successStatus = StudentRecord.SuccessStatusType.not_set;
		objective.completionStatus = StudentRecord.CompletionStatusType.not_set;
		ScormManager.AddObjective(objective);

		Assert.AreEqual("urn:STALS:objective-id-2", Value("cmi.objectives.2.id"), "AddObjective keeps its legacy id");
		Assert.AreEqual("cmi.objectives.2.id", Sets()[0].Element);
		Assert.IsNull(Value("cmi.objectives.2.success_status"));
		Assert.IsNull(Value("cmi.objectives.2.description"));
		AssertNoEmptySets();

		_calls.Clear();
		objective.score.raw = 20f;	// same object as in StudentRecord, mutated in place like AnObjective does
		ScormManager.UpdateObjective(2, objective);
		CollectionAssert.AreEqual(new[] { "cmi.objectives.2.score.raw" }, Sets().Select(s => s.Element).ToArray());

		_calls.Clear();
		ScormManager.UpdateObjective(2, objective);
		CollectionAssert.IsEmpty(Sets());
	}

	[Test]
	public void LegacyUpdateInteraction_SkipsEmptyValues() {
		StudentRecord.LearnerInteractionRecord r = new StudentRecord.LearnerInteractionRecord();
		r.type = StudentRecord.InteractionType.true_false;
		r.result = StudentRecord.ResultType.not_set;
		r.response = null;
		r.description = null;
		r.weighting = 1f;

		ScormManager.UpdateInteraction(0, r);

		AssertNoEmptySets();
		Assert.AreEqual("incorrect", Value("cmi.interactions.0.result"));
		Assert.AreEqual("Interaction description", Value("cmi.interactions.0.description"));
	}

	// ---------------------------------------------------------------------------------------------------------------
	// ScormCall event
	// ---------------------------------------------------------------------------------------------------------------

	[Test]
	public void ScormCall_ReportsEveryOperationWithLmsErrorCodes() {
		ScormManager.Terminate();
		ScormEditorBackend.Reset();
		_calls.Clear();

		ScormManager.Initialize();
		ScormCallInfo init = _calls[0];
		Assert.AreEqual(ScormCallOperation.Initialize, init.Operation);
		Assert.IsTrue(init.Succeeded);
		Assert.AreEqual("true", init.Result);

		ScormCallInfo get = _calls.First(c => c.Operation == ScormCallOperation.GetValue && c.Element == "cmi.learner_name");
		Assert.AreEqual("Rene Descartes", get.Result);
		Assert.IsTrue(get.Succeeded);
		Assert.AreEqual(0, get.ErrorCode);

		ScormAPIWrapper wrapper = new ScormAPIWrapper(null, null);
		wrapper.Initialize();
		_calls.Clear();

		wrapper.GetValue("cmi.does_not_exist");
		Assert.AreEqual(ScormCallOperation.GetValue, _calls[0].Operation);
		Assert.IsFalse(_calls[0].Succeeded);
		Assert.AreEqual(401, _calls[0].ErrorCode);
		Assert.AreEqual("Undefined Data Model Element", _calls[0].ErrorDescription);

		wrapper.SetValue("cmi.objectives.7.id", "gap");
		Assert.AreEqual(ScormCallOperation.SetValue, _calls[1].Operation);
		Assert.AreEqual("cmi.objectives.7.id", _calls[1].Element);
		Assert.AreEqual("gap", _calls[1].Value);
		Assert.AreEqual("false", _calls[1].Result);
		Assert.AreEqual(351, _calls[1].ErrorCode);

		wrapper.SetValue("cmi.location", "page-3");
		Assert.IsTrue(_calls[2].Succeeded);
		Assert.AreEqual(0, _calls[2].ErrorCode);

		wrapper.Commit();
		wrapper.Terminate();
		wrapper.Commit();
		Assert.AreEqual(ScormCallOperation.Commit, _calls[3].Operation);
		Assert.IsTrue(_calls[3].Succeeded);
		Assert.AreEqual(ScormCallOperation.Terminate, _calls[4].Operation);
		Assert.IsTrue(_calls[4].Succeeded);
		Assert.IsFalse(_calls[5].Succeeded);
		Assert.AreEqual(143, _calls[5].ErrorCode);
		StringAssert.Contains("error 143", _calls[5].ToString());
	}

	// ---------------------------------------------------------------------------------------------------------------
	// Review round 2: H1 (index invariant), H2 (failures reported), H3 (event reentrancy), L1-L4
	// ---------------------------------------------------------------------------------------------------------------

	[Test]
	public void H1_LegacyAddObjectiveWithRejectedId_IsNotAddedAndUpsertStillLandsAtCount() {
		ScormEditorBackend.InjectSetError("cmi.objectives.2.id", 351);
		StudentRecord.Objectives legacy = new StudentRecord.Objectives();
		legacy.score = new StudentRecord.LearnerScore { max = 100f };
		ScormManager.AddObjective(legacy);

		Assert.AreEqual(2, ScormManager.GetObjectives().Count, "A rejected objective must not be added locally");
		Assert.IsFalse(Sets().Any(s => s.Element.StartsWith("cmi.objectives.2.") && !s.Element.EndsWith(".id")), "No field after a rejected id");

		ScormEditorBackend.ClearInjectedErrors();
		bool allWritten;
		int index = ScormManager.UpsertObjective(Scenario("scenario-01", 50f), out allWritten);
		Assert.AreEqual(2, index);
		Assert.IsTrue(allWritten);
		Assert.AreEqual("scenario-01", Value("cmi.objectives.2.id"));
		Assert.AreEqual("3", Value("cmi.objectives._count"));
	}

	[Test]
	public void H1_LegacyAddInteractionAfterTerminate_IsNotAddedAndRecordInteractionStillLandsAtCount() {
		ScormManager.Terminate();
		StudentRecord.LearnerInteractionRecord legacy = new StudentRecord.LearnerInteractionRecord();
		legacy.type = StudentRecord.InteractionType.other;
		ScormManager.AddInteraction(legacy);
		Assert.AreEqual(1, ScormManager.GetInteractions().Count, "Rejected (133) interaction must not be added locally");

		ScormEditorBackend.Reset();
		ScormManager.Initialize();
		StudentRecord.LearnerInteractionRecord decision = new StudentRecord.LearnerInteractionRecord();
		decision.id = "d-1";
		decision.type = StudentRecord.InteractionType.other;
		Assert.AreEqual(1, ScormManager.RecordInteraction(decision));
		Assert.AreEqual("2", Value("cmi.interactions._count"));
	}

	[Test]
	public void H2_UpsertObjective_ReportsRejectedFieldAndRetriesItNextTime() {
		ScormEditorBackend.InjectSetError("cmi.objectives.2.score.raw", 406);
		bool allWritten;
		int index = ScormManager.UpsertObjective(Scenario("scenario-01", 75f), out allWritten);

		Assert.AreEqual(2, index);
		Assert.IsFalse(allWritten);
		Assert.IsTrue(ScormManager.LastWriteFailed);
		Assert.AreEqual(1, ScormManager.LastWriteFailures);
		Assert.AreEqual(406, ScormManager.LastWriteErrorCode);
		Assert.AreEqual("passed", Value("cmi.objectives.2.success_status"), "Other fields are still written");
		Assert.AreNotEqual(75f, ScormManager.GetObjective("scenario-01").score.raw, "StudentRecord only reflects accepted values");

		ScormEditorBackend.ClearInjectedErrors();
		_calls.Clear();
		ScormManager.UpsertObjective(Scenario("scenario-01", 75f), out allWritten);

		Assert.IsTrue(allWritten);
		Assert.IsFalse(ScormManager.LastWriteFailed);
		Assert.AreEqual(0, ScormManager.LastWriteErrorCode);
		CollectionAssert.AreEqual(new[] { "cmi.objectives.2.score.raw" }, Sets().Select(s => s.Element).ToArray(), "Only the rejected value is retried");
		Assert.AreEqual("75", Value("cmi.objectives.2.score.raw"));
	}

	[Test]
	public void H2_RecordInteraction_ReportsRejectedField() {
		ScormEditorBackend.InjectSetError("cmi.interactions.1.learner_response", 406);
		StudentRecord.LearnerInteractionRecord decision = new StudentRecord.LearnerInteractionRecord();
		decision.id = "d-1";
		decision.type = StudentRecord.InteractionType.true_false;
		decision.response = "maybe";
		decision.result = StudentRecord.ResultType.incorrect;

		bool allWritten;
		int index = ScormManager.RecordInteraction(decision, out allWritten, "scenario-01");

		Assert.AreEqual(1, index);
		Assert.IsFalse(allWritten);
		Assert.AreEqual(406, ScormManager.LastWriteErrorCode);
		Assert.AreEqual("incorrect", Value("cmi.interactions.1.result"));

		decision = new StudentRecord.LearnerInteractionRecord();
		decision.type = StudentRecord.InteractionType.other;
		ScormManager.RecordInteraction(decision, out allWritten);
		Assert.IsTrue(allWritten);
		Assert.IsFalse(ScormManager.LastWriteFailed);
	}

	[Test]
	public void H2_RecordInteraction_RejectedId_ReturnsMinusOneAndAddsNothing() {
		ScormEditorBackend.InjectSetError("cmi.interactions.1.id", 351);
		StudentRecord.LearnerInteractionRecord decision = new StudentRecord.LearnerInteractionRecord();
		decision.id = "d-1";
		decision.type = StudentRecord.InteractionType.other;
		bool allWritten;
		Assert.AreEqual(-1, ScormManager.RecordInteraction(decision, out allWritten));
		Assert.IsFalse(allWritten);
		Assert.AreEqual(1, ScormManager.GetInteractions().Count);
		Assert.AreEqual(1, Sets().Count, "Nothing is written after a rejected id");
	}

	[Test]
	public void H3_HandlerCallingTheApi_DoesNotRecurseAndKeepsOuterErrorCode() {
		int handlerRuns = 0;
		int observedErrorCode = -1;
		ScormAPIWrapper wrapper = null;
		Action<ScormCallInfo> writer = info => {
			handlerRuns++;
			ScormManager.SetLocation("written-by-handler-" + handlerRuns);	// would recurse forever without the guard
			ScormManager.GetLocation();
		};
		ScormManager.ScormCall += writer;
		try {
			ScormManager.SetSuspendData("x");
			Assert.AreEqual(1, handlerRuns, "Nested calls made by a handler must not raise the event again");
			Assert.AreEqual("written-by-handler-1", Value("cmi.location"), "The nested call itself works");

		} finally {
			ScormManager.ScormCall -= writer;
		}

		wrapper = new ScormAPIWrapper(null, null);
		wrapper.Initialize();
		ScormAPIWrapper target = wrapper;
		Action<ScormCallInfo> sameWrapperWriter = info => {
			handlerRuns++;
			target.SetValue("cmi.location", "nested");		// succeeds (error 0) on the same wrapper
		};
		ScormManager.ScormCall += sameWrapperWriter;
		try {
			handlerRuns = 0;
			wrapper.GetValue("cmi.does_not_exist");			// 401
			observedErrorCode = wrapper.LastErrorCode;
		} finally {
			ScormManager.ScormCall -= sameWrapperWriter;
		}
		Assert.AreEqual("nested", Value("cmi.location"));
		Assert.AreEqual(1, handlerRuns);
		Assert.AreEqual(401, observedErrorCode, "The outer call keeps its own LastErrorCode");
		Assert.IsFalse(_calls.Any(c => c.Element == "cmi.location"), "Nested calls are not reported to the other handlers either");
	}

	[Test]
	public void L1_RealUpdateAgainstVocabularyValue_IsWritten() {
		// Seed: cmi.interactions.0.result = "incorrect"; an estimate of 0 must replace it (ParseReal("incorrect") == 0).
		StudentRecord.LearnerInteractionRecord r = new StudentRecord.LearnerInteractionRecord();
		r.type = StudentRecord.InteractionType.true_false;
		r.result = StudentRecord.ResultType.estimate;
		r.estimate = 0f;
		ScormManager.UpdateInteraction(0, r);
		Assert.AreEqual("0", Value("cmi.interactions.0.result"));
	}

	[Test]
	public void L2_NonFiniteScores_AreRejectedBeforeWriting() {
		ScormObjectiveData data = new ScormObjectiveData("scenario-09");
		data.score = new ScormScoreData(null, float.NegativeInfinity, 100f, null);
		Assert.Throws<ArgumentOutOfRangeException>(() => ScormManager.UpsertObjective(data));
		data.score = new ScormScoreData(float.PositiveInfinity, null, null, null);
		Assert.Throws<ArgumentOutOfRangeException>(() => ScormManager.UpsertObjective(data));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScormManager.UpdateScore(new ScormScoreData(null, null, float.NaN, null)));
		CollectionAssert.IsEmpty(Sets());
	}

	[Test]
	public void L3_DuplicatedObjectiveLinks_AreWrittenOnce() {
		StudentRecord.LearnerInteractionRecord decision = new StudentRecord.LearnerInteractionRecord();
		decision.type = StudentRecord.InteractionType.other;
		decision.objectives = new List<StudentRecord.LearnerInteractionObjective> {
			new StudentRecord.LearnerInteractionObjective { id = "scenario-01" },
			new StudentRecord.LearnerInteractionObjective { id = "scenario-01" },
			new StudentRecord.LearnerInteractionObjective { id = "scenario-02" } };
		bool allWritten;
		ScormManager.RecordInteraction(decision, out allWritten);
		Assert.IsTrue(allWritten);
		Assert.AreEqual("2", Value("cmi.interactions.1.objectives._count"));
		Assert.AreEqual("scenario-02", Value("cmi.interactions.1.objectives.1.id"));
	}

	[Test]
	public void L4_IdsWithWhitespaceOrTooLong_AreRejected() {
		Assert.Throws<ArgumentException>(() => ScormManager.UpsertObjective(new ScormObjectiveData("scenario 01")));
		Assert.Throws<ArgumentException>(() => ScormManager.UpsertObjective(new ScormObjectiveData(new string('a', 4001))));
		StudentRecord.LearnerInteractionRecord decision = new StudentRecord.LearnerInteractionRecord();
		decision.type = StudentRecord.InteractionType.other;
		Assert.Throws<ArgumentException>(() => ScormManager.RecordInteraction(decision, "scenario 01"));
		decision.id = "my decision";
		Assert.Throws<ArgumentException>(() => ScormManager.RecordInteraction(decision));
		CollectionAssert.IsEmpty(Sets());
	}

	[Test]
	public void ScormCall_ThrowingHandler_DoesNotBreakTheApiCall() {
		Action<ScormCallInfo> broken = info => { throw new InvalidOperationException("broken log UI"); };
		ScormManager.ScormCall += broken;
		try {
			LogAssert.Expect(LogType.Exception, new Regex("broken log UI"));
			ScormManager.SetLocation("page-7");
		} finally {
			ScormManager.ScormCall -= broken;
		}
		Assert.AreEqual("page-7", Value("cmi.location"));
		Assert.AreEqual(1, Sets().Count, "the other handlers still run");
	}
}
