namespace Plugin.Maui.NfcPlus;

/// <summary>
/// Entry point for the NFC plugin when dependency injection is not used.
/// </summary>
public static class NfcPlus
{
    static INfcPlus? _current;

    /// <summary>
    /// Gets the shared <see cref="INfcPlus"/> instance.
    /// </summary>
    public static INfcPlus Current => _current ??= Create(new NfcPlusOptions());

    /// <summary>
    /// Gets a value indicating whether native NFC APIs exist on this target.
    /// </summary>
    public static bool IsSupported => Current.IsSupported;

    /// <summary>
    /// Gets the current hardware / radio state.
    /// </summary>
    public static NfcAvailability Availability => Current.Availability;

    /// <summary>
    /// Raised after a tag is read during an active session.
    /// </summary>
    public static event EventHandler<NfcTagDetectedEventArgs>? TagDetected
    {
        add => Current.TagDetected += value;
        remove => Current.TagDetected -= value;
    }

    /// <summary>
    /// Raised when the session starts or stops.
    /// </summary>
    public static event EventHandler<NfcSessionChangedEventArgs>? SessionChanged
    {
        add => Current.SessionChanged += value;
        remove => Current.SessionChanged -= value;
    }

    /// <summary>
    /// Starts foreground dispatch (Android) or prepares a CoreNFC session (iOS).
    /// </summary>
    /// <example>
    /// <code>
    /// await NfcPlus.StartAsync();
    /// var tag = await NfcPlus.ReadAsync();
    /// </code>
    /// </example>
    public static Task StartAsync(NfcSessionOptions? options = null, CancellationToken cancellationToken = default) =>
        Current.StartAsync(options, cancellationToken);

    /// <summary>
    /// Stops the active session.
    /// </summary>
    public static Task StopAsync() => Current.StopAsync();

    /// <summary>
    /// Waits for the next tag.
    /// </summary>
    public static Task<NfcTag> ReadAsync(NfcReadOptions? options = null, CancellationToken cancellationToken = default) =>
        Current.ReadAsync(options, cancellationToken);

    /// <summary>
    /// Writes an NDEF message to the next tag.
    /// </summary>
    public static Task<NfcTag> WriteAsync(NdefMessage message, NfcWriteOptions? options = null, CancellationToken cancellationToken = default) =>
        Current.WriteAsync(message, options, cancellationToken);

    /// <summary>
    /// Writes a text record to the next tag.
    /// </summary>
    public static Task<NfcTag> WriteTextAsync(string text, string language = "en", NfcWriteOptions? options = null, CancellationToken cancellationToken = default) =>
        Current.WriteTextAsync(text, language, options, cancellationToken);

    /// <summary>
    /// Writes a URI record to the next tag.
    /// </summary>
    public static Task<NfcTag> WriteUriAsync(Uri uri, NfcWriteOptions? options = null, CancellationToken cancellationToken = default) =>
        Current.WriteUriAsync(uri, options, cancellationToken);

    /// <summary>
    /// Android foreground-dispatch hook. Call from <c>MainActivity.OnNewIntent</c>
    /// when <see cref="NfcAndroidListenMode.ForegroundDispatch"/> is used.
    /// No-op on other platforms.
    /// </summary>
    public static bool HandlePlatformIntent(object? platformIntent)
    {
#if ANDROID
        return AndroidNfcTransport.TryHandleIntent(platformIntent);
#else
        _ = platformIntent;
        return false;
#endif
    }

    /// <summary>
    /// Creates a new instance using the platform NFC transport.
    /// </summary>
    public static INfcPlus Create(NfcPlusOptions? options = null) =>
        new NfcPlusImplementation(options ?? new NfcPlusOptions(), CreatePlatform());

    /// <summary>
    /// Replaces the shared instance. Intended for tests and custom implementations.
    /// </summary>
    public static void SetDefault(INfcPlus implementation) =>
        _current = implementation ?? throw new ArgumentNullException(nameof(implementation));

    internal static NfcPlusImplementation Create(NfcPlusOptions options, INfcTransport transport) =>
        new(options, transport);

    internal static INfcTransport CreatePlatform()
    {
#if ANDROID
        return new AndroidNfcTransport();
#elif IOS
        return new IosNfcTransport();
#else
        return new UnsupportedNfcTransport();
#endif
    }
}
