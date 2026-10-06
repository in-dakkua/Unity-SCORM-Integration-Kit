using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scorm.Scenarios.Demo
{
    /// <summary>Instantiates one <see cref="ScenarioRowView"/> per scenario of the tracker from an inactive template.</summary>
    public class ScenarioListPanel : MonoBehaviour
    {
        [SerializeField] private ScenarioRowView _rowTemplate;
        [SerializeField] private RectTransform _content;

        private readonly List<ScenarioRowView> _rows = new List<ScenarioRowView>();

        public void Build(ScenarioTracker tracker, Action<ScenarioProgress> onPlay, Action<ScenarioProgress, float> onManual)
        {
            foreach (ScenarioRowView row in _rows)
                if (row != null)
                    Destroy(row.gameObject);
            _rows.Clear();
            _rowTemplate.gameObject.SetActive(false);

            foreach (ScenarioProgress scenario in tracker.Scenarios)
            {
                ScenarioRowView row = Instantiate(_rowTemplate, _content);
                row.name = "Row " + scenario.Id;
                row.gameObject.SetActive(true);
                row.Bind(scenario, onPlay, onManual);
                _rows.Add(row);
            }
        }

        public void Refresh(bool interactable)
        {
            foreach (ScenarioRowView row in _rows)
                row.Refresh(interactable);
        }
    }
}
