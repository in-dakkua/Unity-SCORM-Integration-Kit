/***********************************************************************************************************************
 * Unity-SCORM Integration Kit
 *
 * In-memory SCORM 2004 API used in the Unity Editor and on non-WebGL platforms (same seed data as ScormSimulator.js)
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with
 * the License. You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 **********************************************************************************************************************/

using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// In-memory SCORM 2004 Run-Time API. ScormAPIWrapper uses it whenever the code does not run inside a WebGL player,
/// so Play Mode in the Editor and EditMode tests work without a browser or an LMS.
/// </summary>
/// <remarks>
/// It behaves like a strict LMS for the rules the kit depends on: collection indexes must be contiguous (351),
/// cmi.objectives.n.id / cmi.interactions.n.id must be set before the other fields of that index (408), an objective
/// id cannot be changed nor duplicated (351), and calls before Initialize / after Terminate fail (122/123, 132/133,
/// 142/143). After Terminate with cmi.exit = "suspend", the next Initialize resumes the same data with
/// cmi.entry = "resume" (a new launch of a suspended attempt); any other exit starts a new attempt from the seed data.
/// </remarks>
public static class ScormEditorBackend {

	/// <summary>SCORM 2004 error code 401: Undefined Data Model Element.</summary>
	public const int ErrorUndefinedElement = 401;
	public const int ErrorGetBeforeInit = 122;
	public const int ErrorGetAfterTerm = 123;
	public const int ErrorSetBeforeInit = 132;
	public const int ErrorSetAfterTerm = 133;
	public const int ErrorCommitBeforeInit = 142;
	public const int ErrorCommitAfterTerm = 143;
	/// <summary>SCORM 2004 error code 351: General Set Failure (index gap, objective id changed or duplicated).</summary>
	public const int ErrorGeneralSetFailure = 351;
	/// <summary>SCORM 2004 error code 408: Data Model Dependency Not Established (field written before the id).</summary>
	public const int ErrorDependencyNotEstablished = 408;

	static readonly Dictionary<string, string> data = new Dictionary<string, string>();
	static readonly Regex CollectionIndex = new Regex(@"\.(\d+)\.", RegexOptions.CultureInvariant);
	static readonly Regex TopLevelEntry = new Regex(@"^(cmi\.(?:objectives|interactions))\.(\d+)\.(.+)$", RegexOptions.CultureInvariant);
	static bool seeded;
	static readonly Dictionary<string, int> injectedErrors = new Dictionary<string, int>();

	/// <summary>The data model as it is currently stored.</summary>
	public static IDictionary<string, string> Data { get { return data; } }

	/// <summary>Error code of the last call (0 = no error).</summary>
	public static int LastError { get; private set; }

	public static bool IsInitialized { get; private set; }
	public static bool IsTerminated { get; private set; }
	public static int CommitCount { get; private set; }

	/// <summary>Restores the seed data and the initial state (a brand new attempt).</summary>
	public static void Reset() {
		data.Clear();
		LastError = 0;
		IsInitialized = false;
		IsTerminated = false;
		CommitCount = 0;
		injectedErrors.Clear();
		Seed();
		seeded = true;
	}

	/// <summary>
	/// Starts a session. First call: seed data. After Terminate: resumes the data if cmi.exit was "suspend",
	/// otherwise starts a new attempt from the seed data.
	/// </summary>
	public static bool Initialize() {
		if (!seeded) {
			Reset();
		} else if (IsTerminated) {
			string exit;
			bool suspended = data.TryGetValue("cmi.exit", out exit) && exit == "suspend";
			if (suspended) {
				data.Remove("cmi.exit");
				data.Remove("cmi.session_time");
				data["cmi.entry"] = "resume";
			} else {
				Reset();
			}
		}
		IsTerminated = false;
		IsInitialized = true;
		LastError = 0;
		return true;
	}

	public static string GetValue(string identifier) {
		if (!IsInitialized) {
			LastError = IsTerminated ? ErrorGetAfterTerm : ErrorGetBeforeInit;
			return "";
		}
		string value;
		if (identifier != null && data.TryGetValue(identifier, out value)) {
			LastError = 0;
			return value ?? "";
		}
		LastError = ErrorUndefinedElement;
		return "";
	}

	public static bool SetValue(string identifier, string value) {
		if (!IsInitialized) {
			LastError = IsTerminated ? ErrorSetAfterTerm : ErrorSetBeforeInit;
			return false;
		}
		if (string.IsNullOrEmpty(identifier)) {
			LastError = ErrorUndefinedElement;
			return false;
		}
		int error;
		if (!injectedErrors.TryGetValue(identifier, out error))
			error = CheckCollectionRules(identifier, value ?? "");
		if (error != 0) {
			LastError = error;
			return false;
		}
		LastError = 0;
		data[identifier] = value ?? "";
		UpdateCounts(identifier);
		return true;
	}

	/// <summary>
	/// Testing aid: every SetValue on identifier fails with errorCode (e.g. 406, 351) until ClearInjectedErrors or Reset.
	/// Simulates an LMS that rejects a value.
	/// </summary>
	public static void InjectSetError(string identifier, int errorCode) {
		injectedErrors[identifier] = errorCode;
	}

	public static void ClearInjectedErrors() {
		injectedErrors.Clear();
	}

	public static bool Commit() {
		if (!IsInitialized) {
			LastError = IsTerminated ? ErrorCommitAfterTerm : ErrorCommitBeforeInit;
			return false;
		}
		LastError = 0;
		CommitCount++;
		return true;
	}

	public static bool Terminate() {
		LastError = 0;
		IsTerminated = true;
		IsInitialized = false;
		return true;
	}

	public static string GetErrorString(int code) {
		switch (code) {
		case 0: return "No Error";
		case ErrorGetBeforeInit: return "Retrieve Data Before Initialization";
		case ErrorGetAfterTerm: return "Retrieve Data After Termination";
		case ErrorSetBeforeInit: return "Store Data Before Initialization";
		case ErrorSetAfterTerm: return "Store Data After Termination";
		case ErrorCommitBeforeInit: return "Commit Before Initialization";
		case ErrorCommitAfterTerm: return "Commit After Termination";
		case ErrorGeneralSetFailure: return "General Set Failure";
		case ErrorUndefinedElement: return "Undefined Data Model Element";
		case ErrorDependencyNotEstablished: return "Data Model Dependency Not Established";
		case 406: return "Data Model Element Type Mismatch";
		case 407: return "Data Model Element Value Out Of Range";
		default: return "General Exception";
		}
	}

	static int CheckCollectionRules(string identifier, string value) {
		// Indexes must be contiguous: n <= _count at every collection level.
		foreach (Match m in CollectionIndex.Matches(identifier)) {
			string countKey = identifier.Substring(0, m.Index) + "._count";
			string current;
			int count = data.TryGetValue(countKey, out current) ? ScormFormat.ParseInt(current) : 0;
			if (int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) > count)
				return ErrorGeneralSetFailure;
		}

		Match entry = TopLevelEntry.Match(identifier);
		if (!entry.Success)
			return 0;
		string prefix = entry.Groups[1].Value + "." + entry.Groups[2].Value + ".";
		string field = entry.Groups[3].Value;
		string existingId;
		bool hasId = data.TryGetValue(prefix + "id", out existingId) && !string.IsNullOrEmpty(existingId);

		if (field != "id")
			return hasId ? 0 : ErrorDependencyNotEstablished;

		if (entry.Groups[1].Value == "cmi.objectives") {
			if (hasId && existingId != value)
				return ErrorGeneralSetFailure;
			int count = ScormFormat.ParseInt(GetRaw("cmi.objectives._count"));
			for (int i = 0; i < count; i++) {
				if (i.ToString(System.Globalization.CultureInfo.InvariantCulture) == entry.Groups[2].Value)
					continue;
				if (GetRaw("cmi.objectives." + i + ".id") == value)
					return ErrorGeneralSetFailure;
			}
		}
		return 0;
	}

	static string GetRaw(string key) {
		string v;
		return data.TryGetValue(key, out v) ? v : "";
	}

	// Like a real LMS, writing cmi.interactions.N.x grows cmi.interactions._count to N+1 (also for nested collections).
	static void UpdateCounts(string identifier) {
		foreach (Match m in CollectionIndex.Matches(identifier)) {
			string countKey = identifier.Substring(0, m.Index) + "._count";
			int index = int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
			string current;
			int count = 0;
			if (data.TryGetValue(countKey, out current))
				count = ScormFormat.ParseInt(current);
			if (index + 1 > count)
				data[countKey] = (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
		}
	}

	static void Seed() {
		data["cmi._version"] = "1.0";

		data["cmi.comments_from_learner._count"] = "1";
		data["cmi.comments_from_learner.0.comment"] = "An example comment from learner";
		data["cmi.comments_from_learner.0.location"] = "Location 1";
		data["cmi.comments_from_learner.0.timestamp"] = "2015-09-07T09:00:00";
		data["cmi.comments_from_lms._count"] = "1";
		data["cmi.comments_from_lms.0.comment"] = "test comment from LMS";
		data["cmi.comments_from_lms.0.location"] = "test location string";
		data["cmi.comments_from_lms.0.timestamp"] = "2014-09-07T09:00:00";

		data["cmi.completion_status"] = "incomplete";
		data["cmi.completion_threshold"] = "0.9";
		data["cmi.credit"] = "credit";
		data["cmi.entry"] = "ab-initio";

		data["cmi.interactions._count"] = "1";
		data["cmi.interactions.0.id"] = "urn:STALS:interaction-id-0";
		data["cmi.interactions.0.type"] = "true-false";
		data["cmi.interactions.0.objectives._count"] = "2";
		data["cmi.interactions.0.objectives.0.id"] = "Objective1";
		data["cmi.interactions.0.objectives.1.id"] = "Objective2";
		data["cmi.interactions.0.timestamp"] = "2015-09-07T09:00:00";
		data["cmi.interactions.0.correct_responses._count"] = "1";
		data["cmi.interactions.0.correct_responses.0.pattern"] = "true";
		data["cmi.interactions.0.weighting"] = "1.0";
		data["cmi.interactions.0.learner_response"] = "false";
		data["cmi.interactions.0.result"] = "incorrect";
		data["cmi.interactions.0.latency"] = "P0DT0H2M18S";
		data["cmi.interactions.0.description"] = "Interaction description";

		data["cmi.launch_data"] = "Launch data from the ims manifest file will be here.";

		data["cmi.learner_id"] = "rdescartes";
		data["cmi.learner_name"] = "Rene Descartes";

		data["cmi.learner_preference.audio_captioning"] = "-1";
		data["cmi.learner_preference.audio_level"] = "80.0";
		data["cmi.learner_preference.delivery_speed"] = "1.0";
		data["cmi.learner_preference.language"] = "en-US";

		data["cmi.location"] = "Bookmarked location id";
		data["cmi.max_time_allowed"] = "P0DT1H0M0S";
		data["cmi.mode"] = "normal";

		data["cmi.objectives._count"] = "2";
		data["cmi.objectives.0.id"] = "Objective1";
		data["cmi.objectives.0.score.scaled"] = "0.50";
		data["cmi.objectives.0.score.raw"] = "45.0";
		data["cmi.objectives.0.score.min"] = "0.0";
		data["cmi.objectives.0.score.max"] = "90.0";
		data["cmi.objectives.0.success_status"] = "failed";
		data["cmi.objectives.0.completion_status"] = "completed";
		data["cmi.objectives.0.progress_measure"] = "0.9";
		data["cmi.objectives.0.description"] = "Understand how to use the SCORM API.";
		data["cmi.objectives.1.id"] = "Objective2";
		data["cmi.objectives.1.score.scaled"] = "0.80";
		data["cmi.objectives.1.score.raw"] = "80.0";
		data["cmi.objectives.1.score.min"] = "0.0";
		data["cmi.objectives.1.score.max"] = "100.0";
		data["cmi.objectives.1.success_status"] = "passed";
		data["cmi.objectives.1.completion_status"] = "completed";
		data["cmi.objectives.1.progress_measure"] = "1";
		data["cmi.objectives.1.description"] = "Understand how to use the SCORM API in Unity3D.";

		data["cmi.progress_measure"] = "0.3";
		data["cmi.scaled_passing_score"] = "0.8";

		data["cmi.score.max"] = "100.0";
		data["cmi.score.min"] = "0.0";
		data["cmi.score.raw"] = "75.5";
		data["cmi.score.scaled"] = "0.755";

		data["cmi.success_status"] = "unknown";
		data["cmi.suspend_data"] = "You set the suspend data.";

		data["cmi.time_limit_action"] = "continue,no message";
		data["cmi.total_time"] = "P0DT0H27M10S";
	}
}
