using System;
using System.Collections.Generic;

namespace Scorm.Scenarios
{
    /// <summary>Pure score arithmetic shared by the tracker and the UI (no SCORM calls).</summary>
    public static class ScenarioScoring
    {
        /// <summary>Global raw score range written to cmi.score.min / cmi.score.max (Moodle shows cmi.score.raw).</summary>
        public const float GlobalMin = 0f;
        public const float GlobalMax = 100f;

        private const float Tolerance = 1e-5f;

        /// <summary>(raw - min) / (max - min), clamped to 0..1.</summary>
        public static float ToScaled(ScenarioDefinition definition, float raw)
        {
            float range = definition.MaxScore - definition.MinScore;
            if (range <= 0f)
                return 0f;
            float scaled = (raw - definition.MinScore) / range;
            return Clamp01(scaled);
        }

        /// <summary>Raw score that corresponds to a scaled score (0..1) in the range of the scenario.</summary>
        public static float FromScaled(ScenarioDefinition definition, float scaled)
        {
            return definition.MinScore + Clamp01(scaled) * (definition.MaxScore - definition.MinScore);
        }

        public static bool IsPassed(ScenarioDefinition definition, float scaled)
        {
            return scaled + Tolerance >= definition.PassingScaledScore;
        }

        /// <summary>
        /// Global scaled score (0..1), or null when it cannot be computed yet (nothing completed with
        /// <see cref="GlobalScoreMethod.WeightedAverageCompletedOnly"/>, or no scenario at all). When every weight is 0
        /// the scenarios count equally.
        /// </summary>
        public static float? GlobalScaled(GlobalScoreMethod method, IReadOnlyList<ScenarioProgress> scenarios)
        {
            if (scenarios == null || scenarios.Count == 0)
                return null;
            bool completedOnly = method == GlobalScoreMethod.WeightedAverageCompletedOnly;
            bool anyCompleted = false;
            double weightSum = 0, weightedSum = 0;
            double plainSum = 0;
            int plainCount = 0;
            foreach (ScenarioProgress scenario in scenarios)
            {
                float? scaled = scenario.ReportedScaled;
                if (scaled.HasValue)
                    anyCompleted = true;
                if (completedOnly && !scaled.HasValue)
                    continue;
                double value = scaled ?? 0f;
                double weight = Math.Max(0f, scenario.Definition.Weight);
                weightSum += weight;
                weightedSum += weight * value;
                plainSum += value;
                plainCount++;
            }
            if (!anyCompleted || plainCount == 0)
                return null;
            double result = weightSum > 0 ? weightedSum / weightSum : plainSum / plainCount;
            return Clamp01((float)result);
        }

        /// <summary>Global raw score 0..100 with two decimals (the value Moodle puts in the gradebook).</summary>
        public static float ToGlobalRaw(float scaled)
        {
            return (float)Math.Round(Clamp01(scaled) * (GlobalMax - GlobalMin) + GlobalMin, 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// passed / failed only once every scenario is completed (failed if the global score is below the threshold,
        /// or, when required, a scenario is failed); unknown before that.
        /// </summary>
        public static StudentRecord.SuccessStatusType GlobalSuccess(ScenarioCatalog catalog, IReadOnlyList<ScenarioProgress> scenarios, float? globalScaled)
        {
            if (scenarios.Count == 0 || !AllCompleted(scenarios) || !globalScaled.HasValue)
                return StudentRecord.SuccessStatusType.unknown;
            bool passed = globalScaled.Value + Tolerance >= catalog.GlobalPassingScaledScore;
            if (passed && catalog.RequireAllScenariosPassed)
                foreach (ScenarioProgress scenario in scenarios)
                    if (!scenario.IsPassed)
                        passed = false;
            return passed ? StudentRecord.SuccessStatusType.passed : StudentRecord.SuccessStatusType.failed;
        }

        public static StudentRecord.CompletionStatusType GlobalCompletion(IReadOnlyList<ScenarioProgress> scenarios)
        {
            return scenarios.Count > 0 && AllCompleted(scenarios)
                ? StudentRecord.CompletionStatusType.completed
                : StudentRecord.CompletionStatusType.incomplete;
        }

        /// <summary>Completed scenarios / total (0..1).</summary>
        public static float ProgressMeasure(IReadOnlyList<ScenarioProgress> scenarios)
        {
            if (scenarios.Count == 0)
                return 0f;
            int completed = 0;
            foreach (ScenarioProgress scenario in scenarios)
                if (scenario.IsCompleted)
                    completed++;
            return (float)Math.Round((double)completed / scenarios.Count, 4);
        }

        public static bool AllCompleted(IReadOnlyList<ScenarioProgress> scenarios)
        {
            foreach (ScenarioProgress scenario in scenarios)
                if (!scenario.IsCompleted)
                    return false;
            return true;
        }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value < 0f)
                return 0f;
            return value > 1f ? 1f : value;
        }
    }
}
