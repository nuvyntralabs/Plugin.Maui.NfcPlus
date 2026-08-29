namespace Plugin.Maui.NfcPlus;

/// <summary>
/// Session-based NFC reader and NDEF writer for Android and iOS.
/// </summary>
public interface INfcPlus
{
    /// <summary>
    /// Gets a value indicating whether native NFC APIs exist on this target.
    /// Hardware can still be missing; use <see cref="Availability"/>.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Gets the current hardware / radio state.
    /// </summary>
    NfcAvailability Availability { get; }

    /// <summary>
    /// Gets the native stack used on this platform.
    /// </summary>
    NfcPlatformInfo Platform { get; }

    /// <summary>
    /// Gets the plugin session lifecycle.
    /// </summary>
    NfcSessionState SessionState { get; }

    /// <summary>
    /// Gets a value indicating whether a native session is running.
    /// </summary>
    bool IsSessionActive { get; }

    /// <summary>
    /// Gets the most recently detected tag, if any.
    /// </summary>
    NfcTag? LastTag { get; }

    /// <summary>
    /// Gets a point-in-time availability and session payload.
    /// </summary>
    NfcSnapshot Snapshot { get; }

    /// <summary>
    /// Raised after a tag is read during an active session.
    /// </summary>
    event EventHandler<NfcTagDetectedEventArgs>? TagDetected;

    /// <summary>
    /// Raised when the session starts or stops.
    /// </summary>
    event EventHandler<NfcSessionChangedEventArgs>? SessionChanged;

    /// <summary>
    /// Raised when NFC is turned on or off (Android) or becomes restricted.
    /// </summary>
    event EventHandler<NfcAvailabilityChangedEventArgs>? AvailabilityChanged;

    /// <summary>
    /// Re-probes hardware and radio state.
    /// </summary>
    Task<NfcAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts foreground dispatch (Android) or prepares a CoreNFC session (iOS).
    /// Tags raise <see cref="TagDetected"/> until <see cref="StopAsync"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// await NfcPlus.StartAsync();
    /// var tag = await NfcPlus.ReadAsync();
    /// </code>
    /// </example>
    Task StartAsync(NfcSessionOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops reader mode / invalidates the iOS session.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Waits for the next tag. Starts a one-shot session when none is active.
    /// </summary>
    Task<NfcTag> ReadAsync(NfcReadOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes <paramref name="message"/> to the next tag presented.
    /// On iOS this is a dedicated writer session — do not expect to
    /// <c>ReadAsync</c> then <c>WriteAsync</c> the same presentation.
    /// </summary>
    Task<NfcTag> WriteAsync(NdefMessage message, NfcWriteOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a well-known text record to the next tag.
    /// </summary>
    Task<NfcTag> WriteTextAsync(string text, string language = "en", NfcWriteOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a well-known URI record to the next tag.
    /// </summary>
    Task<NfcTag> WriteUriAsync(Uri uri, NfcWriteOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a MIME record to the next tag.
    /// </summary>
    Task<NfcTag> WriteMimeAsync(string mimeType, byte[] payload, NfcWriteOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently locks the next tag so it cannot be rewritten.
    /// </summary>
    Task<NfcTag> MakeReadOnlyAsync(NfcWriteOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the system NFC settings screen when the platform provides one.
    /// </summary>
    Task OpenSettingsAsync();
}
