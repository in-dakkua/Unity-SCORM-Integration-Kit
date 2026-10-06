using System.Collections.Generic;
using UnityEngine;

namespace Scorm.Scenarios
{
    /// <summary>Ordered list of the scenarios of one SCO and the rules used to compute the global result.</summary>
    [CreateAssetMenu(menuName = "SCORM/Scenarios/Scenario Catalog", fileName = "ScenarioCatalog")]
    public class ScenarioCatalog : ScriptableObject
    {
        [SerializeField] private List<ScenarioDefinition> _scenarios = new List<ScenarioDefinition>();

        [Tooltip("Score reported to the LMS for a scenario played more than once.")]
        [SerializeField] private ScenarioScorePolicy _scorePolicy = ScenarioScorePolicy.Best;

        [SerializeField] private GlobalScoreMethod _globalScoreMethod = GlobalScoreMethod.WeightedAverageAllScenarios;

        [Tooltip("Global scaled score (0..1) needed for cmi.success_status = passed once every scenario is completed.")]
        [Range(0f, 1f)]
        [SerializeField] private float _globalPassingScaledScore = 0.7f;

        [Tooltip("If true, every scenario must also be passed for the SCO to be passed.")]
        [SerializeField] private bool _requireAllScenariosPassed;

        public IReadOnlyList<ScenarioDefinition> Scenarios => _scenarios;
        public ScenarioScorePolicy ScorePolicy => _scorePolicy;
        public GlobalScoreMethod GlobalScoreMethod => _globalScoreMethod;
        public float GlobalPassingScaledScore => _globalPassingScaledScore;
        public bool RequireAllScenariosPassed => _requireAllScenariosPassed;

        /// <summary>Sets every field at once (editor tooling and tests).</summary>
        public void Configure(IEnumerable<ScenarioDefinition> scenarios, ScenarioScorePolicy scorePolicy,
            GlobalScoreMethod globalScoreMethod, float globalPassingScaledScore, bool requireAllScenariosPassed)
        {
            _scenarios = new List<ScenarioDefinition>(scenarios);
            _scorePolicy = scorePolicy;
            _globalScoreMethod = globalScoreMethod;
            _globalPassingScaledScore = globalPassingScaledScore;
            _requireAllScenariosPassed = requireAllScenariosPassed;
        }

        public ScenarioDefinition Find(string id)
        {
            foreach (ScenarioDefinition scenario in _scenarios)
                if (scenario != null && scenario.Id == id)
                    return scenario;
            return null;
        }

        /// <summary>Configuration problems (empty/duplicated/whitespace ids, invalid ranges). Empty list = valid.</summary>
        public List<string> Validate()
        {
            List<string> errors = new List<string>();
            HashSet<string> ids = new HashSet<string>();
            if (_scenarios.Count == 0)
                errors.Add("The catalog has no scenarios.");
            for (int i = 0; i < _scenarios.Count; i++)
            {
                ScenarioDefinition s = _scenarios[i];
                if (s == null)
                {
                    errors.Add("Scenario #" + i + " is missing.");
                    continue;
                }
                if (string.IsNullOrEmpty(s.Id) || s.Id.Trim().Length != s.Id.Length || s.Id.IndexOf(' ') >= 0 || s.Id.IndexOf('\t') >= 0)
                    errors.Add("Scenario #" + i + " ('" + s.name + "') has an empty id or an id with whitespace.");
                else if (!ids.Add(s.Id))
                    errors.Add("Scenario id '" + s.Id + "' is duplicated.");
                if (s.MaxScore <= s.MinScore)
                    errors.Add("Scenario '" + s.Id + "': maxScore must be greater than minScore.");
                if (s.Weight < 0f)
                    errors.Add("Scenario '" + s.Id + "': weight must be >= 0.");
            }
            return errors;
        }
    }
}
