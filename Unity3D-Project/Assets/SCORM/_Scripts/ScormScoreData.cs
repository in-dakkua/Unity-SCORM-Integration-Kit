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
/// Partial SCORM 2004 score (cmi.score.* or cmi.objectives.n.score.*). A null field is not written to the LMS.
/// </summary>
public class ScormScoreData {

	/// <summary>score.raw: between min and max when both are known.</summary>
	public float? raw;

	/// <summary>score.min.</summary>
	public float? min;

	/// <summary>score.max.</summary>
	public float? max;

	/// <summary>score.scaled: -1..1.</summary>
	public float? scaled;

	public ScormScoreData() { }

	public ScormScoreData(float? raw, float? min, float? max, float? scaled) {
		this.raw = raw;
		this.min = min;
		this.max = max;
		this.scaled = scaled;
	}

	/// <summary>
	/// Raw score with its range; scaled = (raw - min) / (max - min), clamped to -1..1 (0 when max == min).
	/// </summary>
	public static ScormScoreData FromRaw(float raw, float min, float max) {
		float range = max - min;
		float scaled = Math.Abs(range) < 1e-9f ? 0f : (raw - min) / range;
		if (scaled < -1f) scaled = -1f;
		if (scaled > 1f) scaled = 1f;
		return new ScormScoreData(raw, min, max, scaled);
	}

	/// <summary>True when no field is set.</summary>
	public bool IsEmpty {
		get { return !raw.HasValue && !min.HasValue && !max.HasValue && !scaled.HasValue; }
	}
}
