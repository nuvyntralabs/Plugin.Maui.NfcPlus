namespace Plugin.Maui.NfcPlus.Tests;

sealed class FakeNfcTransport : INfcTransport
{
    public bool IsSupported { get; set; } = true;

    public NfcPlatformInfo Platform { get; set; } = NfcPlatformInfo.Android;

    public NfcAvailability Availability { get; set; } = NfcAvailability.Available;

    public NfcListenRequest? LastRequest { get; private set; }

    public int StartCount { get; private set; }

    public int StopCount { get; private set; }

    public int OpenSettingsCount { get; private set; }

    public bool ThrowOnStart { get; set; }

    public event EventHandler<NfcAvailabilityChangedEventArgs>? AvailabilityChanged;

    public event EventHandler<NfcSessionChangedEventArgs>? NativeSessionEnded;

    public NfcAvailability GetAvailability() => Availability;

    public Task StartAsync(NfcListenRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ThrowOnStart)
            throw new NfcPlusException(NfcPlusError.SessionFailed, "Reader mode failed.");

        LastRequest = request;
        StartCount++;
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        StopCount++;
        return Task.CompletedTask;
    }

    public Task OpenSettingsAsync()
    {
        OpenSettingsCount++;
        return Task.CompletedTask;
    }

    public void Discover(NfcTag tag)
    {
        if (LastRequest?.Purpose == NfcSessionPurpose.Write && LastRequest.WriteMessage is { } message)
        {
            tag = new NfcTag(
                tag.Id,
                tag.Technologies,
                message,
                true,
                !LastRequest.WriteOptions.MakeReadOnly,
                tag.CanMakeReadOnly,
                tag.MaxNdefSize,
                DateTimeOffset.UtcNow);
        }
        else if (LastRequest?.Purpose == NfcSessionPurpose.MakeReadOnly)
        {
            tag = new NfcTag(
                tag.Id,
                tag.Technologies,
                tag.Message,
                tag.IsNdef,
                false,
                false,
                tag.MaxNdefSize,
                DateTimeOffset.UtcNow);
        }

        LastRequest?.OnTag(tag);
    }

    public void Fail(Exception exception) => LastRequest?.OnFailed?.Invoke(exception);

    public void EndSession(string reason = "cancelled") =>
        NativeSessionEnded?.Invoke(this, new NfcSessionChangedEventArgs(NfcSessionState.Idle, reason));

    public void RaiseAvailability(NfcAvailability availability)
    {
        Availability = availability;
        AvailabilityChanged?.Invoke(this, new NfcAvailabilityChangedEventArgs(availability));
    }

    public void Dispose()
    {
    }
}

static class Harness
{
    public static (NfcPlusImplementation Nfc, FakeNfcTransport Transport) Create(
        Action<NfcPlusOptions>? configure = null,
        Action<FakeNfcTransport>? transport = null)
    {
        var options = new NfcPlusOptions { DefaultTimeout = TimeSpan.FromSeconds(2) };
        configure?.Invoke(options);
        var fake = new FakeNfcTransport();
        transport?.Invoke(fake);
        return (NfcPlus.Create(options, fake), fake);
    }

    public static NfcTag Tag(
        string idHex = "04A2B3C4",
        NdefMessage? message = null,
        bool writable = true)
    {
        var id = Convert.FromHexString(idHex);
        return new NfcTag(
            id,
            [NfcTagTechnology.Ndef, NfcTagTechnology.NfcA],
            message,
            message is not null,
            writable,
            writable,
            137,
            DateTimeOffset.UtcNow);
    }
}
