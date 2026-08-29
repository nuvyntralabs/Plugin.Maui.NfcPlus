namespace Plugin.Maui.NfcPlus;

sealed class UnsupportedNfcTransport : INfcTransport
{
    public bool IsSupported => false;

    public NfcPlatformInfo Platform => NfcPlatformInfo.Unsupported;

    public event EventHandler<NfcAvailabilityChangedEventArgs>? AvailabilityChanged
    {
        add { }
        remove { }
    }

    public event EventHandler<NfcSessionChangedEventArgs>? NativeSessionEnded
    {
        add { }
        remove { }
    }

    public NfcAvailability GetAvailability() => NfcAvailability.Unsupported;

    public Task StartAsync(NfcListenRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        throw new NfcPlusException(
            NfcPlusError.NotSupported,
            "NFC is supported on Android and iOS. The net10.0 reference assembly is for tests; inject INfcTransport.");
    }

    public Task StopAsync() => Task.CompletedTask;

    public Task OpenSettingsAsync() =>
        throw new NfcPlusException(NfcPlusError.NotSupported, "NFC settings are not available on this target.");

    public void Dispose()
    {
    }
}
