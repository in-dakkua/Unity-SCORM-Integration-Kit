/***********************************************************************************************************************
 * Unity-SCORM Integration Kit
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with
 * the License. You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 **********************************************************************************************************************/

/// <summary>SCORM Run-Time API call reported by <see cref="ScormAPIWrapper.CallCompleted"/>.</summary>
public enum ScormCallOperation {
	Initialize,
	GetValue,
	SetValue,
	Commit,
	Terminate
}
