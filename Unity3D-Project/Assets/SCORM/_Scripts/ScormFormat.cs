/***********************************************************************************************************************
 * Unity-SCORM Integration Kit
 *
 * Culture-invariant conversion between C# values and the SCORM 2004 data model (real, timeinterval, time, vocabularies)
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with
 * the License. You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 **********************************************************************************************************************/

using System;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Pure conversion helpers for the SCORM 2004 Run-Time Environment data model.
/// </summary>
/// <remarks>
/// Every value sent to the LMS must be culture invariant: with a Spanish (es-ES) culture a plain float.ToString()
/// produces "0,5", which the LMS rejects with error 406 (Data Model Element Type Mismatch).
/// </remarks>
public static class ScormFormat {

	static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

	static readonly Regex TimeIntervalRegex = new Regex(
		@"^P(?:(\d+)Y)?(?:(\d+)M)?(?:(\d+)D)?(?:T(?:(\d+)H)?(?:(\d+)M)?(?:(\d+(?:\.\d+)?)S)?)?$",
		RegexOptions.CultureInvariant);

	// ---------------------------------------------------------------------------------------------------------------
	// real(10,7)
	// ---------------------------------------------------------------------------------------------------------------

	/// <summary>Formats a float as a SCORM real(10,7): invariant culture, no exponent, at most 7 decimals.</summary>
	public static string ToReal(float value) {
		if (float.IsNaN(value) || float.IsInfinity(value))
			return "0";
		// "R" is not used on purpose: it produces exponents ("1E-05") that LMSs reject.
		return FloatToDouble(value).ToString("0.#######", Invariant);
	}

	/// <summary>Widens a float keeping its 7 significant decimal digits (0.1f -> 0.1, not 0.100000001490116).</summary>
	static double FloatToDouble(float value) {
		if (Math.Abs(value) >= 1e15f)
			return value;
		return (double)(decimal)value;
	}

	/// <summary>Formats a double as a SCORM real(10,7): invariant culture, no exponent, at most 7 decimals.</summary>
	public static string ToReal(double value) {
		if (double.IsNaN(value) || double.IsInfinity(value))
			return "0";
		return value.ToString("0.#######", Invariant);
	}

	/// <summary>Parses a SCORM real coming from the LMS (invariant culture). Empty or invalid returns 0.</summary>
	public static float ParseReal(string str) {
		float result;
		if (!string.IsNullOrEmpty(str) && float.TryParse(str.Trim(), NumberStyles.Float, Invariant, out result))
			return result;
		return 0f;
	}

	/// <summary>Parses an integer coming from the LMS (invariant culture). Empty or invalid returns 0.</summary>
	public static int ParseInt(string str) {
		int result;
		if (!string.IsNullOrEmpty(str) && int.TryParse(str.Trim(), NumberStyles.Integer, Invariant, out result))
			return result;
		return 0;
	}

	/// <summary>Tries to parse a number typed by a user: invariant culture first, then the current culture.</summary>
	public static bool TryParseUserReal(string str, out float value) {
		value = 0f;
		if (string.IsNullOrEmpty(str))
			return false;
		str = str.Trim();
		if (float.TryParse(str, NumberStyles.Float, Invariant, out value))
			return true;
		return float.TryParse(str, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
	}

	/// <summary>Parses a number typed by a user (invariant culture, then current culture). Invalid returns 0.</summary>
	public static float ParseUserReal(string str) {
		float value;
		return TryParseUserReal(str, out value) ? value : 0f;
	}

	// ---------------------------------------------------------------------------------------------------------------
	// timeinterval (second,10,2)
	// ---------------------------------------------------------------------------------------------------------------

	/// <summary>Converts seconds to a SCORM timeinterval, e.g. 18.4 -> "P0DT0H0M18.4S". Negative values become 0.</summary>
	public static string SecondsToTimeInterval(float seconds) {
		return SecondsToTimeInterval(float.IsNaN(seconds) || float.IsInfinity(seconds) ? 0.0 : FloatToDouble(seconds));
	}

	/// <summary>Converts seconds to a SCORM timeinterval, e.g. 18.4 -> "P0DT0H0M18.4S". Negative values become 0.</summary>
	public static string SecondsToTimeInterval(double seconds) {
		if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
			seconds = 0;
		long hundredths = (long)Math.Round(seconds * 100.0, MidpointRounding.AwayFromZero);
		long days = hundredths / 8640000; hundredths %= 8640000;
		long hours = hundredths / 360000; hundredths %= 360000;
		long minutes = hundredths / 6000; hundredths %= 6000;
		decimal secs = hundredths / 100m;
		return string.Format(Invariant, "P{0}DT{1}H{2}M{3}S", days, hours, minutes, secs.ToString("0.##", Invariant));
	}

	/// <summary>Converts a SCORM timeinterval (ISO 8601 duration) to seconds. Null, empty or invalid returns 0.</summary>
	public static float TimeIntervalToSeconds(string timeInterval) {
		if (string.IsNullOrEmpty(timeInterval))
			return 0f;
		Match m = TimeIntervalRegex.Match(timeInterval.Trim());
		if (!m.Success)
			return 0f;
		double total = 0;
		total += GroupValue(m, 1) * 31557600.0;		// years (365.25 days, as in the ADL reference implementation)
		total += GroupValue(m, 2) * 2629800.0;		// months (1/12 year)
		total += GroupValue(m, 3) * 86400.0;
		total += GroupValue(m, 4) * 3600.0;
		total += GroupValue(m, 5) * 60.0;
		total += GroupValue(m, 6);
		return (float)(Math.Round(total * 100.0) / 100.0);
	}

	static double GroupValue(Match m, int group) {
		string v = m.Groups[group].Value;
		double result;
		if (v.Length > 0 && double.TryParse(v, NumberStyles.Float, Invariant, out result))
			return result;
		return 0;
	}

	// ---------------------------------------------------------------------------------------------------------------
	// time (second,10,0)
	// ---------------------------------------------------------------------------------------------------------------

	/// <summary>Formats a DateTime as a SCORM time: "yyyy-MM-ddTHH:mm:ss".</summary>
	public static string ToTimestamp(DateTime value) {
		return value.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss", Invariant);
	}

	/// <summary>Parses a SCORM time coming from the LMS. Empty or invalid returns DateTime.MinValue (never throws).</summary>
	public static DateTime ParseTimestamp(string str) {
		DateTime result;
		if (!string.IsNullOrEmpty(str) && DateTime.TryParse(str.Trim(), Invariant, DateTimeStyles.RoundtripKind, out result))
			return result;
		return DateTime.MinValue;
	}

	// ---------------------------------------------------------------------------------------------------------------
	// Enum -> SCORM vocabulary
	// ---------------------------------------------------------------------------------------------------------------

	/// <summary>Converts any of the StudentRecord enums to its SCORM vocabulary token. Unknown objects use ToString().</summary>
	public static string ToVocabulary(object value) {
		if (value == null) return "";
		if (value is StudentRecord.CompletionStatusType) return ToVocabulary((StudentRecord.CompletionStatusType)value);
		if (value is StudentRecord.CreditType) return ToVocabulary((StudentRecord.CreditType)value);
		if (value is StudentRecord.EntryType) return ToVocabulary((StudentRecord.EntryType)value);
		if (value is StudentRecord.ExitType) return ToVocabulary((StudentRecord.ExitType)value);
		if (value is StudentRecord.InteractionType) return ToVocabulary((StudentRecord.InteractionType)value);
		if (value is StudentRecord.ModeType) return ToVocabulary((StudentRecord.ModeType)value);
		if (value is StudentRecord.ResultType) return ToVocabulary((StudentRecord.ResultType)value);
		if (value is StudentRecord.SuccessStatusType) return ToVocabulary((StudentRecord.SuccessStatusType)value);
		if (value is StudentRecord.TimeLimitActionType) return ToVocabulary((StudentRecord.TimeLimitActionType)value);
		string s = value.ToString();
		return s == "not_set" ? "" : s;
	}

	public static string ToVocabulary(StudentRecord.CompletionStatusType value) {
		switch (value) {
		case StudentRecord.CompletionStatusType.completed: return "completed";
		case StudentRecord.CompletionStatusType.incomplete: return "incomplete";
		case StudentRecord.CompletionStatusType.not_attempted: return "not attempted";
		case StudentRecord.CompletionStatusType.unknown: return "unknown";
		default: return "";
		}
	}

	public static string ToVocabulary(StudentRecord.CreditType value) {
		switch (value) {
		case StudentRecord.CreditType.credit: return "credit";
		case StudentRecord.CreditType.no_credit: return "no-credit";
		default: return "";
		}
	}

	public static string ToVocabulary(StudentRecord.EntryType value) {
		switch (value) {
		case StudentRecord.EntryType.start: return "ab-initio";
		case StudentRecord.EntryType.resume: return "resume";
		default: return "";
		}
	}

	public static string ToVocabulary(StudentRecord.ExitType value) {
		switch (value) {
		case StudentRecord.ExitType.timeout: return "time-out";
		case StudentRecord.ExitType.suspend: return "suspend";
		case StudentRecord.ExitType.normal: return "normal";
		case StudentRecord.ExitType.logout: return "logout";
		default: return "";
		}
	}

	public static string ToVocabulary(StudentRecord.InteractionType value) {
		switch (value) {
		case StudentRecord.InteractionType.true_false: return "true-false";
		case StudentRecord.InteractionType.choice: return "choice";
		case StudentRecord.InteractionType.fill_in: return "fill-in";
		case StudentRecord.InteractionType.long_fill_in: return "long-fill-in";
		case StudentRecord.InteractionType.likert: return "likert";
		case StudentRecord.InteractionType.matching: return "matching";
		case StudentRecord.InteractionType.performance: return "performance";
		case StudentRecord.InteractionType.sequencing: return "sequencing";
		case StudentRecord.InteractionType.numeric: return "numeric";
		case StudentRecord.InteractionType.other: return "other";
		default: return "";
		}
	}

	public static string ToVocabulary(StudentRecord.ModeType value) {
		switch (value) {
		case StudentRecord.ModeType.browse: return "browse";
		case StudentRecord.ModeType.normal: return "normal";
		case StudentRecord.ModeType.review: return "review";
		default: return "";
		}
	}

	/// <summary>Result vocabulary. ResultType.estimate returns "" because the caller must send the numeric estimate.</summary>
	public static string ToVocabulary(StudentRecord.ResultType value) {
		switch (value) {
		case StudentRecord.ResultType.correct: return "correct";
		case StudentRecord.ResultType.incorrect: return "incorrect";
		case StudentRecord.ResultType.unanticipated: return "unanticipated";
		case StudentRecord.ResultType.neutral: return "neutral";
		default: return "";
		}
	}

	public static string ToVocabulary(StudentRecord.SuccessStatusType value) {
		switch (value) {
		case StudentRecord.SuccessStatusType.passed: return "passed";
		case StudentRecord.SuccessStatusType.failed: return "failed";
		case StudentRecord.SuccessStatusType.unknown: return "unknown";
		default: return "";
		}
	}

	public static string ToVocabulary(StudentRecord.TimeLimitActionType value) {
		switch (value) {
		case StudentRecord.TimeLimitActionType.exit_message: return "exit,message";
		case StudentRecord.TimeLimitActionType.continue_message: return "continue,message";
		case StudentRecord.TimeLimitActionType.exit_no_message: return "exit,no message";
		case StudentRecord.TimeLimitActionType.continue_no_message: return "continue,no message";
		default: return "";
		}
	}

	// ---------------------------------------------------------------------------------------------------------------
	// SCORM vocabulary -> enum
	// ---------------------------------------------------------------------------------------------------------------

	/// <summary>Converts a cmi.interactions.n.result value. A numeric value returns ResultType.estimate and sets estimate.</summary>
	public static StudentRecord.ResultType StringToResultType(string str, out float estimate) {
		estimate = 0f;
		switch (str) {
		case "correct": return StudentRecord.ResultType.correct;
		case "incorrect": return StudentRecord.ResultType.incorrect;
		case "neutral": return StudentRecord.ResultType.neutral;
		case "unanticipated": return StudentRecord.ResultType.unanticipated;
		}
		if (!string.IsNullOrEmpty(str) && float.TryParse(str.Trim(), NumberStyles.Float, Invariant, out estimate))
			return StudentRecord.ResultType.estimate;
		estimate = 0f;
		return StudentRecord.ResultType.not_set;
	}

	public static StudentRecord.InteractionType StringToInteractionType(string str) {
		switch (str) {
		case "true-false": return StudentRecord.InteractionType.true_false;
		case "choice": return StudentRecord.InteractionType.choice;
		case "fill-in": return StudentRecord.InteractionType.fill_in;
		case "long-fill-in": return StudentRecord.InteractionType.long_fill_in;
		case "matching": return StudentRecord.InteractionType.matching;
		case "performance": return StudentRecord.InteractionType.performance;
		case "sequencing": return StudentRecord.InteractionType.sequencing;
		case "likert": return StudentRecord.InteractionType.likert;
		case "numeric": return StudentRecord.InteractionType.numeric;
		case "other": return StudentRecord.InteractionType.other;
		default: return StudentRecord.InteractionType.not_set;
		}
	}

	public static StudentRecord.EntryType StringToEntryType(string str) {
		switch (str) {
		case "ab-initio": return StudentRecord.EntryType.start;
		case "resume": return StudentRecord.EntryType.resume;
		default: return StudentRecord.EntryType.not_set;
		}
	}

	public static StudentRecord.TimeLimitActionType StringToTimeLimitActionType(string str) {
		switch (str) {
		case "continue,message": return StudentRecord.TimeLimitActionType.continue_message;
		case "continue,no message": return StudentRecord.TimeLimitActionType.continue_no_message;
		case "exit,message": return StudentRecord.TimeLimitActionType.exit_message;
		case "exit,no message": return StudentRecord.TimeLimitActionType.exit_no_message;
		default: return StudentRecord.TimeLimitActionType.not_set;
		}
	}

	public static StudentRecord.CompletionStatusType StringToCompletionStatusType(string str) {
		switch (str) {
		case "completed": return StudentRecord.CompletionStatusType.completed;
		case "incomplete": return StudentRecord.CompletionStatusType.incomplete;
		case "not attempted": return StudentRecord.CompletionStatusType.not_attempted;
		case "unknown": return StudentRecord.CompletionStatusType.unknown;
		default: return StudentRecord.CompletionStatusType.not_set;
		}
	}

	public static StudentRecord.SuccessStatusType StringToSuccessStatusType(string str) {
		switch (str) {
		case "passed": return StudentRecord.SuccessStatusType.passed;
		case "failed": return StudentRecord.SuccessStatusType.failed;
		case "unknown": return StudentRecord.SuccessStatusType.unknown;
		default: return StudentRecord.SuccessStatusType.not_set;
		}
	}

	public static StudentRecord.CreditType StringToCreditType(string str) {
		switch (str) {
		case "credit": return StudentRecord.CreditType.credit;
		case "no-credit": return StudentRecord.CreditType.no_credit;
		default: return StudentRecord.CreditType.not_set;
		}
	}

	public static StudentRecord.ModeType StringToModeType(string str) {
		switch (str) {
		case "browse": return StudentRecord.ModeType.browse;
		case "normal": return StudentRecord.ModeType.normal;
		case "review": return StudentRecord.ModeType.review;
		default: return StudentRecord.ModeType.not_set;
		}
	}
}
