/***********************************************************************************************************************
 * Unity-SCORM Integration Kit
 *
 * Unity3D Editor Plugin for in Unity3D functions (creation, and export)
 *
 * Copyright (C) 2015, Richard Stals (http://stals.com.au)
 * ==========================================
 *
 *
 * Derived from:
 * Unity-SCORM Integration Toolkit Version 1.0 Beta
 * ==========================================
 *
 * Copyright (C) 2011, by ADL (Advance Distributed Learning). (http://www.adlnet.gov)
 * http://www.adlnet.gov/UnityScormIntegration/
 *
 ***********************************************************************************************************************
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with
 * the License. You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on
 * an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the
 * specific language governing permissions and limitations under the License.
 *
 **********************************************************************************************************************/

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using System.IO;
using System;

/// <summary>
/// This class handles the editor window for the Unity-SCORM Integration Kit
/// </summary>
public class ScormExport : EditorWindow {

	public GUISkin skin;

	static bool foldout1,foldout2;
	static ScormExport window;
	static Vector2 scrollview;

    /// <summary>
	/// The menu item to allow the creation of the export SCORM package
    /// </summary>
    [MenuItem("SCORM/Export SCORM Package",false,0)]
    static void ShowWindow() {
        window = (ScormExport)EditorWindow.GetWindow (typeof (ScormExport));
		window.Show ();
		foldout1=foldout2 = true;
    }

	/// <summary>
	/// Get existing open window or if none, make a new one.
	/// </summary>
	static void Init () {
		window = (ScormExport)EditorWindow.GetWindow (typeof (ScormExport));
		window.ShowAuxWindow();
	}

	/// <summary>
	/// The menu item to allow the initialisation of a scene to allow integration with SCORM
	/// </summary>
	[MenuItem("SCORM/Create SCORM Manager",false,0)]
    static void CreateManager() {
		GameObject manager = GameObject.Find("ScormManager");
		if(manager == null) {
        	manager = (GameObject)UnityEditor.SceneView.Instantiate(Resources.Load("ScormManager"));
			manager.name = "ScormManager";
			EditorUtility.DisplayDialog("The SCORM Manager has been added to the scene","Remember to place objects that need messages from the ScormManager under it in the scene heirarchy. It will send the message 'Scorm_Initialize_Complete' when it finishes communicating with the LMS.","OK");
		} else {
			EditorUtility.DisplayDialog("SCORM Manager is already present","You only need one SCORM Manager game object in your simulation. Remember to place objects that need messages from the ScormManager under it in the scene heirarchy.","OK");
		}

    }

	/// <summary>
	/// The menu item to display a short about message
	/// </summary>
    [MenuItem("SCORM/About SCORM Integration",false,0)]
    static void About() {
		EditorUtility.DisplayDialog("Unity-SCORM Integration Kit","This software enables the integration between web deployed Unity3D applications and a Learning Managment System (LMS) using the Sharable Content Object Reference Model (SCORM) developed at the US Department of Defence Advance Distributed Learning (ADL) Inititive. This software is provided 'as-is' and is available free of charge at http://www.adlnet.gov. This software may be used under the provisions of the Apache 2.0 license. This project is derived from the Unity-SCORM Integration Toolkit Version 1.0 Beta project from the ADL (Advance Distributed Learning) [http://www.adlnet.gov]. Source code is available from unity3d.stals.com.au/scorm-integration-kit  ","OK");
    }

	/// <summary>
	/// The menu item to open a browser window to the support page for this package
	/// </summary>
    [MenuItem("SCORM/Help",false,0)]
    static void Help() {
		Application.OpenURL("http://unity3d.stals.com.au/scorm-integration-kit");
    }


	/// <summary>
	/// Copies the files recursively.
	/// </summary>
	/// <param name="source">Source.</param>
	/// <param name="target">Target.</param>
	public static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target) {
	    foreach (DirectoryInfo dir in source.GetDirectories())
	        CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
	    foreach (FileInfo file in source.GetFiles()) {
			// Skip hidden files and meta files
			if (!file.Name.StartsWith(".") && !file.Name.EndsWith(".meta")) {
				file.CopyTo (Path.Combine (target.FullName, file.Name));
			}
		}
	}

	/// <summary>
	/// Publish the WebGL build in "Folder Location" as a SCORM 2004 zip file.
	/// </summary>
	void Publish() {
		ScormPackageSettings settings = ScormPackageSettings.FromPlayerPrefs();
		string buildDir = PlayerPrefs.GetString("Course_Export", ScormBuildCli.DefaultBuildDir);
		if (!File.Exists(Path.Combine(buildDir, "index.html"))) {
			EditorUtility.DisplayDialog("WebGL build not found", "There is no WebGL build (index.html) in:\n" + buildDir + "\n\nUse 'Build WebGL + Publish', or choose the folder of a WebGL build made with the SCORM template.", "OK");
			return;
		}
		List<string> errors = settings.Validate();
		if (errors.Count > 0) {
			EditorUtility.DisplayDialog("Invalid SCORM properties", string.Join("\n", errors.ToArray()), "OK");
			return;
		}
		string zipfile = EditorUtility.SaveFilePanel("Choose Output File", Path.GetDirectoryName(Path.GetFullPath(buildDir)), settings.courseTitle, "zip");
		if (zipfile == "")
			return;
		try {
			ScormPackageResult result = ScormPackager.Package(buildDir, zipfile, settings);
			foreach (string w in result.warnings)
				Debug.LogWarning("[SCORM] " + w);
			string warnings = result.warnings.Count > 0 ? "\n\nWarnings:\n" + string.Join("\n", result.warnings.ToArray()) : "";
			EditorUtility.DisplayDialog("SCORM Package Published", "The SCORM Package has been published to " + result.zipPath + warnings, "OK");
		} catch (Exception e) {
			Debug.LogError("[SCORM] " + e);
			EditorUtility.DisplayDialog("SCORM Package not published", e.Message, "OK");
		}
	}

	/// <summary>
	/// Build the enabled scenes for WebGL with the SCORM template into "Folder Location", then publish.
	/// </summary>
	void BuildAndPublish() {
		string buildDir = PlayerPrefs.GetString("Course_Export");
		if (string.IsNullOrEmpty(buildDir)) {
			buildDir = ScormBuildCli.DefaultBuildDir;
			PlayerPrefs.SetString("Course_Export", buildDir);
		}
		bool built = false;
		try {
			built = ScormBuildCli.BuildWebGLPlayer(buildDir, "gzip-fallback");
		} catch (Exception e) {
			Debug.LogError("[SCORM] " + e);
		}
		if (!built) {
			EditorUtility.DisplayDialog("WebGL build failed", "See the Console for details.", "OK");
			return;
		}
		Publish();
	}

	/// <summary>
	/// Display the Export SCORM dialog
	/// </summary>
    void OnGUI() {
		EditorStyles.miniLabel.wordWrap = true;
		EditorStyles.foldout.fontStyle = FontStyle.Bold;

		// Foldout 1 - the WebGL build location.
		GUILayout.BeginHorizontal();
		foldout1 = EditorGUILayout.Foldout(foldout1,"WebGL Build Location", EditorStyles.foldout);

		bool help1 = GUILayout.Button(new GUIContent ("Help", "Help for the WebGL Build Location section"),EditorStyles.miniBoldLabel);
		if(help1)
			EditorUtility.DisplayDialog("Help","'Build WebGL + Publish' builds the enabled scenes for WebGL with the SCORM template (PROJECT:SCORM, Gzip + decompression fallback) into this folder and then publishes it. 'Publish' packages a WebGL build that already exists in this folder; it must have been built with the SCORM WebGL template, or the JavaScript that connects to the LMS will be missing.","OK");

		GUILayout.EndHorizontal();

		if(foldout1) {
			GUILayout.BeginVertical("TextArea");
			GUILayout.Label("Folder of the WebGL build (relative to the project or absolute). Default: " + ScormBuildCli.DefaultBuildDir, EditorStyles.miniLabel);
			PlayerPrefs.SetString("Course_Export", EditorGUILayout.TextField("Folder Location", PlayerPrefs.GetString("Course_Export", ScormBuildCli.DefaultBuildDir)));

			GUI.skin.button.fontSize = 8;
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			bool ChooseDir = GUILayout.Button(new GUIContent("Choose Folder","Select the folder containing the WebGL build"),GUILayout.ExpandWidth(false));
			GUILayout.EndHorizontal();
			if(ChooseDir)
			{
				string export_dir = EditorUtility.OpenFolderPanel("Choose WebGL build",PlayerPrefs.GetString("Course_Export"),"WebGL");
				if(export_dir != "")
					PlayerPrefs.SetString("Course_Export",export_dir);
			}

	        GUILayout.EndVertical();
		}


		// Foldout 2 - Set the SCORM properties (many of the options available in the imsmanifest.xml file)
		GUILayout.BeginHorizontal();
		foldout2 = EditorGUILayout.Foldout(foldout2,"SCORM Properties", EditorStyles.foldout);

		bool help2 = GUILayout.Button(new GUIContent ("Help", "Help for the SCORM Properties section"),EditorStyles.miniBoldLabel);
		if(help2)
			EditorUtility.DisplayDialog("Help","The properties will control how the LMS controls and displays your SCORM content. These values will be written into the imsmanifest.xml file within the exported zip package. There are many other settings that can be specified in the manifest - for more information read the Content Aggregation Model documents at http://www.adlnet.gov/capabilities/scorm","OK");

		GUILayout.EndHorizontal();

		if(foldout2) {
			GUILayout.BeginVertical("TextArea");
			GUILayout.Label("Information about your SCORM package including the title and various configuration values.", EditorStyles.miniLabel);

			int edition = EditorGUILayout.Popup(new GUIContent("Edition:", "SCORM 2004 edition written to the manifest. 3rd Edition is the most widely supported (e.g. Moodle)."), PlayerPrefs.GetInt(ScormPackageSettings.PrefEdition, 0), new GUIContent[] { new GUIContent("SCORM 2004 3rd Edition"), new GUIContent("SCORM 2004 4th Edition") });
			PlayerPrefs.SetInt(ScormPackageSettings.PrefEdition, edition);

			PlayerPrefs.SetString("Manifest_Identifier", EditorGUILayout.TextField(new GUIContent("Identifier:","The unique IMS Manifest Identifier (e.g. au.com.stals.myapp)"), PlayerPrefs.GetString("Manifest_Identifier")));
			PlayerPrefs.SetString("Course_Title", EditorGUILayout.TextField(new GUIContent("Title:","The title of the SCORM content, as you want it to be displayed in the learning management system (LMS)"), PlayerPrefs.GetString("Course_Title")));
			PlayerPrefs.SetString("Course_Description", EditorGUILayout.TextField(new GUIContent("Description:","Description of the SCORM content."), PlayerPrefs.GetString("Course_Description")));
			PlayerPrefs.SetString("SCO_Title", EditorGUILayout.TextField(new GUIContent("Module Title:","The title of the Unity content.  Note, this title may show as the first item in an LMS-provided table of contents."), PlayerPrefs.GetString("SCO_Title")));
			PlayerPrefs.SetString("Data_From_Lms", EditorGUILayout.TextField(new GUIContent("Launch Data:","User-defined string value that can be used as initial learning experience state data."), PlayerPrefs.GetString("Data_From_Lms")));

			foreach (string editionWarning in ScormManifestBuilder.GetEditionWarnings(ScormPackageSettings.FromPlayerPrefs()))
				EditorGUILayout.HelpBox(editionWarning, MessageType.Warning);

			if (edition == 0) {
				PlayerPrefs.SetString(ScormPackageSettings.PrefCompletionThreshold, EditorGUILayout.TextField(new GUIContent("Completion Threshold:","Optional (0..1, e.g. 0.8). Progress measure at which the LMS considers the SCO completed. Empty = not written."), PlayerPrefs.GetString(ScormPackageSettings.PrefCompletionThreshold)));
			} else {
				bool progress = GUILayout.Toggle(PlayerPrefs.GetInt("completedByMeasure") != 0,new GUIContent("Completed By Measure","If true, then this activity's completion status will be determined by the progress measure's relation to the minimum progress measure. This derived completion status will override what it explicitly set."));
				PlayerPrefs.SetInt("completedByMeasure",progress ? 1 : 0);
				if(progress)
				{
					GUILayout.Label(new GUIContent("Minimum Progress Measure: " + ScormFormat.ToReal(PlayerPrefs.GetFloat("minProgressMeasure", 1f)),"Defines a minimum completion percentage for this activity for use in conjunction with completed by measure.") , EditorStyles.miniLabel);
					PlayerPrefs.SetFloat("minProgressMeasure",(float)System.Math.Round(GUILayout.HorizontalSlider(PlayerPrefs.GetFloat("minProgressMeasure", 1f),0.0f,1.0f)*100.0f)/100.0f);
				}
				GUILayout.Label("If set, this indicates that this activity’s completion status will be determined soley by the relation of the progress measure to Minimum Progress Measure.", EditorStyles.miniLabel);
			}

			GUILayout.Label("Select the Time Limit Action to be passed to the SCO", EditorStyles.largeLabel);
			PlayerPrefs.SetInt("Time_Limit_Action",EditorGUILayout.Popup(PlayerPrefs.GetInt("Time_Limit_Action"),new string[]{"Not Set","exit,message","exit,no message","continue,message","continue,no message"},GUILayout.ExpandWidth(false)));
			PlayerPrefs.SetString("Time_Limit_Secs", EditorGUILayout.TextField(new GUIContent("Time Limit (secs):","The time limit for this SCO in seconds (empty or 0 = no limit)."), PlayerPrefs.GetString("Time_Limit_Secs")));

			GUILayout.EndVertical();
		}

		// Publish Buttons
		GUI.skin.button.fontSize = 12;
		// Builds and file dialogs must not run inside OnGUI (they break the IMGUI layout): run them on the next editor
		// update and leave the current GUI pass. ExitGUI works by throwing, so it must stay outside any try/catch.
		if(GUILayout.Button(new GUIContent ("Publish", "Package the WebGL build in 'Folder Location' as a SCORM zip."))) {
			EditorApplication.delayCall += Publish;
			GUIUtility.ExitGUI();
		}
		if(GUILayout.Button(new GUIContent ("Build WebGL + Publish", "Build the enabled scenes for WebGL with the SCORM template into 'Folder Location', then package it."))) {
			EditorApplication.delayCall += BuildAndPublish;
			GUIUtility.ExitGUI();
		}
    }
}
