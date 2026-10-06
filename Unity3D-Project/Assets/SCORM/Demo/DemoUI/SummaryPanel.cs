using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace Scorm.Scenarios.Demo
{
    /// <summary>Learner, global result, LMS status and connection type.</summary>
    public class SummaryPanel : MonoBehaviour
    {
        [SerializeField] private Text _learnerText;
        [SerializeField] private Text _globalText;
        [SerializeField] private Text _statusText;
        [SerializeField] private Text _sessionText;

        public void Refresh(ScenarioTracker tracker)
        {
            if (tracker == null)
            {
                _learnerText.text = "Alumno: —";
                _globalText.text = "Esperando a Scorm_Initialize_Complete…";
                _statusText.text = "";
                _sessionText.text = "Conexión: " + ConnectionLabel();
                return;
            }

            _learnerText.text = "Alumno: <b>" + Safe(ScormManager.GetLearnerName()) + "</b>  (id: " + Safe(ScormManager.GetLearnerId()) + ")";

            float? raw = tracker.GlobalRaw;
            _globalText.text = "Nota global (cmi.score.raw): <b>" + (raw.HasValue ? raw.Value.ToString("0.##", CultureInfo.InvariantCulture) + " / 100" : "—") +
                "</b>\nUmbral global: " + (tracker.Catalog.GlobalPassingScaledScore * 100f).ToString("0.#", CultureInfo.InvariantCulture) +
                "   Política: " + (tracker.Catalog.ScorePolicy == ScenarioScorePolicy.Best ? "mejor intento" : "último intento");

            _statusText.text = "success_status: <b>" + ScormManager.CustomTypeToString(tracker.GlobalSuccess) + "</b>   completion_status: <b>" +
                ScormManager.CustomTypeToString(tracker.GlobalCompletion) + "</b>\nProgreso: " + tracker.CompletedCount + "/" + tracker.Scenarios.Count +
                " (progress_measure " + tracker.ProgressMeasure.ToString("0.##", CultureInfo.InvariantCulture) + ")";

            string entry = ScormManager.GetEntry() == StudentRecord.EntryType.resume ? "resume (reanudación)" :
                ScormManager.GetEntry() == StudentRecord.EntryType.start ? "ab-initio (intento nuevo)" : "—";
            string session = "Entrada: " + entry + "   Estado cargado de: " + DemoTexts.RestoreSource(tracker.RestoreSource) +
                "\nConexión: " + ConnectionLabel();
            if (tracker.IsClosed)
                session += "\n<color=#ffcc66>Sesión SCORM cerrada (Terminate). Vuelve a lanzar el SCO para continuar.</color>";
            string issue = DemoTexts.RestoreIssue(tracker.RestoreIssue);
            if (issue != null)
                session += "\n<color=#ffcc66>" + issue + "</color>";
            _sessionText.text = session;
        }

        public static string ConnectionLabel()
        {
            if (Application.isEditor)
                return "Editor (backend SCORM en memoria)";
            if (!ScormManager.IsLmsConnected)
                return "<color=#ff6666>Sin LMS (no se encontró API_1484_11)</color>";
            // Same test as scorm.js isSimulatorRequested(): scormsim=1 as a whole query parameter.
            string url = Application.absoluteURL ?? "";
            int query = url.IndexOf('?');
            int hash = url.IndexOf('#');
            string search = query < 0 ? "" : (hash > query ? url.Substring(query + 1, hash - query - 1) : url.Substring(query + 1));
            if (Regex.IsMatch(search, "(^|[?&])scormsim=1(&|$)"))
                return "Simulador del navegador (?scormsim=1)";
            return "LMS real (SCORM 2004)";
        }

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "—" : value;
        }
    }
}
