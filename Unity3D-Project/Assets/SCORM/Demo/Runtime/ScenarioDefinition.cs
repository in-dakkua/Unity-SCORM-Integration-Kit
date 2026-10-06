using UnityEngine;

namespace Scorm.Scenarios
{
    /// <summary>
    /// One training scenario. It is tracked in the LMS as cmi.objectives.n with <see cref="Id"/> as its id, so the id
    /// must never change once the package has been published (the LMS keys the stored score by it).
    /// </summary>
    [CreateAssetMenu(menuName = "SCORM/Scenarios/Scenario Definition", fileName = "ScenarioDefinition")]
    public class ScenarioDefinition : ScriptableObject
    {
        public const int MaxDescriptionLength = 250;

        [Tooltip("cmi.objectives.n.id. Stable, URI-like, without spaces (e.g. scenario-01). Never change it after publishing.")]
        [SerializeField] private string _id = "scenario-01";

        [SerializeField] private string _title = "Escenario";

        [TextArea(2, 5)]
        [Tooltip("Shown in the UI; the first 250 characters go to cmi.objectives.n.description.")]
        [SerializeField] private string _description = "";

        [Tooltip("Relative weight in the global score (weighted average).")]
        [Min(0f)]
        [SerializeField] private float _weight = 1f;

        [SerializeField] private float _minScore = 0f;

        [SerializeField] private float _maxScore = 100f;

        [Tooltip("Scaled score (0..1) needed to pass the scenario.")]
        [Range(0f, 1f)]
        [SerializeField] private float _passingScaledScore = 0.7f;

        [Tooltip("DEMO ONLY: number of decisions (cmi.interactions) the demo simulator generates per attempt. Ignored by the tracker.")]
        [Min(1)]
        [SerializeField] private int _simulatedDecisionCount = 4;

        public string Id => _id;
        public string Title => _title;
        public string Description => _description;
        public float Weight => _weight;
        public float MinScore => _minScore;
        public float MaxScore => _maxScore;
        public float PassingScaledScore => _passingScaledScore;
        public int SimulatedDecisionCount => _simulatedDecisionCount;

        /// <summary>Sets every field at once (editor tooling and tests).</summary>
        public void Configure(string id, string title, string description, float weight, float minScore, float maxScore,
            float passingScaledScore, int simulatedDecisionCount)
        {
            _id = id;
            _title = title;
            _description = description;
            _weight = weight;
            _minScore = minScore;
            _maxScore = maxScore;
            _passingScaledScore = passingScaledScore;
            _simulatedDecisionCount = simulatedDecisionCount;
        }

        /// <summary>Description cut to the SCORM limit (localized_string_type, SPM 250).</summary>
        public string ScormDescription
        {
            get
            {
                string text = string.IsNullOrEmpty(_description) ? _title : _description;
                if (string.IsNullOrEmpty(text))
                    return null;
                return text.Length <= MaxDescriptionLength ? text : text.Substring(0, MaxDescriptionLength);
            }
        }
    }
}
