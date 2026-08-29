namespace Plugin.Maui.NfcPlus;

/// <summary>
/// Point-in-time availability, session, and last-tag payload.
/// </summary>
/// <param name="CapturedAt">When the snapshot was taken (UTC).</param>
/// <param name="Availability">Hardware / radio state.</param>
/// <param name="SessionState">Plugin session lifecycle.</param>
/// <param name="IsSessionActive">Whether a native session is running.</param>
/// <param name="LastTag">Most recently detected tag, if any.</param>
/// <param name="Platform">Native stack description.</param>
public sealed record NfcSnapshot(
    DateTimeOffset CapturedAt,
    NfcAvailability Availability,
    NfcSessionState SessionState,
    bool IsSessionActive,
    NfcTag? LastTag,
    NfcPlatformInfo Platform);
