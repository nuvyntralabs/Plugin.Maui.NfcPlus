namespace Plugin.Maui.NfcPlus;

/// <summary>
/// Lifecycle of the plugin-owned NFC session.
/// </summary>
public enum NfcSessionState
{
    /// <summary>No reader session or foreground dispatch is active.</summary>
    Idle = 0,

    /// <summary>Android reader mode / iOS system sheet is starting.</summary>
    Starting = 1,

    /// <summary>The session is listening for tags.</summary>
    Active = 2,

    /// <summary>A one-shot read, write, or lock is waiting for the next tag.</summary>
    AwaitingTag = 3,

    /// <summary>The session is shutting down.</summary>
    Stopping = 4
}
