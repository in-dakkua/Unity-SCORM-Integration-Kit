/***********************************************************************************************************************
 * Unity-SCORM Integration Kit
 *
 * Settings of a SCORM 2004 package (imsmanifest.xml values), loaded from the export window (PlayerPrefs) or the CLI
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with
 * the License. You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 **********************************************************************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>Values written to imsmanifest.xml.</summary>
[Serializable]
public class ScormPackageSettings {

	/// <summary>PlayerPrefs keys used by the SCORM export window (kept from the original kit).</summary>
	public const string PrefIdentifier = "Manifest_Identifier";
	public const string PrefCourseTitle = "Course_Title";
	public const string PrefCourseDescription = "Course_Description";
	public const string PrefScoTitle = "SCO_Title";
	public const string PrefLaunchData = "Data_From_Lms";
	public const string PrefCompletedByMeasure = "completedByMeasure";
	public const string PrefMinProgressMeasure = "minProgressMeasure";
	public const string PrefTimeLimitAction = "Time_Limit_Action";
	public const string PrefTimeLimitSecs = "Time_Limit_Secs";
	public const string PrefEdition = "Scorm_Edition";
	public const string PrefCompletionThreshold = "Completion_Threshold";

	/// <summary>Index used by the export window popup (0 = not set).</summary>
	public static readonly string[] TimeLimitActions = { "", "exit,message", "exit,no message", "continue,message", "continue,no message" };

	public ScormEdition edition = ScormEdition.Scorm2004_3rd;
	public string identifier = "com.company.scormcontent";
	public string courseTitle = "SCORM Content";
	public string courseDescription = "";
	public string scoTitle = "SCORM Content";
	public string launchData = "";

	/// <summary>3rd Edition: adlcp:completionThreshold (0..1). Null = not written.</summary>
	public float? completionThreshold;

	/// <summary>4th Edition: adlcp:completionThreshold/@completedByMeasure. False = element not written.</summary>
	public bool completedByMeasure;

	/// <summary>4th Edition: adlcp:completionThreshold/@minProgressMeasure (0..1).</summary>
	public float minProgressMeasure = 1f;

	/// <summary>adlcp:timeLimitAction ("" = not written).</summary>
	public string timeLimitAction = "";

	/// <summary>imsss:limitConditions/@attemptAbsoluteDurationLimit in seconds (0 = not written).</summary>
	public float timeLimitSecs;

	/// <summary>Language of the LOM description.</summary>
	public string language = "en-US";

	/// <summary>Defaults taken from PlayerSettings (product name, application identifier).</summary>
	public static ScormPackageSettings FromPlayerSettings() {
		ScormPackageSettings s = new ScormPackageSettings();
		string product = string.IsNullOrEmpty(Application.productName) ? "SCORM Content" : Application.productName;
		s.courseTitle = product;
		s.scoTitle = product;
		string company = string.IsNullOrEmpty(Application.companyName) ? "company" : Application.companyName;
		s.identifier = SanitizeIdentifier("com." + company + "." + product);
		return s;
	}

	/// <summary>Values saved by the SCORM export window; empty values fall back to PlayerSettings.</summary>
	public static ScormPackageSettings FromPlayerPrefs() {
		ScormPackageSettings s = FromPlayerSettings();
		s.edition = PlayerPrefs.GetInt(PrefEdition, 0) == 1 ? ScormEdition.Scorm2004_4th : ScormEdition.Scorm2004_3rd;
		s.identifier = NonEmpty(PlayerPrefs.GetString(PrefIdentifier), s.identifier);
		s.courseTitle = NonEmpty(PlayerPrefs.GetString(PrefCourseTitle), s.courseTitle);
		s.courseDescription = PlayerPrefs.GetString(PrefCourseDescription);
		s.scoTitle = NonEmpty(PlayerPrefs.GetString(PrefScoTitle), s.courseTitle);
		s.launchData = PlayerPrefs.GetString(PrefLaunchData);
		s.completedByMeasure = PlayerPrefs.GetInt(PrefCompletedByMeasure) != 0;
		s.minProgressMeasure = PlayerPrefs.GetFloat(PrefMinProgressMeasure, 1f);
		s.completionThreshold = ParseNullableReal(PlayerPrefs.GetString(PrefCompletionThreshold));
		int action = PlayerPrefs.GetInt(PrefTimeLimitAction);
		s.timeLimitAction = action >= 0 && action < TimeLimitActions.Length ? TimeLimitActions[action] : "";
		s.timeLimitSecs = ScormFormat.ParseUserReal(PlayerPrefs.GetString(PrefTimeLimitSecs));
		return s;
	}

	/// <summary>Overrides values with the -scorm* command line arguments (see ScormBuildCli).</summary>
	public void ApplyCommandLine(string[] args) {
		string v;
		if (TryGetArg(args, "-scormEdition", out v)) edition = ParseEdition(v);
		if (TryGetArg(args, "-scormIdentifier", out v)) identifier = v;
		if (TryGetArg(args, "-scormTitle", out v)) { courseTitle = v; if (!HasArg(args, "-scoTitle")) scoTitle = v; }
		if (TryGetArg(args, "-scormDescription", out v)) courseDescription = v;
		if (TryGetArg(args, "-scoTitle", out v)) scoTitle = v;
		if (TryGetArg(args, "-scormLaunchData", out v)) launchData = v;
		if (TryGetArg(args, "-scormCompletionThreshold", out v)) completionThreshold = ParseNullableReal(v);
		if (TryGetArg(args, "-scormCompletedByMeasure", out v)) completedByMeasure = v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
		if (TryGetArg(args, "-scormMinProgressMeasure", out v)) minProgressMeasure = ScormFormat.ParseReal(v);
		if (TryGetArg(args, "-scormTimeLimitAction", out v)) timeLimitAction = v;
		if (TryGetArg(args, "-scormTimeLimitSecs", out v)) timeLimitSecs = ScormFormat.ParseReal(v);
		if (TryGetArg(args, "-scormLanguage", out v)) language = v;
	}

	/// <summary>Returns the problems that would make the manifest invalid (empty list = valid).</summary>
	public List<string> Validate() {
		List<string> errors = new List<string>();
		if (string.IsNullOrEmpty(identifier))
			errors.Add("The manifest identifier is empty.");
		else if (SanitizeIdentifier(identifier) != identifier)
			errors.Add("The manifest identifier '" + identifier + "' is not a valid xs:ID (letters, digits, '.', '-', '_'; must start with a letter or '_'). Suggested: '" + SanitizeIdentifier(identifier) + "'.");
		if (string.IsNullOrEmpty(courseTitle))
			errors.Add("The course title is empty.");
		if (string.IsNullOrEmpty(scoTitle))
			errors.Add("The SCO title is empty.");
		if (completionThreshold.HasValue && (completionThreshold.Value < 0f || completionThreshold.Value > 1f))
			errors.Add("completionThreshold must be between 0 and 1.");
		if (minProgressMeasure < 0f || minProgressMeasure > 1f)
			errors.Add("minProgressMeasure must be between 0 and 1.");
		if (Array.IndexOf(TimeLimitActions, timeLimitAction ?? "") < 0)
			errors.Add("timeLimitAction must be one of: exit,message | exit,no message | continue,message | continue,no message.");
		if (timeLimitSecs < 0f)
			errors.Add("The time limit cannot be negative.");
		return errors;
	}

	/// <summary>Turns any string into a valid xs:ID / NCName ("my app" -> "my_app", "1app" -> "_1app").</summary>
	public static string SanitizeIdentifier(string value) {
		if (string.IsNullOrEmpty(value))
			return "_";
		StringBuilder sb = new StringBuilder(value.Length + 1);
		foreach (char c in value) {
			bool valid = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '-' || c == '_';
			sb.Append(valid ? c : '_');
		}
		char first = sb[0];
		if (!((first >= 'a' && first <= 'z') || (first >= 'A' && first <= 'Z') || first == '_'))
			sb.Insert(0, '_');
		return sb.ToString();
	}

	public static ScormEdition ParseEdition(string value) {
		if (value == null) return ScormEdition.Scorm2004_3rd;
		value = value.Trim().ToLowerInvariant();
		return value == "4th" || value == "4" || value == "scorm2004_4th" ? ScormEdition.Scorm2004_4th : ScormEdition.Scorm2004_3rd;
	}

	public static bool TryGetArg(string[] args, string name, out string value) {
		value = null;
		if (args == null) return false;
		for (int i = 0; i < args.Length - 1; i++) {
			if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) {
				value = args[i + 1];
				return true;
			}
		}
		return false;
	}

	static bool HasArg(string[] args, string name) {
		string ignored;
		return TryGetArg(args, name, out ignored);
	}

	static string NonEmpty(string value, string fallback) {
		return string.IsNullOrEmpty(value) ? fallback : value;
	}

	static float? ParseNullableReal(string value) {
		float result;
		if (ScormFormat.TryParseUserReal(value, out result))
			return result;
		return null;
	}
}
