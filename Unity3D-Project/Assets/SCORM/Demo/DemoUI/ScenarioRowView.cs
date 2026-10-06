using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Scorm.Scenarios.Demo
{
    /// <summary>One row of the scenario list: status, scores, attempts, random play and manual score.</summary>
    public class ScenarioRowView : MonoBehaviour
    {
        private static readonly Color NotStartedColor = new Color(0.20f, 0.22f, 0.26f, 1f);
        private static readonly Color InProgressColor = new Color(0.30f, 0.27f, 0.12f, 1f);
        private static readonly Color PassedColor = new Color(0.13f, 0.30f, 0.17f, 1f);
        private static readonly Color FailedColor = new Color(0.34f, 0.15f, 0.15f, 1f);

        [SerializeField] private Image _background;
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _statusText;
        [SerializeField] private Text _scoresText;
        [SerializeField] private Button _playButton;
        [SerializeField] private Slider _manualSlider;
        [SerializeField] private Text _manualValueText;
        [SerializeField] private Button _manualButton;

        private ScenarioProgress _scenario;
        private Action<ScenarioProgress> _onPlay;
        private Action<ScenarioProgress, float> _onManual;

        public void Bind(ScenarioProgress scenario, Action<ScenarioProgress> onPlay, Action<ScenarioProgress, float> onManual)
        {
            _scenario = scenario;
            _onPlay = onPlay;
            _onManual = onManual;

            ScenarioDefinition definition = scenario.Definition;
            _manualSlider.minValue = definition.MinScore;
            _manualSlider.maxValue = definition.MaxScore;
            _manualSlider.wholeNumbers = false;
            _manualSlider.value = ScenarioScoring.FromScaled(definition, definition.PassingScaledScore);

            _playButton.onClick.RemoveAllListeners();
            _playButton.onClick.AddListener(() => _onPlay?.Invoke(_scenario));
            _manualButton.onClick.RemoveAllListeners();
            _manualButton.onClick.AddListener(() => _onManual?.Invoke(_scenario, (float)Math.Round(_manualSlider.value, 1)));
            _manualSlider.onValueChanged.RemoveAllListeners();
            _manualSlider.onValueChanged.AddListener(_ => RefreshManualValue());

            Refresh(true);
        }

        public void Refresh(bool interactable)
        {
            if (_scenario == null)
                return;
            ScenarioDefinition definition = _scenario.Definition;
            _titleText.text = definition.Title + "  <color=#9aa4b2>(" + definition.Id + ", peso " + Format(definition.Weight) + ")</color>";
            _statusText.text = "Estado: <b>" + StatusLabel(_scenario) + "</b>   Intentos: " + _scenario.Attempts +
                "   Aprobado con: " + Format(ScenarioScoring.FromScaled(definition, definition.PassingScaledScore)) + "/" + Format(definition.MaxScore);
            _scoresText.text = "Última: " + Score(_scenario.LastRaw, definition) + "   Mejor: " + Score(_scenario.BestRaw, definition) +
                "   LMS: " + Score(_scenario.ReportedRaw, definition);
            _background.color = StatusColor(_scenario.Status);
            _playButton.interactable = interactable;
            _manualButton.interactable = interactable;
            _manualSlider.interactable = interactable;
            RefreshManualValue();
        }

        private void RefreshManualValue()
        {
            _manualValueText.text = Format((float)Math.Round(_manualSlider.value, 1));
        }

        public static string StatusLabel(ScenarioProgress scenario)
        {
            string label;
            switch (scenario.Status)
            {
                case ScenarioStatus.InProgress: label = "en curso"; break;
                case ScenarioStatus.Passed: label = "aprobado"; break;
                case ScenarioStatus.Failed: label = "suspendido"; break;
                default: label = "no iniciado"; break;
            }
            if (scenario.IsCompleted && scenario.IsAttemptInProgress)
                label += " (reintento en curso)";
            return label;
        }

        private static Color StatusColor(ScenarioStatus status)
        {
            switch (status)
            {
                case ScenarioStatus.InProgress: return InProgressColor;
                case ScenarioStatus.Passed: return PassedColor;
                case ScenarioStatus.Failed: return FailedColor;
                default: return NotStartedColor;
            }
        }

        private static string Score(float? raw, ScenarioDefinition definition)
        {
            return raw.HasValue ? Format(raw.Value) + "/" + Format(definition.MaxScore) : "—";
        }

        private static string Format(float value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }
    }
}
