namespace SmoothOperator.Domain.Enums
{
    /// <summary>
    /// Per-connection override of the parent vault's recording flag.
    /// <c>Inherit</c> defers to <c>ConnectionGroup.RecordingEnabled</c>.
    /// </summary>
    public enum RecordingOverride
    {
        Inherit = 0,
        ForceOn = 1,
        ForceOff = 2,
    }
}
