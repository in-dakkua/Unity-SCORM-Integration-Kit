using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using NUnit.Framework;

/// <summary>ScormManager against the in-memory Editor backend (the same code path WebGL uses, minus the jslib).</summary>
public class ScormEditorBackendInteractionTests {

	CultureInfo _previousCulture;

	[SetUp]
	public void SetUp() {
		_previousCulture = Thread.CurrentThread.CurrentCulture;
		Thread.CurrentThread.CurrentCulture = new CultureInfo("es-ES");
		ScormEditorBackend.Reset();
		ScormManager.Initialize();
	}

	[TearDown]
	public void TearDown() {
		Thread.CurrentThread.CurrentCulture = _previousCulture;
	}

	static string Value(string key) {
		string v;
		return ScormEditorBackend.Data.TryGetValue(key, out v) ? v : null;
	}

	static StudentRecord.LearnerInteractionRecord NewInteraction(int objectives, int correctResponses) {
		StudentRecord.LearnerInteractionRecord r = new StudentRecord.LearnerInteractionRecord();
		r.type = StudentRecord.InteractionType.true_false;
		r.weighting = 0.5f;
		r.response = "true";
		r.latency = 18.4f;
		r.description = "Test interaction";
		r.result = StudentRecord.ResultType.correct;
		if (objectives >= 0) {
			r.objectives = new List<StudentRecord.LearnerInteractionObjective>();
			for (int i = 0; i < objectives; i++)
				r.objectives.Add(new StudentRecord.LearnerInteractionObjective { id = "urn:test:objective-" + i });
		}
		if (correctResponses >= 0) {
			r.correctResponses = new List<StudentRecord.LearnerInteractionCorrectResponse>();
			for (int i = 0; i < correctResponses; i++)
				r.correctResponses.Add(new StudentRecord.LearnerInteractionCorrectResponse { pattern = i == 0 ? "true" : "false" });
		}
		return r;
	}

	[Test]
	public void Initialize_LoadsStudentRecordFromBackend() {
		Assert.IsTrue(ScormManager.IsLmsConnected);
		Assert.AreEqual("Rene Descartes", ScormManager.GetLearnerName());
		Assert.AreEqual(2, ScormManager.GetObjectives().Count);
		Assert.AreEqual(1, ScormManager.GetInteractions().Count);
		Assert.AreEqual(StudentRecord.InteractionType.true_false, ScormManager.GetInteractions()[0].type);
		Assert.AreEqual(2, ScormManager.GetInteractions()[0].objectives.Count);
		Assert.AreEqual(0.755f, ScormManager.GetScoreScaled(), 1e-6f);
		Assert.AreEqual(138f, ScormManager.GetInteractions()[0].latency, 1e-3f);
		Assert.AreEqual(StudentRecord.EntryType.start, ScormManager.GetEntry());
	}

	[Test]
	public void AddInteraction_WithTwoObjectivesAndTwoCorrectResponses_WritesAllIndexes() {
		int index = ScormFormat.ParseInt(Value("cmi.interactions._count"));
		ScormManager.AddInteraction(NewInteraction(2, 2));

		string p = "cmi.interactions." + index + ".";
		Assert.AreEqual((index + 1).ToString(CultureInfo.InvariantCulture), Value("cmi.interactions._count"));
		Assert.AreEqual("true-false", Value(p + "type"));
		Assert.AreEqual("urn:test:objective-0", Value(p + "objectives.0.id"));
		Assert.AreEqual("urn:test:objective-1", Value(p + "objectives.1.id"));
		Assert.AreEqual("2", Value(p + "objectives._count"));
		Assert.AreEqual("true", Value(p + "correct_responses.0.pattern"));
		Assert.AreEqual("false", Value(p + "correct_responses.1.pattern"));
		Assert.AreEqual("2", Value(p + "correct_responses._count"));
		Assert.IsNull(Value(p + "correct_responses.0.pattrern"));
		Assert.AreEqual("0.5", Value(p + "weighting"));
		Assert.AreEqual("P0DT0H0M18.4S", Value(p + "latency"));
		Assert.AreEqual("correct", Value(p + "result"));
		Assert.AreEqual("Test interaction", Value(p + "description"));
	}

	[Test]
	public void AddInteraction_WithoutTimestamp_UsesNow() {
		int index = ScormFormat.ParseInt(Value("cmi.interactions._count"));
		ScormManager.AddInteraction(NewInteraction(0, 0));
		DateTime written = ScormFormat.ParseTimestamp(Value("cmi.interactions." + index + ".timestamp"));
		Assert.GreaterOrEqual(written.Year, 1970);
	}

	[Test]
	public void UpdateInteraction_WithNullCollections_DoesNotThrowAndWritesFields() {
		StudentRecord.LearnerInteractionRecord r = NewInteraction(-1, -1);
		r.description = "Updated";
		Assert.DoesNotThrow(() => ScormManager.UpdateInteraction(0, r));
		Assert.AreEqual("Updated", Value("cmi.interactions.0.description"));
		Assert.AreEqual("true-false", Value("cmi.interactions.0.type"));
	}

	[Test]
	public void UpdateInteraction_WritesPatternNotPattrern() {
		ScormManager.UpdateInteraction(0, NewInteraction(1, 2));
		Assert.AreEqual("false", Value("cmi.interactions.0.correct_responses.1.pattern"));
		Assert.IsNull(Value("cmi.interactions.0.correct_responses.1.pattrern"));
	}

	[Test]
	public void Setters_WriteInvariantNumbersAndVocabulary() {
		StudentRecord.LearnerScore score = new StudentRecord.LearnerScore { scaled = 0.75f, raw = 75f, max = 100f, min = 0f };
		ScormManager.SetScore(score);
		ScormManager.SetProgressMeasure(0.25f);
		ScormManager.SetExit(StudentRecord.ExitType.timeout);
		ScormManager.SetSessionTime(18.4f);
		Assert.AreEqual("0.75", Value("cmi.score.scaled"));
		Assert.AreEqual("75", Value("cmi.score.raw"));
		Assert.AreEqual("0.25", Value("cmi.progress_measure"));
		Assert.AreEqual("time-out", Value("cmi.exit"));
		Assert.AreEqual("P0DT0H0M18.4S", Value("cmi.session_time"));
	}

	[Test]
	public void SuspendData_WithPipe_IsNotTruncated() {
		ScormManager.SetSuspendData("a|b|c");
		Assert.AreEqual("a|b|c", Value("cmi.suspend_data"));
	}

	[Test]
	public void CommitAndTerminate_ReachTheBackend() {
		ScormManager.Commit();
		ScormManager.Terminate();
		Assert.AreEqual(1, ScormEditorBackend.CommitCount);
		Assert.IsTrue(ScormEditorBackend.IsTerminated);
	}
}
