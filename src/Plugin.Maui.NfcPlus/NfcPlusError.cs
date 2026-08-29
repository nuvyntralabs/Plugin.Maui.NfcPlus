namespace Plugin.Maui.NfcPlus;

/// <summary>
/// Classifies an <see cref="NfcPlusException"/>.
/// </summary>
public enum NfcPlusError
{
    /// <summary>The operation is not valid in the current session state.</summary>
    InvalidOperation = 0,

    /// <summary>NFC is not available on this target (including the net10.0 reference assembly).</summary>
    NotSupported = 1,

    /// <summary>Hardware is present but the radio is off, restricted, or not ready.</summary>
    Unavailable = 2,

    /// <summary>The user cancelled the system NFC sheet or the session was dismissed.</summary>
    Cancelled = 3,

    /// <summary>No tag was presented before the timeout.</summary>
    Timeout = 4,

    /// <summary>The tag left the field or the connection dropped mid-operation.</summary>
    TagLost = 5,

    /// <summary>The tag does not accept NDEF writes.</summary>
    NotWritable = 6,

    /// <summary>The NDEF payload is larger than the tag capacity.</summary>
    MessageTooLarge = 7,

    /// <summary>A native read failed after the tag was detected.</summary>
    ReadFailed = 8,

    /// <summary>A native write or format failed.</summary>
    WriteFailed = 9,

    /// <summary>The reader session could not start or was invalidated unexpectedly.</summary>
    SessionFailed = 10
}
