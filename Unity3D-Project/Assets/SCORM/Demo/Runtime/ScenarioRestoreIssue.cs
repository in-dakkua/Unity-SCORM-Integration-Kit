namespace Scorm.Scenarios
{
    /// <summary>Why cmi.suspend_data was ignored on Initialize (see <see cref="ScenarioTracker.RestoreWarning"/>).</summary>
    public enum ScenarioRestoreIssue
    {
        None,
        /// <summary>Not a JSON object (e.g. data of another SCO or of the LMS seed).</summary>
        NotJson,
        /// <summary>Starts like JSON but cannot be parsed.</summary>
        ParseError,
        /// <summary>JSON without the "v" format version (another format or an older app).</summary>
        NoVersion,
        /// <summary>Format version newer than this build understands.</summary>
        NewerVersion
    }
}
