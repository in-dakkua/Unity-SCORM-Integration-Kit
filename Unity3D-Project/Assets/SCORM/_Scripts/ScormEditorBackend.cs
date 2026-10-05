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
public static class ScormEditorBackend {

	/// <summary>SCORM 2004 error code 401: Undefined Data Model Element.</summary>
	public const int ErrorUndefinedElement = 401;

	static readonly Dictionary<string, string> data = new Dictionary<string, string>();
	static readonly Regex CollectionIndex = new Regex(@"\.(\d+)\.", RegexOptions.CultureInvariant);

	/// <summary>The data model as it is currently stored.</summary>
	public static IDictionary<string, string> Data { get { return data; } }

	/// <summary>Error code of the last call (0 = no error).</summary>
	public static int LastError { get; private set; }

	public static bool IsInitialized { get; private set; }
	public static bool IsTerminated { get; private set; }
	public static int CommitCount { get; private set; }

	/// <summary>Restores the seed data and the initial state.</summary>
	public static void Reset() {
		data.Clear();
		LastError = 0;
		IsInitialized = false;
		IsTerminated = false;
		CommitCount = 0;
		Seed();
	}

	public static bool Initialize() {
		Reset();
		IsInitialized = true;
		return true;
	}

	public static string GetValue(string identifier) {
		string value;
		if (identifier != null && data.TryGetValue(identifier, out value)) {
			LastError = 0;
			return value ?? "";
		}
		LastError = ErrorUndefinedElement;
		return "";
	}

	public static bool SetValue(string identifier, string value) {
		if (string.IsNullOrEmpty(identifier)) {
			LastError = ErrorUndefinedElement;
			return false;
		}
		LastError = 0;
		data[identifier] = value ?? "";
		UpdateCounts(identifier);
		return true;
	}

	public static bool Commit() {
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
		case ErrorUndefinedElement: return "Undefined Data Model Element";
		default: return "General Exception";
		}
	}

	// Like a real LMS, writing cmi.interactions.N.x grows cmi.interactions._count to N+1 (also for nested collections).
	static void UpdateCounts(string identifier) {
		foreach (Match m in CollectionIndex.Matches(identifier)) {
			string countKey = identifier.Substring(0, m.Index) + "._count";
			int index = int.Parse(m.Groups[1].Value);
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
