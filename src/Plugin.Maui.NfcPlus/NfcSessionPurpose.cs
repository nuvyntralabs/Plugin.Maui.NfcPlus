namespace Plugin.Maui.NfcPlus;

/// <summary>
/// What the current native session should do when a tag enters the field.
/// </summary>
public enum NfcSessionPurpose
{
    /// <summary>Read every tag and raise <see cref="INfcPlus.TagDetected"/>.</summary>
    Listen = 0,

    /// <summary>Read the next tag, then complete the pending <c>ReadAsync</c>.</summary>
    Read = 1,

    /// <summary>Write NDEF to the next tag.</summary>
    Write = 2,

    /// <summary>Lock the next tag so it cannot be rewritten.</summary>
    MakeReadOnly = 3
}
