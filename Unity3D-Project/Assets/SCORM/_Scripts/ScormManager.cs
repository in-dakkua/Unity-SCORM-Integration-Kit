/***********************************************************************************************************************
 * Unity-SCORM Integration Kit
 * 
 * Unity-SCORM Integration Manager
 * 
 * Copyright (C) 2015, Richard Stals (http://stals.com.au)
 * This Version removes multi-threading which is not supported by WebGL Player
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
using System;
using System.Collections.Generic;

using System.Runtime.InteropServices;

/// <summary>
/// The main interface between your Unity3D code and the SCORM API (via the ScormManager and the ScormAPIWrapper).
/// </summary>
public class ScormManager : MonoBehaviour
{

	/// <summary>Debug output: browser console in WebGL, Unity console elsewhere.</summary>
	private static void wgldebugPrint(string str) {
		ScormAPIWrapper.DebugPrint(str);
	}

/// <summary>The reference to the ScormAPIWrapper object (the bridge between Unity's C# and scorm.js</summary>
static ScormAPIWrapper scormAPIWrapper;

	/// <summary>The name of this object, used for callbacks from the ScormAPIWrapper</summary>
	static string objectName;

	/// <summary>Has the javascript communication layer to the LMS been initialised?</summary>
	static bool initialized = false;
	
	/// <summary>The student record data returned from the LMS via SCORM</summary>
	static StudentRecord studentRecord;


	/// <summary>
	/// Begin the ScormManager. Wait for "Scorm_Initialize_Complete" after Start();
	/// </summary>
	/// <remarks>
	/// Triggered by the Unity Engine, this function begins reading in all the data from the LMS. Launches
	/// Initialize(), that will fire "Scorm_Initialize_Complete" when ready.
	/// </remarks> 
	void Start() {
		objectName = this.gameObject.name;
		Initialize();
	}

	/// <summary>
	/// Stop the Scorm Manager. Will commit data.
	/// </summary>
	/// <remarks>
	/// Triggered by the Unity Engine, this will commit data back to the LMS. 
	/// DO NOT RELY on this - the shutdown of Unity may break the message loop and cause timeouts in the 
	/// serilization thread. Commit your data manually and wait for the "Scorm_Commit_Complete" message
	/// before shutting down the engine.
	/// </remarks> 
	void Stop() {
		Commit();
	}

	/// <summary>
	/// Read the student data in from the LMS
	/// </summary>
	/// <remarks>
	/// Calls the javascript layer to read in all the data. 
	/// Will fire "Scorm_Initialize_Complete" when the StudentRecord datamodel is ready to be manipulated
	/// </remarks> 
	public static void Initialize() {
		lmsValues.Clear();
		scormAPIWrapper = new ScormAPIWrapper(objectName,"ScormValueCallback");
		scormAPIWrapper.Initialize();
		IsLmsConnected = scormAPIWrapper.IsApiFound && scormAPIWrapper.IsInitialized;
		try {
			if(scormAPIWrapper.IsScorm2004)
			{
				//Load StudentRecord data using SCORM 2004
				studentRecord = LoadStudentRecord();
			} else {
				//Load StudentRecord data using SCORM 1.2
				throw new System.InvalidOperationException("SCORM 1.2 not currently supported");  //TODO: Not currently supported.
			}
		} catch(Exception e) {
            wgldebugPrint("***Initialize***" + e.Message + "<br/>" + e.StackTrace + "<br/>" + e.Source);
		}
		initialized = true;
		GameObject manager = FindManagerObject();
		if (manager != null)
			manager.BroadcastMessage("Scorm_Initialize_Complete",SendMessageOptions.DontRequireReceiver);
	}

	/// <summary>True when an LMS API was found and accepted Initialize (always true with the Editor backend).</summary>
	public static bool IsLmsConnected { get; private set; }

	/// <summary>True once Initialize() has run and the StudentRecord has been loaded.</summary>
	public static bool IsInitialized { get { return initialized; } }

	/// <summary>
	/// Raised after every SCORM API call (Initialize, GetValue, SetValue, Commit, Terminate) with its result and the LMS
	/// error code. Same event as <see cref="ScormAPIWrapper.CallCompleted"/>; use it to show an API log in the UI.
	/// </summary>
	/// <remarks>
	/// Static event: subscribe in OnEnable and unsubscribe in OnDisable, otherwise a destroyed MonoBehaviour keeps
	/// receiving calls. Handlers should not call the SCORM API; if they do, those nested calls work but are not
	/// reported again (no recursion).
	/// </remarks>
	public static event Action<ScormCallInfo> ScormCall {
		add { ScormAPIWrapper.CallCompleted += value; }
		remove { ScormAPIWrapper.CallCompleted -= value; }
	}

	/// <summary>
	/// Last value of each data model element read from the LMS (error 0, not empty) or accepted by it in this session.
	/// Used to skip redundant writes; StudentRecord cannot be used for that because its floats cannot tell "not set"
	/// from 0 and callers (e.g. AnObjective) mutate the StudentRecord objects before calling Update*.
	/// </summary>
	static readonly Dictionary<string, string> lmsValues = new Dictionary<string, string>();

	/// <summary>GetValue that remembers the value for <see cref="WriteIfChanged"/>.</summary>
	static string LoadValue(string identifier) {
		string value = scormAPIWrapper.GetValue(identifier);
		if (scormAPIWrapper.IsApiFound && scormAPIWrapper.LastErrorCode == 0 && !string.IsNullOrEmpty(value))
			lmsValues[identifier] = value;
		return value;
	}

	/// <summary>
	/// True if the LMS rejected at least one write during the last UpsertObjective, RecordInteraction, UpdateScore,
	/// UpdateStatus or UpdateProgressMeasure call (rejected values are not cached, so the next call retries them).
	/// </summary>
	public static bool LastWriteFailed { get { return LastWriteFailures > 0; } }

	/// <summary>Number of writes the LMS rejected during the last call of the scenario API (see LastWriteFailed).</summary>
	public static int LastWriteFailures { get; private set; }

	/// <summary>SCORM error code of the first write rejected during that call (0 = none, or no LMS API).</summary>
	public static int LastWriteErrorCode { get; private set; }

	static void BeginWrites() {
		LastWriteFailures = 0;
		LastWriteErrorCode = 0;
	}

	/// <summary>SetValue that remembers the value when the LMS accepts it, and counts rejections.</summary>
	static bool SetAndCache(string identifier, string value) {
		value = value ?? "";
		bool ok = scormAPIWrapper.SetValue(identifier, value);
		if (ok) {
			lmsValues[identifier] = value;
		} else {
			if (LastWriteFailures == 0)
				LastWriteErrorCode = scormAPIWrapper.LastErrorCode;
			LastWriteFailures++;
		}
		return ok;
	}

	/// <summary>
	/// Writes value unless it is null/empty (never sent: invalid for vocabularies, scores and ids) or equal to the
	/// value the LMS already has (reals are compared numerically: "0.50" == "0.5").
	/// </summary>
	/// <returns>True if written or skipped; false only if the LMS rejected the write.</returns>
	static bool WriteIfChanged(string identifier, string value, bool isReal) {
		if (string.IsNullOrEmpty(value))
			return true;
		string known;
		if (lmsValues.TryGetValue(identifier, out known)) {
			if (known == value)
				return true;
			float knownReal, newReal;
			if (isReal && TryParseReal(known, out knownReal) && TryParseReal(value, out newReal) && Math.Abs(knownReal - newReal) < 1e-7f)
				return true;
		}
		return SetAndCache(identifier, value);
	}

	static bool HasKnownValue(string identifier) {
		string known;
		return lmsValues.TryGetValue(identifier, out known) && known.Length > 0;
	}

	static float? KnownReal(string identifier) {
		string known;
		float value;
		if (lmsValues.TryGetValue(identifier, out known) && TryParseReal(known, out value))
			return value;
		return null;
	}

	static bool TryParseReal(string str, out float value) {
		return float.TryParse(str, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
	}

	static GameObject FindManagerObject() {
		if (string.IsNullOrEmpty(objectName))
			return null;
		return GameObject.Find(objectName);
	}



	/// <summary>
	/// Used for the Javascript layer to communicate back to the Unity layer.
	/// </summary>
	/// <param name="value" direction="input">
	/// A string in the format "value|number" where the number is the identifier of the Set or Get operation
	/// issued by the ScormAPIWrapper bridge, and the value is the result of that API call
	/// </param>
	/// <remarks>
	/// The javascript layer uses the name of the ScormManager object and the name of this function to return
	/// the results of an operation on the javascript API to the ScormAPIWrapper bridge. The number in the input string
	/// is used by the ScormAPIWrapper bridge to figure out what API call this message answers. This complexity is due to the 
	/// asyncronous nature of the UNITY/Javascript interface.
	/// </remarks> 
	[Obsolete("The SCORM bridge is synchronous now; ScormValueCallback does nothing.")]
	public void ScormValueCallback(string value) {
	}

	/// <summary>
	/// Call the final Commit.  It is the responsibility of the users of this class to write data to the LMS as you go (i.e. call SetValue())
	/// </summary>
	/// <remarks>
	/// Calls Commit on scorm.js. will fire "Scorm_Commit_Complete"
	/// when operation is complete.
	/// </remarks> 
	public static void Commit() 	{
		try {
			scormAPIWrapper.Commit();
		} catch(System.Exception e) {
            wgldebugPrint("***CallFinalCommit***" + e.Message + "<br/>" + e.StackTrace + "<br/>" + e.Source);
        }
		GameObject manager = FindManagerObject();
		if (manager != null)
			manager.BroadcastMessage("Scorm_Commit_Complete", SendMessageOptions.DontRequireReceiver);
	}


	/// <summary>
	/// Sets the value.
	/// </summary>
	/// <remarks>
	/// Calls SetValue on scorm.js.
	/// </remarks> 
	/// <param name="identifier">The dot notation identifier of the data model element to set.</param>
	/// <param name="value">The string of the value to set.</param>
	private static void SetValue(string identifier, string value) {
		try {
			SetAndCache(identifier,value);
		} catch(System.Exception e) {
            wgldebugPrint("***ERROR***CallSetValue***" + e.Message + "<br/>" + e.StackTrace + "<br/>" + e.Source);
        }
	}

	/// <summary>
	/// Close the course. Be sure to call Commit first if you want to save your data.
	/// </summary>
	/// <remarks>
	/// Will cause the browser to close the window, and signal to the LMS that the course is over.
	/// Be sure to save your data to the LMS by calling Commit first, then waiting for Scorm_Commit_Complete.
	/// </remarks> 
	public static void Terminate() {
		try {
			scormAPIWrapper.Terminate();
		} catch(System.Exception e) {
            wgldebugPrint("***Terminate***" + e.Message + "<br/>" + e.StackTrace + "<br/>" + e.Source);
        }
	}

	/// <summary>
	/// Log a message.
	/// </summary>
	/// <param name="text" direction="input">
	/// The string value to log.
	/// </param>
	/// <remarks>
	/// Simply sends the log data down to child objects, which should handle the logging tasks.
	/// </remarks> 
	[UnityEngine.Scripting.Preserve]	// called by name through BroadcastMessage/SendMessage
	public void LogMessage(object text) {
		DoLogMessage(text);
	}

	/// <summary>
	/// For local logging and debugging from this class
	/// </summary>
	/// <param name="text">Text.</param>
	public static void DoLogMessage(object text) {
		GameObject manager = FindManagerObject();
		if (manager != null)
			manager.BroadcastMessage("Log",text,UnityEngine.SendMessageOptions.DontRequireReceiver);
	}
	
	/// <summary>
	/// Gets the completion status.
	/// </summary>
	/// <remarks>
	/// cmi.completion_status (“completed”, “incomplete”, “not attempted”, “unknown”, RW) Indicates whether the learner has completed the SCO
	/// </remarks>
	/// <returns>The completion status (StudentRecord.CompletionStatusType).</returns>
	public static StudentRecord.CompletionStatusType GetCompletionStatus() {
		return studentRecord.completionStatus;
	}

	/// <summary>
	/// Sets the completion status.
	/// </summary>
	/// <remarks>
	/// cmi.completion_status (“completed”, “incomplete”, “not attempted”, “unknown”, RW) Indicates whether the learner has completed the SCO
	/// </remarks>
	/// <param name="value">StudentRecord.CompletionStatusType object.</param>
	public static void SetCompletionStatus(StudentRecord.CompletionStatusType value) {
		string identifier = "cmi.completion_status";
		string strValue = CustomTypeToString(value);
		if (strValue.Length == 0)																//not_set: "" is not a valid vocabulary value
			return;
		studentRecord.completionStatus = value;
		SetValue (identifier, strValue);

	}

	/// <summary>
	/// Gets the version.
	/// </summary>
	/// <remarks>cmi._version (characterstring, RO) Represents the version of the data model</remarks>
	/// <returns>String representation of the SCORM API version.</returns>
	public static string GetVersion() {
		return studentRecord.version;
	}

	/// <summary>
	/// Gets the comments from learner.
	/// </summary>
	/// <remarks>The collection of comments made by the learner.</remarks>
	/// <returns>The comments from learner (List<StudentRecord.CommentsFromLearner>).</returns>
	public static List<StudentRecord.CommentsFromLearner> GetCommentsFromLearner() {
		return studentRecord.commentsFromLearner;
	}

	public static void AddCommentFromLearnerByFields(string commentText, string location) {
		StudentRecord.CommentsFromLearner comment = new StudentRecord.CommentsFromLearner ();
		comment.timeStamp = DateTime.Now;
		comment.comment = commentText;
		comment.location = location;
		AddCommentFromLearner (comment);
	}

	/// <summary>
	/// Gets the comments from LMS.
	/// </summary>
	/// <remarks>The collection of comments made by the LMS.</remarks>
	/// <returns>The comments from LMS (List<StudentRecord.CommentsFromLMS>).</returns>
	public static List<StudentRecord.CommentsFromLMS> GetCommentsFromLMS() {
		return studentRecord.commentsFromLMS;
	}

	/// <summary>
	/// Adds the comment from learner.
	/// </summary>
	/// <param name="comment">StudentRecord.CommentsFromLearner object.</param>
	/// <c>
	/// Usage:\n
	/// StudentRecord.CommentsFromLearner comment = new StudentRecord.CommentsFromLearner ();\n
	/// comment.comment = "The comment";\n
	/// comment.location = "The location (bookmark) in the SCO";\n
	/// ScormManager.AddCommentFromLearner(comment);
	/// </c>
	public static void AddCommentFromLearner(StudentRecord.CommentsFromLearner comment) {
		try {
			string identifier;
			string strValue;

			comment.timeStamp = DateTime.Now;														//Set timestamp to Now
			//All other properties must be set by the caller

			//Set the Comment Values
			int i = studentRecord.commentsFromLearner.Count;

			studentRecord.commentsFromLearner.Add(comment);

			identifier = "cmi.comments_from_learner."+i+".comment";
			strValue = comment.comment;
			SetAndCache(identifier,strValue);

			identifier = "cmi.comments_from_learner."+i+".location";
			strValue = comment.location;
			SetAndCache(identifier,strValue);

			identifier = "cmi.comments_from_learner."+i+".timestamp";
			strValue = ScormFormat.ToTimestamp(comment.timeStamp);
			SetAndCache(identifier,strValue);



		} catch(System.Exception e) {
            wgldebugPrint("***AddCommentFromLearner***" + e.Message + "<br/>" + e.StackTrace + "<br/>" + e.Source);
        }
	}

	/// <summary>
	/// Updates the comment from learner.
	/// </summary>
	/// <param name="index">Index of the comment to update.</param>
	/// <param name="comment">StudentRecord.CommentsFromLearner object.</param>
	/// <c>
	/// Usage:\n
	/// int index = 1;\n
	/// StudentRecord.CommentsFromLearner comment = new StudentRecord.CommentsFromLearner ();\n
	/// comment.comment = "The comment";\n
	/// comment.location = "The location (bookmark) in the SCO";\n
	/// ScormManager.UpdateCommentFromLearner(index, comment);
	/// </c>
	public static void UpdateCommentFromLearner(int index, StudentRecord.CommentsFromLearner comment) {
		try {
			string identifier;
			string strValue;

			comment.timeStamp = DateTime.Now;														//Set timestamp to Now
			//All other properties must be set by the caller

			//Set the Comment Values
			identifier = "cmi.comments_from_learner."+index+".comment";
			strValue = comment.comment;
			SetAndCache(identifier,strValue);

			identifier = "cmi.comments_from_learner."+index+".location";
			strValue = comment.location;
			SetAndCache(identifier,strValue);

			identifier = "cmi.comments_from_learner."+index+".timestamp";
			strValue = ScormFormat.ToTimestamp(comment.timeStamp);
			SetAndCache(identifier,strValue);

			studentRecord.commentsFromLearner[index] = comment;
		} catch(System.Exception e) {
            wgldebugPrint("***UpdateCommentFromLearner***" + e.Message + "<br/>" + e.StackTrace + "<br/>" + e.Source);
        }
	}

	/// <summary>
	/// Gets the completion thershold.
	/// </summary>
	/// <remarks>cmi.completion_threshold (real(10,7) range (0..1), RO) Used to determine whether the SCO should be considered complete</remarks>
	/// <returns>The completion thershold.</returns>
	public static float GetCompletionThreshold() {
		return studentRecord.completionThreshold;
	}

	/// <summary>
	/// Gets the credit.
	/// </summary>
	/// <remarks>cmi.credit (“credit”, “no-credit”, RO) Indicates whether the learner will be credited for performance in the SCO</remarks>
	/// <returns>The StudentRecord.CreditType value.</returns>
	/// <c>
	/// Usage:\n
	/// StudentRecord.CreditType credit = ScormManager.GetCredit ();  or\n
	/// string credit = ScormManager.CustomTypeToString (ScormManager.GetCredit ());
	/// </c>
	public static StudentRecord.CreditType GetCredit() {
		return studentRecord.credit;
	}

	/// <summary>
	/// Gets the entry.
	/// </summary>
	/// <remarks>cmi.entry (ab_initio, resume, “”, RO) Asserts whether the learner has previously accessed the SCO</remarks>
	/// <returns>The StudentRecord.EntryType value.</returns>
	/// <c>
	/// Usage:\n
	/// StudentRecord.EntryType entry = ScormManager.GetEntry ();  or\n
	/// string entry = ScormManager.CustomTypeToString (ScormManager.GetEntry ());
	/// </c>
	public static StudentRecord.EntryType GetEntry() {
		return studentRecord.entry;
	}

	/// <summary>
	/// Sets the exit.
	/// </summary>
	/// <param name="value">The StudentRecord.ExitType value.</param>
	/// <c>
	/// Usage:\n
	/// ScormManager.SetExit(StudentRecord.ExitType.suspend);
	/// </c>
	public static void SetExit(StudentRecord.ExitType value) {
		string identifier = "cmi.exit";
		string strValue = CustomTypeToString(value);
		if (strValue.Length == 0)
			return;
		SetValue (identifier, strValue);
	}

	/// <summary>
	/// Gets the interactions.
	/// </summary>
	/// <returns>The interactions (StudentRecord.LearnerInteractionRecord).</returns>
	/// <c>
	/// Usage:\n
	/// List<StudentRecord.LearnerInteractionRecord> interactions = ScormManager.GetInteractions ();\n
	/// for (int i=0; i < interactions.Count; i++) {\n
	/// 	string id = interactions[i].id;\n
	/// 	string type = CustomTypeToString (interactions[i].type);\n
	/// 	string timeStamp = String.Format("{0:s}", interactions[i].timeStamp);\n
	/// 	string weighting = interactions[i].weighting.ToString();\n
	/// 	string learnerResponse = interactions[i].response;\n
	/// 	string result = CustomTypeToString (interactions[i].result);\n
	/// 	string estimate = interaction.estimate.ToString();\n
	/// 	string latency = secondsToTimeInterval(interactions[i].latency);\n
	/// 	string description = interactions[i].description;\n
	/// 	\n
	/// 	int objectivesCount = interactions[i].objectives.Count;\n
	/// 	if(objectivesCount != 0) {\n
	/// 		for (int x = 0; x < objectivesCount; i++) {\n
	/// 			string objectiveId = interactions[i].objectives[x];\n
	/// 			//Do something with the objective id here\n
	/// 		}\n
	/// 	}\n
	/// 	\n
	/// 	int correctResponsesCount = interactions[i].correctResponses.Count;\n
	/// 	if(correctResponsesCount != 0) {\n
	/// 		for (int x = 0; x < correctResponsesCount; i++) {\n
	/// 			string correctResponsePattern = interactions[i].correctResponses[x];\n
	/// 			//Do something with the correct response pattern here\n
	/// 		}\n
	/// 	}\n
	/// 	\n
	/// }\n
	/// </c>
	public static List<StudentRecord.LearnerInteractionRecord> GetInteractions() {
		return studentRecord.interactions;
	}

	/// <summary>
	/// Adds to the interactions.
	/// </summary>
	/// <param name="interaction">Interaction (StudentRecord.LearnerInteractionRecord).</param>
	/// <c>
	/// Usage:\n
	/// StudentRecord.LearnerInteractionRecord newRecord = new StudentRecord.LearnerInteractionRecord();\n
	/// newRecord.type = StudentRecord.InteractionType.other;\n
	/// newRecord.timeStamp = DateTime.Now;\n
	/// newRecord.weighting = 0.5f;\n
	/// newRecord.response = "true";\n
	/// newRecord.latency = 12f;\n
	/// newRecord.description = "Is this easy to use?";\n
	/// newRecord.result = StudentRecord.ResultType.correct;\n
	/// newRecord.estimate = 1f;	//Not used\n
	/// \n
	/// ScormManager.AddInteraction (newRecord);\n
	/// </c>
	public static void AddInteraction(StudentRecord.LearnerInteractionRecord interaction) {
		try {
			interaction.id = "urn:STALS:interaction-id-" + studentRecord.interactions.Count.ToString ();	//Override ID to ensure it is unique (use RecordInteraction to keep your own id)
			//All other properties must be set by the caller

			int i = studentRecord.interactions.Count;

			if (interaction.timeStamp.Year < 1970)											//SCORM time must be >= 1970; an unset DateTime would be rejected
				interaction.timeStamp = DateTime.Now;

			//Added locally only if the LMS accepted the id (keeps interactions.Count == cmi.interactions._count),
			//or when there is no LMS at all (the list then only feeds the UI).
			if (WriteInteraction(i, interaction, true) || !IsLmsConnected)
				studentRecord.interactions.Add(interaction);
		} catch(System.Exception e) {
            wgldebugPrint("***AddInteraction***" + e.Message + "<br/>" + e.StackTrace + "<br/>" + e.Source);
        }
	}

	/// <summary>
	/// Appends an interaction keeping its own id and links it to objectives by id.
	/// </summary>
	/// <remarks>
	/// cmi.interactions.n.id is written first (the id of the interaction if set, otherwise
	/// <see cref="GetNextInteractionId"/>), then type, cmi.interactions.n.objectives.m.id (interaction.objectives plus
	/// objectiveIds; empty and repeated ids are skipped), timestamp (now if unset), correct_responses, weighting,
	/// learner_response, result, latency (only if &gt; 0) and description. Empty values and not_set vocabularies are
	/// never sent; learner_response and correct_responses are skipped while the type is not_set (the LMS would answer
	/// 408). Interactions are a journal: a repeated interaction id creates a new entry, it does not update the old one.
	/// </remarks>
	/// <returns>The index n of the new interaction, or -1 if the LMS rejected its id (the interaction is not added).</returns>
	/// <param name="interaction">Interaction to record (its objectives list is extended with objectiveIds).</param>
	/// <param name="objectiveIds">Ids of the objectives (e.g. "scenario-01") this interaction contributes to.</param>
	/// <exception cref="InvalidOperationException">ScormManager is not initialized.</exception>
	/// <exception cref="ArgumentNullException">interaction is null.</exception>
	public static int RecordInteraction(StudentRecord.LearnerInteractionRecord interaction, params string[] objectiveIds) {
		bool allWritten;
		return RecordInteraction(interaction, out allWritten, objectiveIds);
	}

	/// <summary>
	/// Same as <see cref="RecordInteraction(StudentRecord.LearnerInteractionRecord, string[])"/>; allWritten is false if
	/// the LMS rejected any field (see also LastWriteFailed / LastWriteErrorCode).
	/// </summary>
	/// <exception cref="ArgumentException">An id (interaction or objective) contains whitespace or is longer than 4000 characters.</exception>
	public static int RecordInteraction(StudentRecord.LearnerInteractionRecord interaction, out bool allWritten, params string[] objectiveIds) {
		RequireStudentRecord();
		BeginWrites();
		allWritten = false;
		if (interaction == null)
			throw new ArgumentNullException("interaction");
		if (!string.IsNullOrEmpty(interaction.id))
			ValidateIdentifier(interaction.id, "interaction id");
		if (objectiveIds != null)
			foreach (string objectiveId in objectiveIds)
				if (!string.IsNullOrEmpty(objectiveId))
					ValidateIdentifier(objectiveId, "objective id");
		if (interaction.objectives != null)
			foreach (StudentRecord.LearnerInteractionObjective link in interaction.objectives)
				if (link != null && !string.IsNullOrEmpty(link.id))
					ValidateIdentifier(link.id, "objective id");

		if (string.IsNullOrEmpty(interaction.id))
			interaction.id = GetNextInteractionId();
		if (interaction.objectives == null)
			interaction.objectives = new List<StudentRecord.LearnerInteractionObjective>();
		if (objectiveIds != null) {
			foreach (string objectiveId in objectiveIds) {
				if (string.IsNullOrEmpty(objectiveId) || interaction.objectives.Exists(o => o != null && o.id == objectiveId))
					continue;
				StudentRecord.LearnerInteractionObjective link = new StudentRecord.LearnerInteractionObjective();
				link.id = objectiveId;
				interaction.objectives.Add(link);
			}
		}
		if (interaction.timeStamp.Year < 1970)
			interaction.timeStamp = DateTime.Now;

		int index = studentRecord.interactions.Count;
		if (!WriteInteraction(index, interaction, true))
			return -1;
		studentRecord.interactions.Add(interaction);
		allWritten = !LastWriteFailed;
		return index;
	}

	public static string GetNextInteractionId() {
		return "urn:STALS:interaction-id-" + studentRecord.interactions.Count.ToString ();
	}

	/// <summary>
	/// Updates the interaction.
	/// </summary>
	/// <param name="index">Index.</param>
	/// <param name="interaction">Interaction (StudentRecord.LearnerInteractionRecord).</param>
	/// <c>
	/// Usage:\n
	/// int index = 0;
	/// StudentRecord.LearnerInteractionRecord newRecord = new StudentRecord.LearnerInteractionRecord();\n
	/// newRecord.type = StudentRecord.InteractionType.other;\n
	/// newRecord.timeStamp = DateTime.Now;\n
	/// newRecord.weighting = 0.5f;\n
	/// newRecord.response = "true";\n
	/// newRecord.latency = 12f;\n
	/// newRecord.description = "Is this easy to use?";\n
	/// newRecord.result = StudentRecord.ResultType.correct;\n
	/// newRecord.estimate = 1f;	//Not used\n
	/// \n
	/// ScormManager.UpdateInteraction (index, newRecord);\n
	/// </c>
	public static void UpdateInteraction(int index, StudentRecord.LearnerInteractionRecord interaction) {
		try {
			interaction.timeStamp = DateTime.Now;														//Set timestamp to Now
			//All other properties must be set by the caller

			WriteInteraction(index, interaction, false);
			studentRecord.interactions[index] = interaction;
		} catch(System.Exception e) {
            wgldebugPrint("***UpdateInteraction***" + e.Message + "<br/>" + e.StackTrace + "<br/>" + e.Source);
        }
	}

	/// <summary>
	/// Writes cmi.interactions.index: id first, type before learner_response/correct_responses (408 otherwise),
	/// skipping empty values and values the LMS already has.
	/// </summary>
	/// <returns>False only if the id of a new interaction was rejected (nothing else is written then).</returns>
	static bool WriteInteraction(int index, StudentRecord.LearnerInteractionRecord interaction, bool isNew) {
		string p = "cmi.interactions." + index + ".";

		if (isNew) {
			if (!SetAndCache(p + "id", interaction.id))
				return false;
		} else {
			WriteIfChanged(p + "id", interaction.id, false);
		}

		string type = CustomTypeToString(interaction.type);
		WriteIfChanged(p + "type", type, false);
		bool typeKnown = type.Length > 0 || HasKnownValue(p + "type");

		if (interaction.objectives != null) {
			int x = 0;
			HashSet<string> linked = new HashSet<string>(StringComparer.Ordinal);
			foreach (StudentRecord.LearnerInteractionObjective objective in interaction.objectives) {
				if (objective == null || string.IsNullOrEmpty(objective.id) || !linked.Add(objective.id))
					continue;	//a repeated id in cmi.interactions.n.objectives is rejected by the LMS (351)
				WriteIfChanged(p + "objectives." + x + ".id", objective.id, false);
				x++;
			}
		}

		if (interaction.timeStamp.Year >= 1970)
			WriteIfChanged(p + "timestamp", ScormFormat.ToTimestamp(interaction.timeStamp), false);

		if (typeKnown && interaction.correctResponses != null) {
			int x = 0;
			foreach (StudentRecord.LearnerInteractionCorrectResponse correctResponse in interaction.correctResponses) {
				if (correctResponse == null || string.IsNullOrEmpty(correctResponse.pattern))
					continue;
				WriteIfChanged(p + "correct_responses." + x + ".pattern", correctResponse.pattern, false);
				x++;
			}
		}

		WriteIfChanged(p + "weighting", ScormFormat.ToReal(interaction.weighting), true);

		if (typeKnown)
			WriteIfChanged(p + "learner_response", interaction.response, false);

		if (interaction.result == StudentRecord.ResultType.estimate)
			WriteIfChanged(p + "result", ScormFormat.ToReal(interaction.estimate), true);
		else
			WriteIfChanged(p + "result", CustomTypeToString(interaction.result), false);

		if (interaction.latency > 0f)
			WriteIfChanged(p + "latency", secondsToTimeInterval(interaction.latency), false);

		WriteIfChanged(p + "description", interaction.description, false);
		return true;
	}

	/// <summary>
	/// Gets the launch data.
	/// </summary>
	/// <returns>The launch data string.</returns>
	public static string GetLaunchData() {
		return studentRecord.launchData;
	}

	/// <summary>
	/// Gets the learner identifier.
	/// </summary>
	/// <returns>The learner identifier string.</returns>
	public static string GetLearnerId() {
		return studentRecord.learnerID;
	}

	/// <summary>
	/// Gets the name of the learner.
	/// </summary>
	/// <returns>The learner name string.</returns>
	public static string GetLearnerName() {
		return studentRecord.learnerName;
	}

	/// <summary>
	/// Gets the learner preference.
	/// </summary>
	/// <remarks>
	/// Contains:
	/// 1. cmi.learner_preference.audio_level <see cref="SetLearnerPreferenceAudioLevel"/>
	/// 2. cmi.learner_preference.language <see cref="GetLearnerPreferenceLanguage"/>
	/// 3. cmi.learner_preference.delivery_speed <see cref="GetLearnerPreferenceDeliverySpeed"/>
	/// 4. cmi.learner_preference.audio_captioning <see cref="GetLearnerPreferenceAudioCaptioning"/>
	/// </remarks>
	/// <returns>The learner preference StudentRecord.LearnerPreference.</returns>
	/// <c>
	/// Usage:\n
	/// StudentRecord.LearnerPreference learnerPreference = ScormManager.GetLearnerPreference;\n
	/// int audioCaptioning = learnerPreference.audioCaptioning;\n
	/// </c>
	public static StudentRecord.LearnerPreference GetLearnerPreference() {
		return studentRecord.learnerPreference;
	}

	/// <summary>
	/// Sets the learner preference.
	/// </summary>
	/// <param name="learnerPreference">Learner preference.</param>
	/// <c>
	/// Usage:\n
	/// StudentRecord.LearnerPreference learnerPreference = new StudentRecord.LearnerPreference();\n
	/// learnerPreference.audioLevel = 1.1;\n
	/// learnerPreference.deliverySpeed = 1f;\n
	/// learnerPreference.audioCaptioning = 0;\n
	/// learnerPreference.langauge = "";\n
	/// ScormManager.SetLearnerPreference(learnerPreference);
	/// </c>
	public static void SetLearnerPreference(StudentRecord.LearnerPreference learnerPreference) {
		string identifier = "cmi.learner_preference.audio_level";
		string strValue = ScormFormat.ToReal(learnerPreference.audioLevel);
		SetValue (identifier, strValue);

		identifier = "cmi.learner_preference.language";
		strValue = learnerPreference.langauge;
		SetValue (identifier, strValue);

		identifier = "cmi.learner_preference.delivery_speed";
		strValue = ScormFormat.ToReal(learnerPreference.deliverySpeed);
		SetValue (identifier, strValue);

		identifier = "cmi.learner_preference.audio_captioning";
		strValue = learnerPreference.audioCaptioning.ToString(System.Globalization.CultureInfo.InvariantCulture);
		SetValue (identifier, strValue);

		studentRecord.learnerPreference = learnerPreference;
	}

	/// <summary>
	/// Gets the learner preference audio level.
	/// </summary>
	/// <remarks>cmi.learner_preference.audio_level (real(10,7), range (0..*), RW) Specifies an intended change in perceived audio level</remarks>
	/// <returns>The learner preference audio level float.</returns>
	public static float GetLearnerPreferenceAudioLevel() {
		return studentRecord.learnerPreference.audioLevel;
	}

	/// <summary>
	/// Sets the learner preference audio level.
	/// </summary>
	/// <param name="value">float Value.</param>
	public static void SetLearnerPreferenceAudioLevel(float value) {
		string identifier = "cmi.learner_preference.audio_level";
		string strValue = ScormFormat.ToReal(value);
		studentRecord.learnerPreference.audioLevel = value;
		SetValue (identifier, strValue);
	}

	/// <summary>
	/// Gets the learner preference language.
	/// </summary>
	/// <remarks>cmi.learner_preference.language (language_type (SPM 250), RW) The learner’s preferred language for SCOs with multilingual capability</remarks>
	/// <returns>The learner preference language string.</returns>
	public static string GetLearnerPreferenceLanguage() {
		return studentRecord.learnerPreference.langauge;
	}

	/// <summary>
	/// Sets the learner preference language.
	/// </summary>
	/// <param name="value">string Value.</param>
	public static void SetLearnerPreferenceLanguage(string value) {
		string identifier = "cmi.learner_preference.language";
		string strValue = value;
		studentRecord.learnerPreference.langauge = value;
		SetValue (identifier, strValue);
	}

	/// <summary>
	/// Gets the learner preference delivery speed.
	/// </summary>
	/// <remarks>cmi.learner_preference.delivery_speed (real(10,7), range (0..*), RW) The learner’s preferred relative speed of content delivery</remarks>
	/// <returns>The learner preference delivery speed float.</returns>
	public static float GetLearnerPreferenceDeliverySpeed() {
		return studentRecord.learnerPreference.deliverySpeed;
	}

	/// <summary>
	/// Sets the learner preference delivery speed.
	/// </summary>
	/// <param name="value">float Value.</param>
	public static void SetLearnerPreferenceDeliverySpeed(float value) {
		string identifier = "cmi.learner_preference.delivery_speed";
		string strValue = ScormFormat.ToReal(value);
		studentRecord.learnerPreference.deliverySpeed = value;
		SetValue (identifier, strValue);
	}

	/// <summary>
	/// Gets the learner preference audio captioning.
	/// </summary>
	/// <remarks>cmi.learner_preference.audio_captioning (“-1″, “0″, “1″, RW) Specifies whether captioning text corresponding to audio is displayed</remarks>
	/// <returns>The learner preference audio captioning integer.</returns>
	public static int GetLearnerPreferenceAudioCaptioning() {
		return studentRecord.learnerPreference.audioCaptioning;
	}

	/// <summary>
	/// Sets the learner preference audio captioning.
	/// </summary>
	/// <param name="value">integer Value.</param>
	public static void SetLearnerPreferenceAudioCaptioning(int value) {
		string identifier = "cmi.learner_preference.audio_captioning";
		string strValue = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
		studentRecord.learnerPreference.audioCaptioning = value;
		SetValue (identifier, strValue);
	}

	/// <summary>
	/// Gets the location.
	/// </summary>
	/// <remarks>cmi.location (characterstring (SPM: 1000), RW) The learner’s current location in the SCO</remarks>
	/// <returns>The location string (bookmark).</returns>
	public static string GetLocation() {
		return studentRecord.location;
	}

	/// <summary>
	/// Sets the location.
	/// </summary>
	/// <param name="value">string location (bookmark) value.</param>
	public static void SetLocation(string value) {
		string identifier = "cmi.location";
		string strValue = value;
		studentRecord.location = value;
		SetValue (identifier, strValue);
	}

	public static List<StudentRecord.Objectives> GetObjectives() {
		return studentRecord.objectives;
	}

	/// <summary>
	/// Adds the objective.
	/// </summary>
	/// <remarks>
	/// Contains:
	/// 1. cmi.objectives.n.id (long_identifier_type (SPM: 4000), RW) Unique label for the objective
	/// 2. cmi.objectives.n.score._children (scaled,raw,min,max, RO) Listing of supported data model elements
	/// 3. cmi.objectives.n.score.scaled (real (10,7) range (-1..1), RW) Number that reflects the performance of the learner for the objective
	/// 4. cmi.objectives.n.score.raw (real (10,7), RW) Number that reflects the performance of the learner, for the objective, relative to the range bounded by the values of min and max
	/// 5. cmi.objectives.n.score.min (real (10,7), RW) Minimum value, for the objective, in the range for the raw score
	/// 6. cmi.objectives.n.score.max (real (10,7), RW) Maximum value, for the objective, in the range for the raw score
	/// 7. cmi.objectives.n.success_status (“passed”, “failed”, “unknown”, RW) Indicates whether the learner has mastered the objective
	/// 8. cmi.objectives.n.completion_status (“completed”, “incomplete”, “not attempted”, “unknown”, RW) Indicates whether the learner has completed the associated objective
	/// 9. cmi.objectives.n.progress_measure (real (10,7) range (0..1), RW) Measure of the progress the learner has made toward completing the objective
	/// 10. cmi.objectives.n.description (localized_string_type (SPM: 250), RW) Provides a brief informative description of the objective
	/// </remarks>
	/// <param name="objective">StudentRecord.Objectives objective.</param>
	/// <c>
	/// Usage:\n
	/// StudentRecord.Objectives newRecord = new StudentRecord.Objectives();\n
	/// StudentRecord.LearnerScore newScore = new StudentRecord.LearnerScore ();\n
	/// newScore.scaled = 0.8f;\n
	/// newScore.raw = 80f;\n
	/// newScore.max = 100f;\n
	/// newScore.min = 0f;\n
	/// newRecord.score = newScore;\n
	/// newRecord.successStatus = StudentRecord.SuccessStatusType.passed;\n
	/// newRecord.completionStatus = StudentRecord.CompletionStatusType.completed;\n
	/// newRecord.progressMeasure = 1f;\n
	/// newRecord.description = "The description of this objective";\n
	/// ScormManager.AddObjective(newRecord);
	/// </c>
	public static void AddObjective(StudentRecord.Objectives objective) {
		try {
			objective.id = "urn:STALS:objective-id-" + studentRecord.objectives.Count.ToString ();	//Override ID to ensure it is unique (use UpsertObjective to keep your own id)
			//All other properties must be set by the caller

			int i = studentRecord.objectives.Count;

			//The id must be set before any other field. The objective is added locally only if the LMS accepted the id
			//(keeps objectives.Count == cmi.objectives._count), or when there is no LMS at all (the list only feeds the UI).
			if (SetAndCache("cmi.objectives."+i+".id", objective.id)) {
				studentRecord.objectives.Add(objective);
				WriteObjectiveFields(i, objective);
			} else if (!IsLmsConnected) {
				studentRecord.objectives.Add(objective);
			}
		} catch(System.Exception e) {
            wgldebugPrint("***AddObjective***" + e.Message + "<br/>" + e.StackTrace + "<br/>" + e.Source);
        }
	}

	/// <summary>
	/// Updates the objective.
	/// </summary>
	/// <param name="index">Integer Index.</param>
	/// <param name="objective">StudentRecord.Objectives objective.</param>
	/// <c>
	/// Usage:\n
	/// StudentRecord.Objectives newRecord = new StudentRecord.Objectives();\n
	/// StudentRecord.LearnerScore newScore = new StudentRecord.LearnerScore ();\n
	/// newScore.scaled = 0.8f;\n
	/// newScore.raw = 80f;\n
	/// newScore.max = 100f;\n
	/// newScore.min = 0f;\n
	/// newRecord.score = newScore;\n
	/// newRecord.successStatus = StudentRecord.SuccessStatusType.passed;\n
	/// newRecord.completionStatus = StudentRecord.CompletionStatusType.completed;\n
	/// newRecord.progressMeasure = 1f;\n
	/// newRecord.description = "The description of this objective";\n
	/// ScormManager.UpdateObjective(1, newRecord);
	/// </c>
	public static void UpdateObjective(int index, StudentRecord.Objectives objective) {
		try {
			//cmi.objectives.n.id cannot change once set: it is only sent if it differs from the LMS value (a different id is refused by the LMS)
			WriteIfChanged("cmi.objectives."+index+".id", objective.id, false);
			WriteObjectiveFields(index, objective);
			studentRecord.objectives[index] = objective;
		} catch(System.Exception e) {
            wgldebugPrint("***UpdateObjective***" + e.Message + "<br/>" + e.StackTrace + "<br/>" + e.Source);
        }
	}

	static void WriteObjectiveFields(int index, StudentRecord.Objectives objective) {
		string p = "cmi.objectives." + index + ".";
		if (objective.score != null) {
			WriteIfChanged(p + "score.min", ScormFormat.ToReal(objective.score.min), true);
			WriteIfChanged(p + "score.max", ScormFormat.ToReal(objective.score.max), true);
			WriteIfChanged(p + "score.raw", ScormFormat.ToReal(objective.score.raw), true);
			WriteIfChanged(p + "score.scaled", ScormFormat.ToReal(objective.score.scaled), true);
		}
		WriteIfChanged(p + "success_status", CustomTypeToString(objective.successStatus), false);
		WriteIfChanged(p + "completion_status", CustomTypeToString(objective.completionStatus), false);
		WriteIfChanged(p + "progress_measure", ScormFormat.ToReal(objective.progressMeasure), true);
		WriteIfChanged(p + "description", objective.description, false);
	}

	/// <summary>Index n of the objective whose cmi.objectives.n.id is id (ordinal comparison), or -1.</summary>
	public static int FindObjectiveIndex(string id) {
		if (studentRecord == null || studentRecord.objectives == null || string.IsNullOrEmpty(id))
			return -1;
		for (int i = 0; i < studentRecord.objectives.Count; i++) {
			StudentRecord.Objectives objective = studentRecord.objectives[i];
			if (objective != null && string.Equals(objective.id, id, StringComparison.Ordinal))
				return i;
		}
		return -1;
	}

	/// <summary>The objective with this id (loaded from the LMS or written in this session), or null.</summary>
	public static StudentRecord.Objectives GetObjective(string id) {
		int index = FindObjectiveIndex(id);
		return index < 0 ? null : studentRecord.objectives[index];
	}

	/// <summary>
	/// Creates or updates the objective with data.id (e.g. "scenario-01") and returns its index n.
	/// </summary>
	/// <remarks>
	/// The index is looked up by id in the objectives loaded from the LMS, so on resume the existing entry is updated
	/// instead of being duplicated. A new objective is appended at cmi.objectives._count and its id is written before
	/// any other field. Only the fields set in data are written, in the order score.min, score.max, score.raw,
	/// score.scaled, success_status, completion_status, progress_measure, description, and each one only if it differs
	/// from the value the LMS already has. Every value is validated before the first write.
	/// </remarks>
	/// <returns>The objective index, or -1 if the LMS rejected the id of a new objective (nothing is added then).</returns>
	/// <exception cref="InvalidOperationException">ScormManager is not initialized.</exception>
	/// <exception cref="ArgumentException">Empty id, or a value out of range (scaled -1..1, min &lt;= raw &lt;= max, progress 0..1).</exception>
	public static int UpsertObjective(ScormObjectiveData data) {
		bool allWritten;
		return UpsertObjective(data, out allWritten);
	}

	/// <summary>
	/// Same as <see cref="UpsertObjective(ScormObjectiveData)"/>; allWritten is false if the LMS rejected any field
	/// (see also LastWriteFailed / LastWriteErrorCode). Rejected values are not cached, so the next upsert retries them.
	/// </summary>
	/// <exception cref="ArgumentException">Empty id, id with whitespace or longer than 4000 characters, or a value out of range.</exception>
	public static int UpsertObjective(ScormObjectiveData data, out bool allWritten) {
		RequireStudentRecord();
		BeginWrites();
		allWritten = false;
		if (data == null)
			throw new ArgumentNullException("data");
		if (string.IsNullOrEmpty(data.id) || data.id.Trim().Length == 0)
			throw new ArgumentException("The objective id is empty.", "data");
		ValidateIdentifier(data.id, "objective id");

		int index = FindObjectiveIndex(data.id);
		bool isNew = index < 0;
		if (isNew)
			index = studentRecord.objectives.Count;
		string p = "cmi.objectives." + index + ".";

		ValidateScore(p + "score.", data.score, "Objective '" + data.id + "'");
		if (data.progressMeasure.HasValue && !InRange(data.progressMeasure.Value, 0f, 1f))
			throw new ArgumentOutOfRangeException("data", data.progressMeasure.Value, "Objective '" + data.id + "': progress_measure must be between 0 and 1.");

		StudentRecord.Objectives record;
		if (isNew) {
			if (!SetAndCache(p + "id", data.id))
				return -1;
			record = new StudentRecord.Objectives();
			record.id = data.id;
			record.score = new StudentRecord.LearnerScore();
			record.successStatus = StudentRecord.SuccessStatusType.not_set;
			record.completionStatus = StudentRecord.CompletionStatusType.not_set;
			studentRecord.objectives.Add(record);
		} else {
			record = studentRecord.objectives[index];
			if (record.score == null)
				record.score = new StudentRecord.LearnerScore();
		}

		WriteScore(p + "score.", data.score, record.score);

		if (data.successStatus != StudentRecord.SuccessStatusType.not_set && WriteIfChanged(p + "success_status", CustomTypeToString(data.successStatus), false))
			record.successStatus = data.successStatus;
		if (data.completionStatus != StudentRecord.CompletionStatusType.not_set && WriteIfChanged(p + "completion_status", CustomTypeToString(data.completionStatus), false))
			record.completionStatus = data.completionStatus;
		if (data.progressMeasure.HasValue && WriteIfChanged(p + "progress_measure", ScormFormat.ToReal(data.progressMeasure.Value), true))
			record.progressMeasure = data.progressMeasure.Value;
		if (!string.IsNullOrEmpty(data.description) && WriteIfChanged(p + "description", data.description, false))
			record.description = data.description;

		allWritten = !LastWriteFailed;
		return index;
	}

	/// <summary>
	/// Writes the global score (cmi.score.*): only the fields set, only if they changed, min/max before raw.
	/// </summary>
	/// <returns>False if the LMS rejected any write.</returns>
	/// <exception cref="InvalidOperationException">ScormManager is not initialized.</exception>
	/// <exception cref="ArgumentException">scaled outside -1..1, min &gt; max, or raw outside min..max.</exception>
	public static bool UpdateScore(ScormScoreData score) {
		RequireStudentRecord();
		BeginWrites();
		ValidateScore("cmi.score.", score, "cmi.score");
		if (studentRecord.learnerScore == null)
			studentRecord.learnerScore = new StudentRecord.LearnerScore();
		return WriteScore("cmi.score.", score, studentRecord.learnerScore);
	}

	/// <summary>
	/// Writes cmi.success_status and cmi.completion_status if they changed (not_set = leave untouched).
	/// </summary>
	/// <returns>False if the LMS rejected any write.</returns>
	/// <exception cref="InvalidOperationException">ScormManager is not initialized.</exception>
	public static bool UpdateStatus(StudentRecord.SuccessStatusType successStatus, StudentRecord.CompletionStatusType completionStatus) {
		RequireStudentRecord();
		BeginWrites();
		bool ok = true;
		if (successStatus != StudentRecord.SuccessStatusType.not_set) {
			if (WriteIfChanged("cmi.success_status", CustomTypeToString(successStatus), false))
				studentRecord.successStatus = successStatus;
			else
				ok = false;
		}
		if (completionStatus != StudentRecord.CompletionStatusType.not_set) {
			if (WriteIfChanged("cmi.completion_status", CustomTypeToString(completionStatus), false))
				studentRecord.completionStatus = completionStatus;
			else
				ok = false;
		}
		return ok;
	}

	/// <summary>Writes cmi.progress_measure (0..1) if it changed.</summary>
	/// <returns>False if the LMS rejected the write.</returns>
	/// <exception cref="InvalidOperationException">ScormManager is not initialized.</exception>
	/// <exception cref="ArgumentOutOfRangeException">value outside 0..1.</exception>
	public static bool UpdateProgressMeasure(float value) {
		RequireStudentRecord();
		BeginWrites();
		if (!InRange(value, 0f, 1f))
			throw new ArgumentOutOfRangeException("value", value, "cmi.progress_measure must be between 0 and 1.");
		if (!WriteIfChanged("cmi.progress_measure", ScormFormat.ToReal(value), true))
			return false;
		studentRecord.progressMeasure = value;
		return true;
	}

	static void ValidateScore(string prefix, ScormScoreData score, string owner) {
		if (score == null)
			return;
		if (!IsFinite(score.raw) || !IsFinite(score.min) || !IsFinite(score.max) || !IsFinite(score.scaled))
			throw new ArgumentOutOfRangeException("score", owner + ": score values must be finite numbers (no NaN or Infinity).");
		if (score.scaled.HasValue && !InRange(score.scaled.Value, -1f, 1f))
			throw new ArgumentOutOfRangeException("score", score.scaled.Value, owner + ": score.scaled must be between -1 and 1.");
		float? min = score.min.HasValue ? score.min : KnownReal(prefix + "min");
		float? max = score.max.HasValue ? score.max : KnownReal(prefix + "max");
		if (min.HasValue && max.HasValue && min.Value > max.Value + RealTolerance)
			throw new ArgumentOutOfRangeException("score", owner + ": score.min (" + ScormFormat.ToReal(min.Value) + ") is greater than score.max (" + ScormFormat.ToReal(max.Value) + ").");
		if (score.raw.HasValue) {
			if (float.IsNaN(score.raw.Value))
				throw new ArgumentOutOfRangeException("score", owner + ": score.raw is NaN.");
			if (min.HasValue && score.raw.Value < min.Value - RealTolerance)
				throw new ArgumentOutOfRangeException("score", owner + ": score.raw (" + ScormFormat.ToReal(score.raw.Value) + ") is below score.min (" + ScormFormat.ToReal(min.Value) + ").");
			if (max.HasValue && score.raw.Value > max.Value + RealTolerance)
				throw new ArgumentOutOfRangeException("score", owner + ": score.raw (" + ScormFormat.ToReal(score.raw.Value) + ") is above score.max (" + ScormFormat.ToReal(max.Value) + ").");
		}
	}

	static bool WriteScore(string prefix, ScormScoreData score, StudentRecord.LearnerScore record) {
		if (score == null)
			return true;
		bool ok = true;
		if (score.min.HasValue) {
			if (WriteIfChanged(prefix + "min", ScormFormat.ToReal(score.min.Value), true)) record.min = score.min.Value; else ok = false;
		}
		if (score.max.HasValue) {
			if (WriteIfChanged(prefix + "max", ScormFormat.ToReal(score.max.Value), true)) record.max = score.max.Value; else ok = false;
		}
		if (score.raw.HasValue) {
			if (WriteIfChanged(prefix + "raw", ScormFormat.ToReal(score.raw.Value), true)) record.raw = score.raw.Value; else ok = false;
		}
		if (score.scaled.HasValue) {
			if (WriteIfChanged(prefix + "scaled", ScormFormat.ToReal(score.scaled.Value), true)) record.scaled = score.scaled.Value; else ok = false;
		}
		return ok;
	}

	const float RealTolerance = 1e-6f;

	static bool InRange(float value, float min, float max) {
		return !float.IsNaN(value) && value >= min - RealTolerance && value <= max + RealTolerance;
	}

	static bool IsFinite(float? value) {
		return !value.HasValue || (!float.IsNaN(value.Value) && !float.IsInfinity(value.Value));
	}

	/// <summary>SCORM 2004 long_identifier_type: URI-like, SPM 4000 characters; strict LMSs reject whitespace.</summary>
	static void ValidateIdentifier(string id, string what) {
		if (id.Length > 4000)
			throw new ArgumentException("The " + what + " is longer than 4000 characters.", "id");
		foreach (char c in id)
			if (char.IsWhiteSpace(c))
				throw new ArgumentException("The " + what + " '" + id + "' contains whitespace; use a URI-like id such as 'scenario-01'.", "id");
	}

	static void RequireStudentRecord() {
		if (studentRecord == null || scormAPIWrapper == null)
			throw new InvalidOperationException("ScormManager is not initialized (call ScormManager.Initialize or wait for Scorm_Initialize_Complete; SCORM 1.2 is not supported).");
	}

	/// <summary>
	/// Gets the max time allowed.
	/// </summary>
	/// <remarks>cmi.max_time_allowed (timeinterval (second,10,2), RO) Amount of accumulated time the learner is allowed to use a SCO</remarks>
	/// <returns>The max time allowed in seconds.</returns>
	public static float GetMaxTimeAllowed() {
		return studentRecord.maxTimeAllowed;
	}

	/// <summary>
	/// Gets the mode.
	/// </summary>
	/// <remarks>cmi.mode (“browse”, “normal”, “review”, RO) Identifies one of three possible modes in which the SCO may be presented to the learner</remarks>
	/// <returns>The StudentRecord.ModeType mode.</returns>
	public static StudentRecord.ModeType GetMode() {
		return studentRecord.mode;
	}

	/// <summary>
	/// Gets the progress measure.
	/// </summary>
	/// <remarks>cmi.progress_measure (real (10,7) range (0..1), RW) Measure of the progress the learner has made toward completing the SCO</remarks>
	/// <returns>The progress measure float.</returns>
	public static float GetProgressMeasure() {
		return studentRecord.progressMeasure;
	}

	/// <summary>
	/// Sets the progress measure.
	/// </summary>
	/// <param name="value">float Value.</param>
	public static void SetProgressMeasure(float value) {
		string identifier = "cmi.progress_measure";
		string strValue = ScormFormat.ToReal(value);
		studentRecord.progressMeasure = value;
		SetValue (identifier, strValue);
	}

	/// <summary>
	/// Gets the scaled passing score.
	/// </summary>
	/// <remarks>cmi.scaled_passing_score (real(10,7) range (-1 .. 1), RO) Scaled passing score required to master the SCO</remarks>
	/// <returns>The scaled passing score float.</returns>
	public static float GetScaledPassingScore() {
		return studentRecord.scaledPassingScore;
	}

	/// <summary>
	/// Gets the score.
	/// </summary>
	/// <remarks>
	/// Contains:
	/// 1. cmi.score.scaled <see cref="GetScoreScaled"/>
	/// 2. cmi.score.raw <see cref="GetScoreRaw"/>
	/// 3. cmi.score.min <see cref="GetScoreMin"/>
	/// 4. cmi.score.max  <see cref="GetScoreMax"/>
	/// </remarks>
	/// <returns>The StudentRecord.LearnerScore score.</returns>
	public static StudentRecord.LearnerScore GetScore() {
		return studentRecord.learnerScore;
	}

	/// <summary>
	/// Sets the score.
	/// </summary>
	/// <param name="learnerScore">Learner score.</param>
	public static void SetScore(StudentRecord.LearnerScore learnerScore) {
		string identifier = "cmi.score.scaled";
		string strValue = ScormFormat.ToReal(learnerScore.scaled);
		SetValue (identifier, strValue);

		identifier = "cmi.score.raw";
		strValue = ScormFormat.ToReal(learnerScore.raw);
		SetValue (identifier, strValue);

		identifier = "cmi.score.max";
		strValue = ScormFormat.ToReal(learnerScore.max);
		SetValue (identifier, strValue);

		identifier = "cmi.score.min";
		strValue = ScormFormat.ToReal(learnerScore.min);
		SetValue (identifier, strValue);

		studentRecord.learnerScore = learnerScore;

	}

	/// <summary>
	/// Gets the score scaled.
	/// </summary>
	/// <remarks>cmi.score.scaled (real (10,7) range (-1..1), RW) Number that reflects the performance of the learner</remarks>
	/// <returns>The score scaled float.</returns>
	public static float GetScoreScaled() {
		return studentRecord.learnerScore.scaled;
	}

	/// <summary>
	/// Sets the score scaled.
	/// </summary>
	/// <param name="value">float Value.</param>
	public static void SetScoreScaled(float value) {
		string identifier = "cmi.score.scaled";
		string strValue = ScormFormat.ToReal(value);
		studentRecord.learnerScore.scaled = value;
		SetValue (identifier, strValue);
	}

	/// <summary>
	/// Gets the score raw.
	/// </summary>
	/// <remarks>cmi.score.raw (real (10,7), RW) Number that reflects the performance of the learner relative to the range bounded by the values of min and max</remarks>
	/// <returns>The score raw float.</returns>
	public static float GetScoreRaw() {
		return studentRecord.learnerScore.raw;
	}

	/// <summary>
	/// Sets the score raw.
	/// </summary>
	/// <param name="value">float Value.</param>
	public static void SetScoreRaw(float value) {
		string identifier = "cmi.score.raw";
		string strValue = ScormFormat.ToReal(value);
		studentRecord.learnerScore.raw = value;
		SetValue (identifier, strValue);
	}

	/// <summary>
	/// Gets the score max.
	/// </summary>
	/// <remarks>cmi.score.max (real (10,7), RW) Maximum value in the range for the raw score</remarks>
	/// <returns>The score max float.</returns>
	public static float GetScoreMax() {
		return studentRecord.learnerScore.max;
	}

	/// <summary>
	/// Sets the score max.
	/// </summary>
	/// <param name="value">floatValue.</param>
	public static void SetScoreMax(float value) {
		string identifier = "cmi.score.max";
		string strValue = ScormFormat.ToReal(value);
		studentRecord.learnerScore.max = value;
		SetValue (identifier, strValue);
	}

	/// <summary>
	/// Gets the score minimum.
	/// </summary>
	/// <remarks>cmi.score.min (real (10,7), RW) Minimum value in the range for the raw score</remarks>
	/// <returns>The score minimum float.</returns>
	public static float GetScoreMin() {
		return studentRecord.learnerScore.min;
	}

	/// <summary>
	/// Sets the score minimum.
	/// </summary>
	/// <param name="value">float Value.</param>
	public static void SetScoreMin(float value) {
		string identifier = "cmi.score.min";
		string strValue = ScormFormat.ToReal(value);
		studentRecord.learnerScore.min = value;
		SetValue (identifier, strValue);
	}

	/// <summary>
	/// Sets the session time.
	/// </summary>
	/// <remarks>cmi.session_time (timeinterval (second,10,2), WO) Amount of time that the learner has spent in the current learner session for this SCO</remarks>
	/// <param name="value">float session time in seconds.</param>
	public static void SetSessionTime(float value) {
		string identifier = "cmi.session_time";
		string strValue = secondsToTimeInterval(value);
		SetValue (identifier, strValue);
	}

	/// <summary>
	/// Gets the success status.
	/// </summary>
	/// <remarks>cmi.success_status (“passed”, “failed”, “unknown”, RW) Indicates whether the learner has mastered the SCO</remarks>
	/// <returns>The StudentRecord.SuccessStatusType success status.</returns>
	public static StudentRecord.SuccessStatusType GetSuccessStatus() {
		return studentRecord.successStatus;
	}

	/// <summary>
	/// Sets the success status.
	/// </summary>
	/// <param name="value">StudentRecord.SuccessStatusType Value.</param>
	public static void SetSuccessStatus(StudentRecord.SuccessStatusType value) {
		string identifier = "cmi.success_status";
		string strValue = CustomTypeToString(value);
		if (strValue.Length == 0)																//not_set: "" is not a valid vocabulary value
			return;
		studentRecord.successStatus = value;
		SetValue (identifier, strValue);
	}

	/// <summary>
	/// Gets the suspend data.
	/// </summary>
	/// <remarks>cmi.suspend_data (characterstring (SPM: 64000), RW) Provides space to store and retrieve data between learner sessions</remarks>
	/// <returns>The suspend data string.</returns>
	public static string GetSuspendData() {
		return studentRecord.suspendData;
	}

	/// <summary>
	/// Sets the suspend data.
	/// </summary>
	/// <param name="value">string Value.</param>
	public static void SetSuspendData(string value) {
		string identifier = "cmi.suspend_data";
		string strValue = value;
		studentRecord.suspendData = value;
		SetValue (identifier, strValue);
	}

	/// <summary>
	/// Gets the time limit action.
	/// </summary>
	/// <remarks>cmi.time_limit_action (“exit,message”, “continue,message”, “exit,no message”, “continue,no message”, RO) Indicates what the SCO should do when cmi.max_time_allowed is exceeded</remarks>
	/// <returns>The StudentRecord.TimeLimitActionType time limit action.</returns>
	public static StudentRecord.TimeLimitActionType GetTimeLimitAction() {
		return studentRecord.timeLimitAction;
	}

	/// <summary>
	/// Gets the total time.
	/// </summary>
	/// <remarks>cmi.total_time (timeinterval (second,10,2), RO) Sum of all of the learner’s session times accumulated in the current learner attempt</remarks>
	/// <returns>The total time in seconds.</returns>
	public static float GetTotalTime() {
		return studentRecord.totalTime;
	}

	/// <summary>
	/// Loads the student record.
	/// </summary>
	/// <remarks>This is called on initialise and loads the SCORM data into the StudentRecord object. <see cref="Initialize_imp"/></remarks>
	/// <returns>The StudentRecord student record object.</returns>
	private static StudentRecord LoadStudentRecord() {
		studentRecord = new StudentRecord();

		studentRecord.version = LoadValue ("cmi._version");

		//Comments From Learner
		int commentsFromLearnerCount = ParseInt (LoadValue ("cmi.comments_from_learner._count"));
		studentRecord.commentsFromLearner = new List<StudentRecord.CommentsFromLearner>();

		if (commentsFromLearnerCount != 0) {
			
			for (int i = 0; i < commentsFromLearnerCount; i++) {
				string comment = LoadValue ("cmi.comments_from_learner."+i+".comment");
				string location = LoadValue ("cmi.comments_from_learner."+i+".location");
				DateTime timestamp = ScormFormat.ParseTimestamp(LoadValue ("cmi.comments_from_learner."+i+".timestamp"));

				StudentRecord.CommentsFromLearner newRecord = new StudentRecord.CommentsFromLearner();
				newRecord.comment = comment;
				newRecord.location = location;
				newRecord.timeStamp = timestamp;

				studentRecord.commentsFromLearner.Add(newRecord);
			}
		}

		//Comments From LMS
		int commentsFromLMSCount = ParseInt (LoadValue ("cmi.comments_from_lms._count"));
		studentRecord.commentsFromLMS = new List<StudentRecord.CommentsFromLMS>();
		
		if (commentsFromLMSCount != 0) {
			
			for (int i = 0; i < commentsFromLMSCount; i++) {
				string comment = LoadValue ("cmi.comments_from_lms."+i+".comment");
				string location = LoadValue ("cmi.comments_from_lms."+i+".location");
				DateTime timeStamp = ScormFormat.ParseTimestamp(LoadValue ("cmi.comments_from_lms."+i+".timestamp"));
				
				StudentRecord.CommentsFromLMS newRecord = new StudentRecord.CommentsFromLMS();
				newRecord.comment = comment;
				newRecord.location = location;
				newRecord.timeStamp = timeStamp;
				
				studentRecord.commentsFromLMS.Add(newRecord);
			}
		}

		studentRecord.completionStatus = StringToCompletionStatusType (LoadValue ("cmi.completion_status"));
		studentRecord.completionThreshold = ParseFloat(LoadValue ("cmi.completion_threshold"));
		studentRecord.credit = StringToCreditType (LoadValue ("cmi.credit"));
		studentRecord.entry = StringToEntryType (LoadValue ("cmi.entry"));

		//Interactions
		int interactionCount = ParseInt (LoadValue ("cmi.interactions._count"));
		studentRecord.interactions = new List<StudentRecord.LearnerInteractionRecord>();

		if (interactionCount != 0) {
						
			for (int i = 0; i < interactionCount; i++) {
				string id = LoadValue ("cmi.interactions."+i+".id");
				StudentRecord.InteractionType type = StringToInteractionType( LoadValue ("cmi.interactions."+i+".type") );
				DateTime timestamp = ScormFormat.ParseTimestamp(LoadValue ("cmi.interactions."+i+".timestamp"));
				float weighting = ParseFloat( LoadValue ("cmi.interactions."+i+".weighting") );
				string response = LoadValue ("cmi.interactions."+i+".learner_response");
				float latency = timeIntervalToSeconds ( LoadValue ("cmi.interactions."+i+".latency") );
				string description = LoadValue ("cmi.interactions."+i+".description");
				float estimate = 0;
				StudentRecord.ResultType result = StringToResultType(LoadValue ("cmi.interactions."+i+".result"), out estimate);
								
				StudentRecord.LearnerInteractionRecord newRecord = new StudentRecord.LearnerInteractionRecord();
				newRecord.id = id;
				newRecord.type = type;
				newRecord.timeStamp = timestamp;
				newRecord.weighting = weighting;
				newRecord.response = response;
				newRecord.latency = latency;
				newRecord.description = description;
				newRecord.result = result;
				newRecord.estimate = estimate;

				int interactionObjectivesCount = ParseInt (LoadValue ("cmi.interactions."+i+".objectives._count"));
				newRecord.objectives = new List<StudentRecord.LearnerInteractionObjective>();

				if(interactionObjectivesCount != 0) {
					for (int x = 0; x < interactionObjectivesCount; x++) {
						StudentRecord.LearnerInteractionObjective newObjective = new StudentRecord.LearnerInteractionObjective();
						newObjective.id = LoadValue ("cmi.interactions."+i+".objectives."+x+".id");
						newRecord.objectives.Add(newObjective);
					}
				}

				int correctResponsesCount = ParseInt (LoadValue ("cmi.interactions."+i+".correct_responses._count"));
				newRecord.correctResponses = new List<StudentRecord.LearnerInteractionCorrectResponse>();
				
				if(correctResponsesCount != 0) {
					for (int x = 0; x < correctResponsesCount; x++) {
						StudentRecord.LearnerInteractionCorrectResponse newCorrectResponse = new StudentRecord.LearnerInteractionCorrectResponse();
						newCorrectResponse.pattern = LoadValue ("cmi.interactions."+i+".correct_responses."+x+".pattern");
						newRecord.correctResponses.Add(newCorrectResponse);
					}
				}

				studentRecord.interactions.Add(newRecord);
			}
		}

		studentRecord.launchData = LoadValue ("cmi.launch_data");
		studentRecord.learnerID = LoadValue ("cmi.learner_id");
		studentRecord.learnerName = LoadValue ("cmi.learner_name");
		//learner_preference
		StudentRecord.LearnerPreference learnerPreference = new StudentRecord.LearnerPreference ();
		learnerPreference.audioLevel =  ParseFloat (LoadValue ("cmi.learner_preference.audio_level"));
		learnerPreference.langauge = LoadValue ("cmi.learner_preference.language");
		learnerPreference.deliverySpeed = ParseFloat (LoadValue ("cmi.learner_preference.delivery_speed"));
		learnerPreference.audioCaptioning = ParseInt (LoadValue ("cmi.learner_preference.audio_captioning"));
		studentRecord.learnerPreference = learnerPreference;

		studentRecord.location = LoadValue ("cmi.location");

		//Objectives
		int objectivesCount = ParseInt (LoadValue ("cmi.objectives._count"));
		studentRecord.objectives = new List<StudentRecord.Objectives> ();

		if (objectivesCount != 0) {
			for (int i = 0; i < objectivesCount; i++) {
				string id = LoadValue ("cmi.objectives."+i+".id");

				StudentRecord.LearnerScore objectivesScore = new StudentRecord.LearnerScore();
				objectivesScore.scaled = ParseFloat (LoadValue ("cmi.objectives."+i+".score.scaled"));
				objectivesScore.raw = ParseFloat (LoadValue ("cmi.objectives."+i+".score.raw"));
				objectivesScore.max = ParseFloat (LoadValue ("cmi.objectives."+i+".score.max"));
				objectivesScore.min = ParseFloat (LoadValue ("cmi.objectives."+i+".score.min"));

				StudentRecord.SuccessStatusType successStatus = StringToSuccessStatusType(LoadValue ("cmi.objectives."+i+".success_status"));
				StudentRecord.CompletionStatusType completionStatus = StringToCompletionStatusType(LoadValue ("cmi.objectives."+i+".completion_status"));
				float progressMeasure = ParseFloat (LoadValue ("cmi.objectives."+i+".progress_measure"));
				string description = LoadValue ("cmi.objectives."+i+".description");

				StudentRecord.Objectives newRecord = new StudentRecord.Objectives();
				newRecord.id = id;
				newRecord.score = objectivesScore;
				newRecord.successStatus = successStatus;
				newRecord.completionStatus = completionStatus;
				newRecord.progressMeasure = progressMeasure;
				newRecord.description = description;

				studentRecord.objectives.Add(newRecord);
			}
		}

		studentRecord.maxTimeAllowed = timeIntervalToSeconds (LoadValue ("cmi.max_time_allowed"));
		studentRecord.mode = StringToModeType (LoadValue ("cmi.mode"));
		studentRecord.progressMeasure = ParseFloat (LoadValue ("cmi.progress_measure"));
		studentRecord.scaledPassingScore = ParseFloat(LoadValue ("cmi.scaled_passing_score"));
		//Score
		studentRecord.learnerScore = new StudentRecord.LearnerScore ();
		studentRecord.learnerScore.scaled = ParseFloat (LoadValue ("cmi.score.scaled"));
		studentRecord.learnerScore.raw = ParseFloat (LoadValue ("cmi.score.raw"));
		studentRecord.learnerScore.max = ParseFloat (LoadValue ("cmi.score.max"));
		studentRecord.learnerScore.min = ParseFloat (LoadValue ("cmi.score.min"));

		studentRecord.successStatus = StringToSuccessStatusType (LoadValue ("cmi.success_status"));
		studentRecord.suspendData = LoadValue ("cmi.suspend_data");
		studentRecord.timeLimitAction = StringToTimeLimitActionType (LoadValue ("cmi.time_limit_action"));
		studentRecord.totalTime = timeIntervalToSeconds (LoadValue ("cmi.total_time"));

		return studentRecord;
	}

	/// <summary>
	/// Parses the float.
	/// </summary>
	/// <remarks>Invariant culture; returns 0 for an empty or invalid string.</remarks>
	/// <returns>The float.</returns>
	/// <param name="str">String.</param>
	private static float ParseFloat(string str) {
		return ScormFormat.ParseReal(str);
	}

	/// <summary>
	/// Parses the int.
	/// </summary>
	/// <remarks>Invariant culture; returns 0 for an empty or invalid string.</remarks>
	/// <returns>The int.</returns>
	/// <param name="str">String.</param>
	private static int ParseInt(string str) {
		return ScormFormat.ParseInt(str);
	}

	/// <summary>
	/// Converts a StudentRecord enum to its SCORM vocabulary token (e.g. true_false -> "true-false", not_set -> "").
	/// </summary>
	/// <returns>The SCORM vocabulary string of the custom enum.</returns>
	/// <param name="value">Value.</param>
	public static string CustomTypeToString(object value) {
		return ScormFormat.ToVocabulary(value);
	}

	/// <summary>
	/// Convert string to the StudentRecord ResultType.  Pass the estimate as an out parameter so that it can be set to a float value if needed.
	/// </summary>
	/// <returns>Result Type.</returns>
	/// <param name="str">String.</param>
	/// <param name="estimate">Estimate.</param>
	public static StudentRecord.ResultType StringToResultType(string str, out float estimate) {
		return ScormFormat.StringToResultType(str, out estimate);
	}

	/// <summary>
	/// Convert string to the StudentRecord InteractionType.
	/// </summary>
	/// <returns>The to interaction type.</returns>
	/// <param name="str">String.</param>
	public static StudentRecord.InteractionType StringToInteractionType(string str) {
		return ScormFormat.StringToInteractionType(str);
	}

	private static StudentRecord.EntryType StringToEntryType(string str) {
		return ScormFormat.StringToEntryType(str);
	}

	private static StudentRecord.TimeLimitActionType StringToTimeLimitActionType (string str) {
		return ScormFormat.StringToTimeLimitActionType(str);
	}

	private static StudentRecord.CompletionStatusType StringToCompletionStatusType (string str) {
		return ScormFormat.StringToCompletionStatusType(str);
	}

	private static StudentRecord.SuccessStatusType StringToSuccessStatusType (string str) {
		return ScormFormat.StringToSuccessStatusType(str);
	}

	private static StudentRecord.CreditType StringToCreditType(string str) {
		return ScormFormat.StringToCreditType(str);
	}

	private static StudentRecord.ModeType StringToModeType(string str) {
		return ScormFormat.StringToModeType(str);
	}

	/// <summary>
	/// Convert Seconds to the SCORM timeInterval (invariant culture, keeps the fraction: 18.4 -> "P0DT0H0M18.4S").
	/// </summary>
	/// <returns>timeInterval string.</returns>
	/// <param name="seconds">Seconds.</param>
	private static string secondsToTimeInterval(float seconds) {
		return ScormFormat.SecondsToTimeInterval(seconds);
	}

	/// <summary>
	/// Convert SCORM timeInterval to seconds.
	/// </summary>
	/// <returns>Seconds.</returns>
	/// <param name="timeInterval">SCORM TimeInterval.</param>
	private static float timeIntervalToSeconds(string timeInterval) {
		return ScormFormat.TimeIntervalToSeconds(timeInterval);
	}

}
