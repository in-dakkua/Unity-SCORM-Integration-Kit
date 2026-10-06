/***********************************************************************************************************************
 * Unity-SCORM Integration Kit
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with
 * the License. You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 **********************************************************************************************************************/

/// <summary>
/// Partial update of one cmi.objectives.n entry, located by its id (see <see cref="ScormManager.UpsertObjective"/>).
/// </summary>
/// <remarks>
/// Only the fields that are set are written: null score fields / progressMeasure / description and the not_set
/// statuses are left untouched in the LMS. A typical multi-scenario app uses one objective per scenario
/// (e.g. id "scenario-01").
/// </remarks>
public class ScormObjectiveData {

	/// <summary>cmi.objectives.n.id (long_identifier_type, required). Stable id, e.g. "scenario-01".</summary>
	public string id;

	/// <summary>cmi.objectives.n.score.* (null = leave the score untouched).</summary>
	public ScormScoreData score;

	/// <summary>cmi.objectives.n.success_status (not_set = leave untouched).</summary>
	public StudentRecord.SuccessStatusType successStatus = StudentRecord.SuccessStatusType.not_set;

	/// <summary>cmi.objectives.n.completion_status (not_set = leave untouched).</summary>
	public StudentRecord.CompletionStatusType completionStatus = StudentRecord.CompletionStatusType.not_set;

	/// <summary>cmi.objectives.n.progress_measure, 0..1 (null = leave untouched).</summary>
	public float? progressMeasure;

	/// <summary>cmi.objectives.n.description (null or "" = leave untouched).</summary>
	public string description;

	public ScormObjectiveData() { }

	public ScormObjectiveData(string id) {
		this.id = id;
	}
}
