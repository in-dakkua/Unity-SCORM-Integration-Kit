/***********************************************************************************************************************
 * Unity-SCORM Integration Kit
 *
 * Unity-SCORM Integration Wrapper (Bridge)
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


using System;
using System.Runtime.InteropServices;

/// <summary>
/// Scorm API wrapper.  Forms the 'Bridge' between the Unity3D code and the scorm.js code that communicates with the LMS.
/// </summary>
/// <remarks>
/// The SCORM 2004 JavaScript API is synchronous, so every call into Plugins/scorm.jslib returns its result directly
/// (strings come back as a UTF-8 buffer allocated with _malloc, which IL2CPP frees after marshalling).
/// There is no SendMessage round trip, no queue, no polling and no Thread.Sleep.
///
/// Outside a WebGL player (Unity Editor, EditMode tests, other platforms) the same calls go to ScormEditorBackend,
/// an in-memory LMS with the same seed data as ScormSimulator.js.
/// </remarks>
public class ScormAPIWrapper {

#if UNITY_WEBGL && !UNITY_EDITOR
	[DllImport("__Internal")]
	private static extern void wgldebugPrint(string str);

	[DllImport("__Internal")]
	private static extern int wglInitialize();

	[DllImport("__Internal")]
	private static extern int wglIsApiFound();

	[DllImport("__Internal")]
	private static extern int wglIsScorm2004();

	[DllImport("__Internal")]
	private static extern string wglGetValue(string identifier);

	[DllImport("__Internal")]
	private static extern int wglSetValue(string identifier, string value);

	[DllImport("__Internal")]
	private static extern int wglCommit();

	[DllImport("__Internal")]
	private static extern int wglTerminate();

	[DllImport("__Internal")]
	private static extern int wglGetLastError();

	[DllImport("__Internal")]
	private static extern string wglGetErrorString(int errorCode);
#else
	private static void wgldebugPrint(string str) { UnityEngine.Debug.Log("[SCORM] " + str); }
	private static int wglInitialize() { return ScormEditorBackend.Initialize() ? 1 : 0; }
	private static int wglIsApiFound() { return 1; }
	private static int wglIsScorm2004() { return 1; }
	private static string wglGetValue(string identifier) { return ScormEditorBackend.GetValue(identifier); }
	private static int wglSetValue(string identifier, string value) { return ScormEditorBackend.SetValue(identifier, value) ? 1 : 0; }
	private static int wglCommit() { return ScormEditorBackend.Commit() ? 1 : 0; }
	private static int wglTerminate() { return ScormEditorBackend.Terminate() ? 1 : 0; }
	private static int wglGetLastError() { return ScormEditorBackend.LastError; }
	private static string wglGetErrorString(int errorCode) { return ScormEditorBackend.GetErrorString(errorCode); }
#endif

	/// <summary>Obsolete: results are returned directly by the jslib. Kept so third-party code still compiles.</summary>
	[Obsolete("The SCORM bridge is synchronous now; APICallResult is no longer used.")]
	public class APICallResult {
		/// <summary>The string result of the call to the SCORM API</summary>
		public string Result;
		/// <summary>The random key assigned to the call to uniquely identify it for the callback</summary>
		public string Key;
		/// <summary>Possible error code</summary>
		public string ErrorCode;
		/// <summary>Possible error description</summary>
		public string ErrorDescription;
	}

	/// <summary>The name of the Unity3D object that receives "LogMessage" (should be the ScormManager)</summary>
	string CallbackObjectName;

	/// <summary>Allows interoperability bewteen SCORM 2004 and 1.2 (Not yet implemented)</summary>
	public bool IsScorm2004;

	/// <summary>True when scorm.js found an LMS API (API_1484_11 or API), or the Editor backend is in use.</summary>
	public bool IsApiFound { get; private set; }

	/// <summary>True when the LMS accepted Initialize("").</summary>
	public bool IsInitialized { get; private set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="ScormAPIWrapper"/> class.
	/// </summary>
	/// <param name="obj">Name of the GameObject that receives the "LogMessage" messages.</param>
	/// <param name="callback">Unused; kept for backwards compatibility.</param>
	public ScormAPIWrapper(string obj, string callback) {
		CallbackObjectName = obj;
	}

	/// <summary>Writes to the browser console (or the #console element) in WebGL, or to the Unity console elsewhere.</summary>
	public static void DebugPrint(string text) {
		try {
			wgldebugPrint(text);
		} catch (Exception) {
			UnityEngine.Debug.Log("[SCORM] " + text);
		}
	}

	/// <summary>
	/// Raised after every SCORM Run-Time API call (Initialize, GetValue, SetValue, Commit, Terminate) with its
	/// result and LMS error code. Static because ScormManager creates a new wrapper on every Initialize.
	/// </summary>
	/// <remarks>
	/// Handlers run synchronously on the main thread. An exception thrown by a handler is logged and swallowed so a
	/// broken log UI cannot break the communication with the LMS.
	/// Handlers should not call the SCORM API. If they do, the call works normally but does not raise the event again
	/// (no recursion), and the outer call keeps its own LastErrorCode.
	/// The event is static: a MonoBehaviour must subscribe in OnEnable and unsubscribe in OnDisable.
	/// </remarks>
	public static event Action<ScormCallInfo> CallCompleted;

	/// <summary>
	/// Start up the SCORM API Wrapper.
	/// </summary>
	/// <remarks>
	/// Calls Initialize on the LMS and checks whether it is a 1.2 or 2004 LMS. Without an LMS it returns immediately:
	/// IsApiFound is false and every GetValue returns "".
	/// </remarks>
	public void Initialize() {
		IsInitialized = wglInitialize() == 1;
		IsApiFound = wglIsApiFound() == 1;
		IsScorm2004 = wglIsScorm2004() == 1;

		int error = 0;
		string errorText = "";
		if (!IsApiFound) {
			errorText = NoApiText;
			Log("No LMS API found: running without an LMS");
		} else if (!IsInitialized) {
			ReadLastError(out error, out errorText);
			Log("LMS Initialize failed: " + error + " " + errorText);
		}
		RaiseCall(ScormCallOperation.Initialize, "", "", IsInitialized ? "true" : "false", IsInitialized, error, errorText);

		if(IsScorm2004)
			Log("ScormVersion is 2004");
		else
			Log("ScormVersion is 1.2");
	}

	/// <summary>Obsolete: results are returned directly by the jslib. This method does nothing.</summary>
	[Obsolete("The SCORM bridge is synchronous now; SetCallbackValue does nothing.")]
	public void SetCallbackValue(string input) {
	}

	/// <summary>Error code reported by the LMS for the last GetValue/SetValue/Commit/Terminate (0 = no error).</summary>
	public int LastErrorCode { get; private set; }

	/// <summary>
	/// Get a value from the javascript API
	/// </summary>
	/// <returns>The value returned by the LMS ("" on error).</returns>
	/// <param name="identifier">The dot notation identifier of the data model element to get</param>
	public string GetValue(string identifier) {
		Log("Get " + identifier);
		string result = wglGetValue(identifier) ?? "";

		int error = 0;
		string errorText = "";
		if (IsApiFound)
			ReadLastError(out error, out errorText);
		else
			errorText = NoApiText;
		LastErrorCode = error;
		if (error == 0)
			Log("Got  " + result);
		else
			Log("Error:" + error + " " + errorText + " Result: " + result);

		RaiseCall(ScormCallOperation.GetValue, identifier, "", result, IsApiFound && error == 0, error, error == 0 && IsApiFound ? "" : errorText);
		return result;
	}

	/// <summary>
	/// Set a value on the javascript API
	/// </summary>
	/// <returns>True if the LMS accepted the value.</returns>
	/// <param name="identifier">The dot notation identifier of the data model element to set.</param>
	/// <param name="value">Value to set</param>
	public bool SetValue(string identifier, string value) {
		Log("Set  " + identifier + " to " + value);
		bool result = wglSetValue(identifier, value ?? "") == 1;
		int error;
		string errorText;
		FailureError(result, out error, out errorText);
		if (result)
			Log("Result true");
		else
			Log("Error:" + error + " " + errorText);
		RaiseCall(ScormCallOperation.SetValue, identifier, value, result ? "true" : "false", result, error, errorText);
		return result;
	}

	/// <summary>
	/// Call the commit function in the javascript layer
	/// </summary>
	public bool Commit() {
		bool result = wglCommit() == 1;
		int error;
		string errorText;
		FailureError(result, out error, out errorText);
		if (!result)
			Log("Commit failed: " + error + " " + errorText);
		RaiseCall(ScormCallOperation.Commit, "", "", result ? "true" : "false", result, error, errorText);
		return result;
	}

	/// <summary>
	/// Call the terminate function in the javascript layer
	/// </summary>
	public bool Terminate() {
		bool result = wglTerminate() == 1;
		int error;
		string errorText;
		FailureError(result, out error, out errorText);
		if (!result)
			Log("Terminate failed: " + error + " " + errorText);
		RaiseCall(ScormCallOperation.Terminate, "", "", result ? "true" : "false", result, error, errorText);
		return result;
	}

	const string NoApiText = "No LMS API";

	// A successful Set/Commit/Terminate implies error 0, so GetLastError is only called on failure.
	void FailureError(bool succeeded, out int error, out string errorText) {
		error = 0;
		errorText = "";
		if (succeeded) {
			LastErrorCode = 0;
			return;
		}
		if (!IsApiFound) {
			errorText = NoApiText;
			LastErrorCode = 0;
			return;
		}
		ReadLastError(out error, out errorText);
		LastErrorCode = error;
	}

	void ReadLastError(out int error, out string errorText) {
		error = wglGetLastError();
		errorText = error == 0 ? "" : (wglGetErrorString(error) ?? "");
	}

	static bool raising;

	void RaiseCall(ScormCallOperation operation, string element, string value, string result, bool succeeded, int error, string errorText) {
		Action<ScormCallInfo> handlers = CallCompleted;
		if (handlers == null || raising)
			return;
		int lastErrorCode = LastErrorCode;
		raising = true;
		try {
			ScormCallInfo info = new ScormCallInfo(operation, element, value, result, succeeded, error, errorText);
			foreach (Delegate handler in handlers.GetInvocationList()) {
				try {
					((Action<ScormCallInfo>)handler)(info);
				} catch (Exception e) {
					UnityEngine.Debug.LogException(e);
				}
			}
		} finally {
			raising = false;
			// A handler that called the API (on this or another wrapper) must not change the error of this call.
			LastErrorCode = lastErrorCode;
		}
	}

	/// <summary>
	/// Send a log command up to the parent GameObject.  The actual implementation of the Log function is up to your own code.
	/// </summary>
	/// <param name="text">
	/// the text to log
	/// </param>
	public void Log(string text) {
		if (string.IsNullOrEmpty(CallbackObjectName))
			return;
		UnityEngine.GameObject target = UnityEngine.GameObject.Find(CallbackObjectName);
		if (target != null)
			target.SendMessage("LogMessage", text, UnityEngine.SendMessageOptions.DontRequireReceiver);
	}

}
