using System;

namespace Scorm.Scenarios.Demo
{
    /// <summary>Spanish texts for the tracker states and errors (the template itself only produces English technical messages).</summary>
    public static class DemoTexts
    {
        public static string RestoreSource(ScenarioRestoreSource source)
        {
            switch (source)
            {
                case ScenarioRestoreSource.SuspendData: return "datos guardados (suspend_data)";
                case ScenarioRestoreSource.Objectives: return "objetivos del LMS (reconstruido)";
                default: return "vacío (intento nuevo)";
            }
        }

        /// <summary>Spanish explanation of why cmi.suspend_data was ignored, or null if it was not.</summary>
        public static string RestoreIssue(ScenarioRestoreIssue issue)
        {
            switch (issue)
            {
                case ScenarioRestoreIssue.NotJson:
                    return "cmi.suspend_data no contiene datos de esta app (no es JSON): el estado se ha reconstruido desde los objetivos del LMS.";
                case ScenarioRestoreIssue.ParseError:
                    return "cmi.suspend_data no se pudo leer (JSON dañado): el estado se ha reconstruido desde los objetivos del LMS.";
                case ScenarioRestoreIssue.NoVersion:
                    return "cmi.suspend_data no indica versión de formato (otro formato o versión antigua): el estado se ha reconstruido desde los objetivos del LMS.";
                case ScenarioRestoreIssue.NewerVersion:
                    return "cmi.suspend_data es de una versión más nueva de la app: el estado se ha reconstruido desde los objetivos del LMS.";
                default:
                    return null;
            }
        }

        public static string Error(ScenarioTrackerError error)
        {
            switch (error)
            {
                case ScenarioTrackerError.InvalidCatalog: return "El catálogo de escenarios no es válido (revisa ids, rangos y pesos en la consola).";
                case ScenarioTrackerError.ScormNotInitialized: return "SCORM todavía no está inicializado (falta Scorm_Initialize_Complete).";
                case ScenarioTrackerError.NotInitialized: return "El seguimiento de escenarios no está inicializado.";
                case ScenarioTrackerError.SessionClosed: return "La sesión SCORM está cerrada. Vuelve a lanzar el SCO para continuar.";
                case ScenarioTrackerError.UnknownScenario: return "Ese escenario no está en el catálogo.";
                case ScenarioTrackerError.SuspendDataTooLarge: return "El estado guardado supera el límite de cmi.suspend_data (64000 caracteres).";
                default: return "Error del seguimiento de escenarios.";
            }
        }

        /// <summary>Spanish message for an exception thrown by a tracker call.</summary>
        public static string Exception(Exception e)
        {
            ScenarioTrackerException trackerException = e as ScenarioTrackerException;
            if (trackerException != null)
                return Error(trackerException.Error);
            if (e is ArgumentOutOfRangeException)
                return "La nota está fuera del rango del escenario.";
            if (e is ArgumentException)
                return "Datos de la decisión no válidos (id vacío o con espacios, o respuesta con formato incorrecto).";
            return "Error inesperado: revisa la consola.";
        }
    }
}
