/***********************************************************************************************************************
 * Unity-SCORM Integration Kit
 *
 * Command line (and menu) entry points: WebGL build with the SCORM template, SCORM packaging and zip validation
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with
 * the License. You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 **********************************************************************************************************************/

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Batchmode entry points (all of them call EditorApplication.Exit(0) on success and Exit(1) on failure):
///
///   Unity -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod ScormBuildCli.BuildAndPackage
///         -scormEdition 3rd|4th -scormZip Builds/MyCourse.zip [-scormBuildDir Builds/WebGL]
///         [-scormIdentifier id] [-scormTitle t] [-scormDescription d] [-scoTitle t] [-scormLaunchData d]
///         [-scormCompletionThreshold 0.8] [-scormCompletedByMeasure true] [-scormMinProgressMeasure 0.8]
///         [-scormTimeLimitAction "exit,message"] [-scormTimeLimitSecs 3600] [-webglCompression disabled|gzip-fallback]
///
///   ScormBuildCli.BuildWebGL   build only          ScormBuildCli.Package   package an existing build
///   ScormBuildCli.ValidateZip  -scormZip path: checks the zip and validates the manifest against the XSD it contains
/// </summary>
public static class ScormBuildCli {

	public const string DefaultBuildDir = "Builds/WebGL";
	public const string DefaultZip = "Builds/SCORM_Package.zip";
	public const string TemplateName = "PROJECT:SCORM";
	const string FallbackScene = "Assets/SCORM/_Scenes/TestApp.unity";

	// ------------------------------------------------------------------------------------------------------------
	// -executeMethod entry points
	// ------------------------------------------------------------------------------------------------------------

	public static void BuildWebGL() {
		Run(() => BuildWebGLPlayer(GetArg("-scormBuildDir", DefaultBuildDir), GetArg("-webglCompression", "gzip-fallback")));
	}

	public static void Package() {
		Run(() => PackageBuild(GetArg("-scormBuildDir", DefaultBuildDir), GetArg("-scormZip", DefaultZip), SettingsFromCommandLine()));
	}

	public static void BuildAndPackage() {
		Run(() => {
			string buildDir = GetArg("-scormBuildDir", DefaultBuildDir);
			return BuildWebGLPlayer(buildDir, GetArg("-webglCompression", "gzip-fallback"))
				&& PackageBuild(buildDir, GetArg("-scormZip", DefaultZip), SettingsFromCommandLine());
		});
	}

	public static void ValidateZip() {
		Run(() => {
			string zip = GetArg("-scormZip", DefaultZip);
			string report;
			var errors = ScormPackager.ValidateZip(zip, out report);
			Debug.Log("[SCORM] ValidateZip " + zip + "\n" + report);
			foreach (string e in errors)
				Debug.LogError("[SCORM] " + e);
			Debug.Log(errors.Count == 0 ? "[SCORM] SCORM_VALIDATE_OK " + zip : "[SCORM] SCORM_VALIDATE_FAILED " + zip + " (" + errors.Count + " problems)");
			return errors.Count == 0;
		});
	}

	// ------------------------------------------------------------------------------------------------------------
	// Reusable (no Exit) operations, also used by the SCORM export window
	// ------------------------------------------------------------------------------------------------------------

	/// <summary>Builds the enabled scenes for WebGL with the SCORM template into buildDir.</summary>
	public static bool BuildWebGLPlayer(string buildDir, string compression) {
		ConfigureWebGL(compression);

		string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
		if (scenes.Length == 0)
			scenes = new[] { FallbackScene };

		CleanPreviousBuild(buildDir);

		BuildPlayerOptions options = new BuildPlayerOptions();
		options.scenes = scenes;
		options.locationPathName = buildDir;
		options.target = BuildTarget.WebGL;
		options.targetGroup = BuildTargetGroup.WebGL;
		options.options = BuildOptions.None;

		Debug.Log("[SCORM] Building WebGL (" + string.Join(", ", scenes) + ") -> " + buildDir);
		BuildReport report = BuildPipeline.BuildPlayer(options);
		BuildSummary summary = report.summary;
		Debug.Log("[SCORM] Build result: " + summary.result + ", " + summary.totalErrors + " errors, " + summary.totalWarnings + " warnings, " + summary.totalSize + " bytes, " + summary.totalTime);
		foreach (BuildStep step in report.steps)
			foreach (BuildStepMessage message in step.messages)
				if (message.type == LogType.Error || message.type == LogType.Exception)
					Debug.Log("[SCORM] Build step '" + step.name + "' " + message.type + ": " + message.content);
		return summary.result == BuildResult.Succeeded;
	}

	/// <summary>Packages an existing WebGL build as a SCORM zip.</summary>
	public static bool PackageBuild(string buildDir, string zipPath, ScormPackageSettings settings) {
		ScormPackageResult result = ScormPackager.Package(buildDir, zipPath, settings);
		foreach (string w in result.warnings)
			Debug.LogWarning("[SCORM] " + w);
		Debug.Log("[SCORM] SCORM_PACKAGE_OK " + result.zipPath + " (" + settings.edition + ", " + result.contentFileCount +
			" content files, XSD from " + result.xsdDirectory.Replace('\\', '/') + ", " + new FileInfo(result.zipPath).Length + " bytes)");
		return true;
	}

	/// <summary>SCORM template, compression and decompression fallback (Unity stores them in ProjectSettings).</summary>
	public static void ConfigureWebGL(string compression) {
		PlayerSettings.WebGL.template = TemplateName;
		if (string.Equals(compression, "disabled", StringComparison.OrdinalIgnoreCase)) {
			PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
			PlayerSettings.WebGL.decompressionFallback = false;
		} else {
			// Gzip + decompression fallback: the loader decompresses in JavaScript, so it works on LMSs that do not
			// send Content-Encoding headers (e.g. Moodle pluginfile.php).
			PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
			PlayerSettings.WebGL.decompressionFallback = true;
		}
	}

	public static ScormPackageSettings SettingsFromCommandLine() {
		ScormPackageSettings settings = ScormPackageSettings.FromPlayerSettings();
		settings.ApplyCommandLine(Environment.GetCommandLineArgs());
		return settings;
	}

	// Deletes a previous WebGL build in that folder (only if it looks like one) so no stale files reach the package.
	static void CleanPreviousBuild(string buildDir) {
		if (!Directory.Exists(buildDir))
			return;
		bool looksLikeWebGL = File.Exists(Path.Combine(buildDir, "index.html")) && Directory.Exists(Path.Combine(buildDir, "Build"));
		if (!looksLikeWebGL) {
			if (Directory.GetFileSystemEntries(buildDir).Length > 0)
				throw new IOException("Build folder is not empty and does not look like a WebGL build: " + buildDir);
			return;
		}
		Directory.Delete(buildDir, true);
	}

	static string GetArg(string name, string fallback) {
		string value;
		return ScormPackageSettings.TryGetArg(Environment.GetCommandLineArgs(), name, out value) ? value : fallback;
	}

	static void Run(Func<bool> action) {
		bool ok = false;
		try {
			ok = action();
		} catch (Exception e) {
			Debug.LogError("[SCORM] " + e);
		}
		if (Application.isBatchMode)
			EditorApplication.Exit(ok ? 0 : 1);
		else if (!ok)
			Debug.LogError("[SCORM] Operation failed, see the Console.");
	}
}
