using System;
using System.Globalization;
using System.Threading;
using NUnit.Framework;

/// <summary>Culture-invariant conversions, run with a Spanish (es-ES, decimal comma) current culture.</summary>
public class ScormFormatTests {

	CultureInfo _previousCulture;

	[SetUp]
	public void SetUp() {
		_previousCulture = Thread.CurrentThread.CurrentCulture;
		Thread.CurrentThread.CurrentCulture = new CultureInfo("es-ES");
	}

	[TearDown]
	public void TearDown() {
		Thread.CurrentThread.CurrentCulture = _previousCulture;
	}

	[Test]
	public void ToReal_UsesDotDecimalSeparator() {
		Assert.AreEqual("0.5", ScormFormat.ToReal(0.5f));
		Assert.AreEqual("0.755", ScormFormat.ToReal(0.755f));
		Assert.AreEqual("75", ScormFormat.ToReal(75f));
		Assert.AreEqual("-1", ScormFormat.ToReal(-1f));
	}

	[Test]
	public void ToReal_NeverUsesExponent() {
		Assert.AreEqual("0.00001", ScormFormat.ToReal(0.00001f));
		Assert.AreEqual("0.0000001", ScormFormat.ToReal(0.0000001f));
		Assert.AreEqual("0", ScormFormat.ToReal(float.NaN));
	}

	[Test]
	public void ParseReal_IsInvariantAndTolerant() {
		Assert.AreEqual(0.9f, ScormFormat.ParseReal("0.9"), 1e-6f);
		Assert.AreEqual(0f, ScormFormat.ParseReal(""));
		Assert.AreEqual(0f, ScormFormat.ParseReal(null));
		Assert.AreEqual(0f, ScormFormat.ParseReal("abc"));
		Assert.AreEqual(-1, ScormFormat.ParseInt("-1"));
		Assert.AreEqual(0, ScormFormat.ParseInt(""));
	}

	[Test]
	public void ParseUserReal_AcceptsDotAndCurrentCultureComma() {
		Assert.AreEqual(0.5f, ScormFormat.ParseUserReal("0.5"), 1e-6f);
		Assert.AreEqual(0.5f, ScormFormat.ParseUserReal("0,5"), 1e-6f);
		Assert.AreEqual(0f, ScormFormat.ParseUserReal("x"));
	}

	[Test]
	public void SecondsToTimeInterval_KeepsFractionWithDot() {
		Assert.AreEqual("P0DT0H0M18.4S", ScormFormat.SecondsToTimeInterval(18.4f));
		Assert.AreEqual("P0DT0H0M18S", ScormFormat.SecondsToTimeInterval(18f));
		Assert.AreEqual("P1DT1H1M1.25S", ScormFormat.SecondsToTimeInterval(86400f + 3600f + 60f + 1.25f));
		Assert.AreEqual("P0DT0H0M0S", ScormFormat.SecondsToTimeInterval(-5f));
	}

	[Test]
	public void TimeIntervalToSeconds_ParsesIsoDurations() {
		Assert.AreEqual(138f, ScormFormat.TimeIntervalToSeconds("P0DT0H2M18S"), 1e-3f);
		Assert.AreEqual(18.4f, ScormFormat.TimeIntervalToSeconds("PT18.4S"), 1e-3f);
		Assert.AreEqual(3600f, ScormFormat.TimeIntervalToSeconds("PT1H"), 1e-3f);
		Assert.AreEqual(0f, ScormFormat.TimeIntervalToSeconds(""));
		Assert.AreEqual(0f, ScormFormat.TimeIntervalToSeconds(null));
		Assert.AreEqual(0f, ScormFormat.TimeIntervalToSeconds("garbage"));
	}

	[Test]
	public void TimeInterval_RoundTrips() {
		Assert.AreEqual(18.4f, ScormFormat.TimeIntervalToSeconds(ScormFormat.SecondsToTimeInterval(18.4f)), 1e-3f);
	}

	[Test]
	public void ParseTimestamp_EmptyReturnsMinValueWithoutException() {
		Assert.AreEqual(DateTime.MinValue, ScormFormat.ParseTimestamp(""));
		Assert.AreEqual(DateTime.MinValue, ScormFormat.ParseTimestamp(null));
		Assert.AreEqual(DateTime.MinValue, ScormFormat.ParseTimestamp("not a date"));
	}

	[Test]
	public void ParseTimestamp_ParsesScormTimeWithFractionAndOffset() {
		DateTime t = ScormFormat.ParseTimestamp("2015-09-07T09:00:00.5+02:00");
		Assert.AreNotEqual(DateTime.MinValue, t);
		DateTime utc = t.ToUniversalTime();
		Assert.AreEqual(new DateTime(2015, 9, 7, 7, 0, 0, 500, DateTimeKind.Utc), utc);

		DateTime local = ScormFormat.ParseTimestamp("2015-09-07T09:00:00");
		Assert.AreEqual(new DateTime(2015, 9, 7, 9, 0, 0), local);
	}

	[Test]
	public void ToTimestamp_IsScormTime() {
		Assert.AreEqual("2015-09-07T09:05:03", ScormFormat.ToTimestamp(new DateTime(2015, 9, 7, 9, 5, 3)));
	}

	[Test]
	public void ToVocabulary_MapsEnumsToScormTokens() {
		Assert.AreEqual("true-false", ScormFormat.ToVocabulary(StudentRecord.InteractionType.true_false));
		Assert.AreEqual("fill-in", ScormFormat.ToVocabulary(StudentRecord.InteractionType.fill_in));
		Assert.AreEqual("long-fill-in", ScormFormat.ToVocabulary(StudentRecord.InteractionType.long_fill_in));
		Assert.AreEqual("other", ScormFormat.ToVocabulary(StudentRecord.InteractionType.other));
		Assert.AreEqual("time-out", ScormFormat.ToVocabulary(StudentRecord.ExitType.timeout));
		Assert.AreEqual("logout", ScormFormat.ToVocabulary(StudentRecord.ExitType.logout));
		Assert.AreEqual("suspend", ScormFormat.ToVocabulary(StudentRecord.ExitType.suspend));
		Assert.AreEqual("no-credit", ScormFormat.ToVocabulary(StudentRecord.CreditType.no_credit));
		Assert.AreEqual("not attempted", ScormFormat.ToVocabulary(StudentRecord.CompletionStatusType.not_attempted));
		Assert.AreEqual("", ScormFormat.ToVocabulary(StudentRecord.CompletionStatusType.not_set));
		Assert.AreEqual("", ScormFormat.ToVocabulary(StudentRecord.SuccessStatusType.not_set));
		Assert.AreEqual("exit,message", ScormFormat.ToVocabulary(StudentRecord.TimeLimitActionType.exit_message));
		Assert.AreEqual("continue,no message", ScormFormat.ToVocabulary(StudentRecord.TimeLimitActionType.continue_no_message));
		Assert.AreEqual("ab-initio", ScormFormat.ToVocabulary(StudentRecord.EntryType.start));
	}

	[Test]
	public void CustomTypeToString_DelegatesToVocabulary() {
		Assert.AreEqual("true-false", ScormManager.CustomTypeToString(StudentRecord.InteractionType.true_false));
		Assert.AreEqual("time-out", ScormManager.CustomTypeToString(StudentRecord.ExitType.timeout));
		Assert.AreEqual("", ScormManager.CustomTypeToString(StudentRecord.ResultType.not_set));
	}

	[Test]
	public void StringToVocabulary_RoundTrips() {
		foreach (StudentRecord.InteractionType t in Enum.GetValues(typeof(StudentRecord.InteractionType)))
			Assert.AreEqual(t, ScormFormat.StringToInteractionType(ScormFormat.ToVocabulary(t)), t.ToString());
		foreach (StudentRecord.CompletionStatusType t in Enum.GetValues(typeof(StudentRecord.CompletionStatusType)))
			Assert.AreEqual(t, ScormFormat.StringToCompletionStatusType(ScormFormat.ToVocabulary(t)), t.ToString());
		foreach (StudentRecord.TimeLimitActionType t in Enum.GetValues(typeof(StudentRecord.TimeLimitActionType)))
			Assert.AreEqual(t, ScormFormat.StringToTimeLimitActionType(ScormFormat.ToVocabulary(t)), t.ToString());
	}

	[Test]
	public void StringToResultType_ParsesNumericEstimateInvariant() {
		float estimate;
		Assert.AreEqual(StudentRecord.ResultType.estimate, ScormFormat.StringToResultType("0.75", out estimate));
		Assert.AreEqual(0.75f, estimate, 1e-6f);
		Assert.AreEqual(StudentRecord.ResultType.correct, ScormFormat.StringToResultType("correct", out estimate));
		Assert.AreEqual(StudentRecord.ResultType.not_set, ScormFormat.StringToResultType("", out estimate));
	}
}
