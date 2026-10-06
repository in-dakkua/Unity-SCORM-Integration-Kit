/***********************************************************************************************************************
 * Unity-SCORM Integration Kit
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with
 * the License. You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 **********************************************************************************************************************/

using System;

/// <summary>
/// One call to the SCORM Run-Time API, as reported by <see cref="ScormAPIWrapper.CallCompleted"/> (and
/// <see cref="ScormManager.ScormCall"/>). Meant for logging / on-screen API consoles.
/// </summary>
public sealed class ScormCallInfo {

	/// <summary>Which API function was called.</summary>
	public ScormCallOperation Operation { get; private set; }

	/// <summary>Data model element (GetValue/SetValue); "" for Initialize, Commit and Terminate.</summary>
	public string Element { get; private set; }

	/// <summary>Value sent (SetValue); "" for the other operations.</summary>
	public string Value { get; private set; }

	/// <summary>Value returned by the LMS: the element value for GetValue, "true"/"false" for the other operations.</summary>
	public string Result { get; private set; }

	/// <summary>True when the LMS reported no error.</summary>
	public bool Succeeded { get; private set; }

	/// <summary>SCORM error code reported by the LMS (0 = no error, or no LMS API).</summary>
	public int ErrorCode { get; private set; }

	/// <summary>LMS text for ErrorCode ("" when there is no error; "No LMS API" when scorm.js found no API).</summary>
	public string ErrorDescription { get; private set; }

	/// <summary>Local time of the call.</summary>
	public DateTime Time { get; private set; }

	public ScormCallInfo(ScormCallOperation operation, string element, string value, string result, bool succeeded, int errorCode, string errorDescription) {
		Operation = operation;
		Element = element ?? "";
		Value = value ?? "";
		Result = result ?? "";
		Succeeded = succeeded;
		ErrorCode = errorCode;
		ErrorDescription = errorDescription ?? "";
		Time = DateTime.Now;
	}

	public override string ToString() {
		string text = Operation.ToString();
		if (Element.Length > 0)
			text += " " + Element;
		if (Operation == ScormCallOperation.SetValue)
			text += " = \"" + Value + "\"";
		text += " -> \"" + Result + "\"";
		if (!Succeeded || ErrorCode != 0)
			text += " [error " + ErrorCode + (ErrorDescription.Length > 0 ? ": " + ErrorDescription : "") + "]";
		return text;
	}
}
