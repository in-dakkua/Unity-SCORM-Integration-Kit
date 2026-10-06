using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Scorm.Scenarios.Demo
{
    /// <summary>
    /// On-screen log of every SCORM API call (ScormManager.ScormCall), also sent to Debug.Log (browser console in WebGL).
    /// </summary>
    /// <remarks>
    /// The event handler only queues the call (it must not call the SCORM API nor touch the UI hierarchy heavily);
    /// lines are rebuilt in LateUpdate from a pool, with a cap so a long session does not slow the UI down.
    /// </remarks>
    public class ScormLogPanel : MonoBehaviour
    {
        private const int MaxStoredCalls = 2000;
        private const int MaxValueLength = 80;

        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private RectTransform _content;
        [SerializeField] private Text _lineTemplate;
        [SerializeField] private Toggle _errorsOnlyToggle;
        [SerializeField] private Toggle _setsOnlyToggle;
        [SerializeField] private Button _clearButton;
        [SerializeField] private Text _counterText;
        [SerializeField] private int _maxVisibleLines = 250;
        [SerializeField] private bool _logToConsole = true;

        private readonly List<ScormCallInfo> _calls = new List<ScormCallInfo>();
        private readonly List<Text> _lines = new List<Text>();
        private int _totalCalls;
        private int _errorCount;
        private bool _dirty = true;

        private void Awake()
        {
            _lineTemplate.gameObject.SetActive(false);
            _errorsOnlyToggle.onValueChanged.AddListener(_ => _dirty = true);
            _setsOnlyToggle.onValueChanged.AddListener(_ => _dirty = true);
            _clearButton.onClick.AddListener(Clear);
        }

        private void OnEnable()
        {
            ScormManager.ScormCall += OnScormCall;
        }

        private void OnDisable()
        {
            ScormManager.ScormCall -= OnScormCall;
        }

        public void Clear()
        {
            _calls.Clear();
            _totalCalls = 0;
            _errorCount = 0;
            _dirty = true;
        }

        private void OnScormCall(ScormCallInfo info)
        {
            _calls.Add(info);
            if (_calls.Count > MaxStoredCalls)
                _calls.RemoveRange(0, _calls.Count - MaxStoredCalls);
            _totalCalls++;
            if (IsError(info))
                _errorCount++;
            _dirty = true;

            if (!_logToConsole)
                return;
            if (IsError(info))
                Debug.LogWarning("[SCORM] " + info);
            else
                Debug.Log("[SCORM] " + info);
        }

        private void LateUpdate()
        {
            if (!_dirty)
                return;
            _dirty = false;
            Render();
        }

        private void Render()
        {
            bool errorsOnly = _errorsOnlyToggle.isOn;
            bool setsOnly = _setsOnlyToggle.isOn;
            List<ScormCallInfo> visible = new List<ScormCallInfo>();
            for (int i = _calls.Count - 1; i >= 0 && visible.Count < _maxVisibleLines; i--)
            {
                ScormCallInfo call = _calls[i];
                if (errorsOnly && !IsError(call))
                    continue;
                if (setsOnly && call.Operation != ScormCallOperation.SetValue)
                    continue;
                visible.Add(call);
            }
            visible.Reverse();

            while (_lines.Count < visible.Count)
            {
                Text line = Instantiate(_lineTemplate, _content);
                line.name = "Line";
                _lines.Add(line);
            }
            for (int i = 0; i < _lines.Count; i++)
            {
                bool used = i < visible.Count;
                if (_lines[i].gameObject.activeSelf != used)
                    _lines[i].gameObject.SetActive(used);
                if (used)
                    _lines[i].text = Format(visible[i]);
            }

            _counterText.text = "Llamadas: " + _totalCalls + "   Errores: " + (_errorCount > 0 ? "<color=#ff6666>" + _errorCount + "</color>" : "0") +
                "   Mostrando: " + visible.Count;
            Canvas.ForceUpdateCanvases();
            _scrollRect.verticalNormalizedPosition = 0f;
        }

        private static bool IsError(ScormCallInfo call)
        {
            return !call.Succeeded || call.ErrorCode != 0;
        }

        private static string Format(ScormCallInfo call)
        {
            string text = call.Time.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + "  " + call.Operation;
            if (call.Element.Length > 0)
                text += "  " + call.Element;
            if (call.Operation == ScormCallOperation.SetValue)
                text += " = \"" + Escape(Truncate(call.Value)) + "\"";
            else if (call.Operation == ScormCallOperation.GetValue)
                text += " -> \"" + Escape(Truncate(call.Result)) + "\"";
            else
                text += " -> " + call.Result;
            if (IsError(call))
                text = "<color=#ff6666>" + text + "  [error " + call.ErrorCode + (call.ErrorDescription.Length > 0 ? ": " + Escape(call.ErrorDescription) : "") + "]</color>";
            else if (call.Operation == ScormCallOperation.SetValue)
                text = "<color=#9fe0a8>" + text + "</color>";
            return text;
        }

        private static string Truncate(string value)
        {
            if (value == null)
                return "";
            return value.Length <= MaxValueLength ? value : value.Substring(0, MaxValueLength) + "… (" + value.Length + ")";
        }

        // Rich text is enabled on the lines: keep values such as "<b>" from being interpreted as tags.
        private static string Escape(string value)
        {
            return value.Replace('<', '‹');
        }
    }
}
