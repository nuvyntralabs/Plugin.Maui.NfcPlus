namespace Plugin.Maui.NfcPlus;

internal interface INfcTransport : IDisposable
{
    bool IsSupported { get; }

    NfcPlatformInfo Platform { get; }

    NfcAvailability GetAvailability();

    event EventHandler<NfcAvailabilityChangedEventArgs>? AvailabilityChanged;

    event EventHandler<NfcSessionChangedEventArgs>? NativeSessionEnded;

    Task StartAsync(NfcListenRequest request, CancellationToken cancellationToken);

    Task StopAsync();

    Task OpenSettingsAsync();
}

internal sealed class NfcListenRequest
{
    public required NfcSessionPurpose Purpose { get; init; }

    public NdefMessage? WriteMessage { get; init; }

    public required NfcSessionOptions Options { get; init; }

    public required NfcWriteOptions WriteOptions { get; init; }

    public required Action<NfcTag> OnTag { get; init; }

    public Action<Exception>? OnFailed { get; init; }
}
