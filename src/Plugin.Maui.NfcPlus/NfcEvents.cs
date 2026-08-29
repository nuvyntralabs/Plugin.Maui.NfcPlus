namespace Plugin.Maui.NfcPlus;

/// <summary>
/// Raised when a tag is read during an active session.
/// </summary>
public sealed class NfcTagDetectedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes event data.
    /// </summary>
    public NfcTagDetectedEventArgs(NfcTag tag) =>
        Tag = tag ?? throw new ArgumentNullException(nameof(tag));

    /// <summary>Gets the parsed tag.</summary>
    public NfcTag Tag { get; }
}

/// <summary>
/// Raised when the plugin session starts or stops.
/// </summary>
public sealed class NfcSessionChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes event data.
    /// </summary>
    public NfcSessionChangedEventArgs(NfcSessionState state, string? reason = null)
    {
        State = state;
        Reason = reason;
    }

    /// <summary>Gets the new session state.</summary>
    public NfcSessionState State { get; }

    /// <summary>Gets an optional reason (user cancel, timeout, stop).</summary>
    public string? Reason { get; }
}

/// <summary>
/// Raised when NFC hardware or radio availability changes.
/// </summary>
public sealed class NfcAvailabilityChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes event data.
    /// </summary>
    public NfcAvailabilityChangedEventArgs(NfcAvailability availability) =>
        Availability = availability;

    /// <summary>Gets the new availability.</summary>
    public NfcAvailability Availability { get; }
}
