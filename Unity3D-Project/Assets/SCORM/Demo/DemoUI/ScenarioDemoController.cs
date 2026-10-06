using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Scorm.Scenarios.Demo
{
    /// <summary>
    /// Demo screen: plays scenarios with random (or manual) results through the <see cref="ScenarioTracker"/> and shows
    /// the global result and every SCORM call. Only the tracker calls are meant to be copied to a real project.
    /// </summary>
    public class ScenarioDemoController : MonoBehaviour
    {
        [SerializeField] private ScenarioTrackerHost _host;
        [SerializeField] private ScenarioListPanel _scenarioList;
        [SerializeField] private SummaryPanel _summary;
        [SerializeField] private Text _messageText;
        [SerializeField] private Button _commitButton;
        [SerializeField] private Button _suspendButton;
        [SerializeField] private Button _finishButton;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _relaunchButton;
        [SerializeField] private InputField _seedInput;
        [SerializeField] private Button _applySeedButton;

        private ScenarioTracker _tracker;
        private ScenarioSimulator _simulator;

        private void Awake()
        {
            _simulator = new ScenarioSimulator(Environment.TickCount & 0x7fffffff);
            _commitButton.onClick.AddListener(OnCommit);
            _suspendButton.onClick.AddListener(OnSuspend);
            _finishButton.onClick.AddListener(OnFinish);
            _resetButton.onClick.AddListener(OnReset);
            _relaunchButton.onClick.AddListener(OnRelaunch);
            _applySeedButton.onClick.AddListener(OnApplySeed);
            _relaunchButton.gameObject.SetActive(Application.isEditor);
            _seedInput.text = _simulator.Seed.ToString(CultureInfo.InvariantCulture);
            _host.Ready += OnTrackerReady;
        }

        private void Start()
        {
            if (_host.Tracker != null)
                OnTrackerReady(_host.Tracker);
            else
                RefreshAll();
            if (_host.InitializationErrorKind.HasValue)
                ShowMessage(DemoTexts.Error(_host.InitializationErrorKind.Value), true);
        }

        private void OnDestroy()
        {
            if (_host != null)
                _host.Ready -= OnTrackerReady;
            if (_tracker != null)
                _tracker.Changed -= RefreshAll;
        }

        private void OnTrackerReady(ScenarioTracker tracker)
        {
            if (_tracker == tracker)
                return;
            if (_tracker != null)
                _tracker.Changed -= RefreshAll;
            _tracker = tracker;
            _tracker.Changed += RefreshAll;
            _scenarioList.Build(_tracker, OnPlayRandom, OnManualScore);
            RefreshAll();
            ShowMessage(ScormManager.GetEntry() == StudentRecord.EntryType.resume
                ? "Sesión reanudada: estado restaurado desde " + DemoTexts.RestoreSource(_tracker.RestoreSource) + "."
                : "Sesión iniciada. Juega escenarios con datos aleatorios o pon una nota manual.", false);
        }

        private void OnPlayRandom(ScenarioProgress scenario)
        {
            Run(() =>
            {
                SimulatedAttempt attempt = _simulator.Play(scenario.Definition);
                _tracker.BeginScenario(scenario.Id);
                int rejected = _tracker.LastOperationFailures.Count;
                foreach (SimulatedDecision decision in attempt.Decisions)
                {
                    _tracker.RecordDecision(scenario.Id, decision.Id, decision.Type, decision.Response, decision.Result,
                        decision.CorrectPattern, decision.LatencySeconds, decision.Description);
                    rejected += _tracker.LastOperationFailures.Count;
                }
                _tracker.CompleteScenario(scenario.Id, attempt.RawScore);
                rejected += _tracker.LastOperationFailures.Count;
                Report(scenario.Definition.Title + ": " + attempt.Decisions.Count + " decisiones, nota " + Format(attempt.RawScore) + " → " +
                    ScenarioRowView.StatusLabel(scenario) + ".", rejected);
            });
        }

        private void OnManualScore(ScenarioProgress scenario, float raw)
        {
            Run(() =>
            {
                _tracker.BeginScenario(scenario.Id);
                int rejected = _tracker.LastOperationFailures.Count;
                _tracker.CompleteScenario(scenario.Id, raw);
                rejected += _tracker.LastOperationFailures.Count;
                Report(scenario.Definition.Title + ": nota manual " + Format(raw) + " → " + ScenarioRowView.StatusLabel(scenario) + ".", rejected);
            });
        }

        private void OnCommit()
        {
            Run(() => Report(_tracker.Commit() ? "Commit correcto." : "Commit rechazado.", _tracker.LastOperationFailures.Count));
        }

        private void OnSuspend()
        {
            Run(() =>
            {
                bool ok = _tracker.Suspend();
                Report(ok ? "Salida con exit=suspend: el LMS guardará el intento para reanudarlo." :
                    _tracker.IsClosed ? "Salir (suspend) con errores." : "Salir (suspend) falló: la sesión sigue abierta, puedes reintentarlo.",
                    _tracker.LastOperationFailures.Count);
            });
        }

        private void OnFinish()
        {
            Run(() =>
            {
                bool ok = _tracker.Finish();
                Report(ok ? "Finalizado con exit=normal: el intento queda cerrado en el LMS." :
                    _tracker.IsClosed ? "Finalizar con errores." : "Finalizar falló: la sesión sigue abierta, puedes reintentarlo.",
                    _tracker.LastOperationFailures.Count);
            });
        }

        private void OnReset()
        {
            Run(() =>
            {
                _tracker.ResetLocalState();
                Report("Datos de demo reiniciados (cmi.suspend_data). Atención: el LMS mantiene los objetivos, interacciones y la nota global " +
                    "hasta que vuelvas a completar escenarios.", _tracker.LastOperationFailures.Count);
            });
        }

        // Editor backend only: a real LMS does not allow Initialize again after Terminate in the same page.
        private void OnRelaunch()
        {
            ScormManager.Initialize();
        }

        private void OnApplySeed()
        {
            if (int.TryParse(_seedInput.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed))
            {
                _simulator.Reseed(seed);
                ShowMessage("Semilla aleatoria: " + seed + " (las siguientes partidas son reproducibles).", false);
            }
            else
            {
                ShowMessage("La semilla debe ser un número entero.", true);
            }
        }

        private void Run(Action action)
        {
            if (_tracker == null)
            {
                ShowMessage("El tracker no está listo (¿falta Scorm_Initialize_Complete?).", true);
                return;
            }
            try
            {
                action();
            }
            catch (ScenarioTrackerException e)
            {
                Debug.LogWarning("[ScenarioDemo] " + e.Message);
                ShowMessage(DemoTexts.Exception(e), true);
            }
            catch (ArgumentException e)
            {
                Debug.LogWarning("[ScenarioDemo] " + e.Message);
                ShowMessage(DemoTexts.Exception(e), true);
            }
            RefreshAll();
        }

        private void Report(string text, int rejectedCalls)
        {
            if (rejectedCalls > 0)
                ShowMessage(text + " El LMS rechazó " + rejectedCalls + " llamada(s): revisa el log.", true);
            else
                ShowMessage(text, false);
        }

        private void RefreshAll()
        {
            bool open = _tracker != null && !_tracker.IsClosed;
            _scenarioList.Refresh(open);
            _summary.Refresh(_tracker);
            _commitButton.interactable = open;
            _suspendButton.interactable = open;
            _finishButton.interactable = open;
            _resetButton.interactable = open;
            _relaunchButton.interactable = _tracker != null && _tracker.IsClosed;
        }

        private void ShowMessage(string text, bool isError)
        {
            _messageText.text = isError ? "<color=#ff8080>" + text + "</color>" : text;
            if (isError)
                Debug.LogWarning("[ScenarioDemo] " + text);
        }

        private static string Format(float value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }
    }
}
