using System;
using System.Collections.Generic;

namespace Scorm.Scenarios.Demo
{
    /// <summary>
    /// Generates a random attempt of a scenario (decisions + score) for the demo. Replace it with the real scenario
    /// gameplay in the final project: the tracker calls stay the same.
    /// </summary>
    /// <remarks>
    /// Only true-false and choice interactions are generated, with responses in their SCORM 2004 format
    /// ("true"/"false"; choice ids without spaces), because a strict LMS (SCORM Cloud) answers 406 otherwise.
    /// </remarks>
    public sealed class ScenarioSimulator
    {
        private static readonly string[] ChoiceOptions = { "opt-a", "opt-b", "opt-c", "opt-d" };

        private Random _random;

        public ScenarioSimulator(int seed)
        {
            Reseed(seed);
        }

        public int Seed { get; private set; }

        public void Reseed(int seed)
        {
            Seed = seed;
            _random = new Random(seed);
        }

        public SimulatedAttempt Play(ScenarioDefinition scenario)
        {
            float skill = 0.35f + (float)_random.NextDouble() * 0.65f;
            int count = Math.Max(1, scenario.SimulatedDecisionCount);
            List<SimulatedDecision> decisions = new List<SimulatedDecision>(count);
            int correctCount = 0;
            for (int i = 0; i < count; i++)
            {
                bool correct = _random.NextDouble() < skill;
                if (correct)
                    correctCount++;
                decisions.Add(i % 2 == 0 ? TrueFalse(scenario, i, correct) : Choice(scenario, i, correct));
            }
            float ratio = (float)correctCount / count;
            float noise = ((float)_random.NextDouble() - 0.5f) * 0.1f;
            float scaled = Math.Min(1f, Math.Max(0f, ratio * 0.95f + noise + 0.05f));
            float raw = (float)Math.Round(ScenarioScoring.FromScaled(scenario, scaled), 1);
            raw = Math.Min(scenario.MaxScore, Math.Max(scenario.MinScore, raw));
            return new SimulatedAttempt(decisions, raw);
        }

        private SimulatedDecision TrueFalse(ScenarioDefinition scenario, int index, bool correct)
        {
            bool expected = _random.Next(2) == 0;
            bool answer = correct ? expected : !expected;
            return new SimulatedDecision(
                "d" + (index + 1),
                StudentRecord.InteractionType.true_false,
                answer ? "true" : "false",
                expected ? "true" : "false",
                correct ? StudentRecord.ResultType.correct : StudentRecord.ResultType.incorrect,
                Latency(),
                scenario.Title + ": decisión " + (index + 1) + " (verdadero/falso)");
        }

        private SimulatedDecision Choice(ScenarioDefinition scenario, int index, bool correct)
        {
            int expected = _random.Next(ChoiceOptions.Length);
            int answer = expected;
            if (!correct)
                answer = (expected + 1 + _random.Next(ChoiceOptions.Length - 1)) % ChoiceOptions.Length;
            return new SimulatedDecision(
                "d" + (index + 1),
                StudentRecord.InteractionType.choice,
                ChoiceOptions[answer],
                ChoiceOptions[expected],
                correct ? StudentRecord.ResultType.correct : StudentRecord.ResultType.incorrect,
                Latency(),
                scenario.Title + ": decisión " + (index + 1) + " (elección múltiple)");
        }

        private float Latency()
        {
            return (float)Math.Round(2.0 + _random.NextDouble() * 18.0, 1);
        }
    }
}
