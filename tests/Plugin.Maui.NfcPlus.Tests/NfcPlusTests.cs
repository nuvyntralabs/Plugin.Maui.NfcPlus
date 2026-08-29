using Microsoft.Extensions.DependencyInjection;

namespace Plugin.Maui.NfcPlus.Tests;

public sealed class NfcPlusTests
{
    [Fact]
    public async Task Start_then_Read_returns_detected_tag()
    {
        var (nfc, transport) = Harness.Create();

        await nfc.StartAsync();
        var read = nfc.ReadAsync();
        transport.Discover(Harness.Tag("04AABBCC", NdefMessage.FromText("SKU-1042")));

        var tag = await read;

        Assert.Equal("04AABBCC", tag.IdHex);
        Assert.Equal("SKU-1042", tag.Text);
        Assert.Equal(NfcSessionState.Active, nfc.SessionState);
        Assert.True(nfc.IsSessionActive);
        Assert.Same(tag, nfc.LastTag);
    }

    [Fact]
    public async Task ReadAsync_without_session_is_one_shot()
    {
        var (nfc, transport) = Harness.Create();
        var read = nfc.ReadAsync(new NfcReadOptions { Timeout = TimeSpan.FromSeconds(2) });
        transport.Discover(Harness.Tag(message: NdefMessage.FromUri("https://shop.example.com/p/9")));

        var tag = await read;

        Assert.Equal("https://shop.example.com/p/9", tag.Uri?.OriginalString);
        Assert.Equal(NfcSessionState.Idle, nfc.SessionState);
        Assert.True(transport.StopCount >= 1);
    }

    [Fact]
    public async Task WriteTextAsync_sends_ndef_text_on_next_tag()
    {
        var (nfc, transport) = Harness.Create();
        var write = nfc.WriteTextAsync("EMP-204");
        Assert.Equal(NfcSessionPurpose.Write, transport.LastRequest?.Purpose);

        transport.Discover(Harness.Tag("01020304"));
        var tag = await write;

        Assert.Equal("EMP-204", tag.Text);
        Assert.Equal("01020304", tag.IdHex);
    }

    [Fact]
    public async Task WriteUriAsync_and_WriteMimeAsync_build_typed_messages()
    {
        var (nfc, transport) = Harness.Create();

        var uriWrite = nfc.WriteUriAsync(new Uri("mauiessentials://vehicle/VIN-88"));
        transport.Discover(Harness.Tag());
        Assert.Equal("mauiessentials://vehicle/VIN-88", (await uriWrite).Uri?.OriginalString);

        var mimeWrite = nfc.WriteMimeAsync("application/json", """{"ok":true}"""u8.ToArray());
        transport.Discover(Harness.Tag());
        Assert.Equal("application/json", (await mimeWrite).Mime?.MimeType);
    }

    [Fact]
    public async Task MakeReadOnlyAsync_marks_tag_not_writable()
    {
        var (nfc, transport) = Harness.Create();
        var lockTask = nfc.MakeReadOnlyAsync();
        Assert.Equal(NfcSessionPurpose.MakeReadOnly, transport.LastRequest?.Purpose);

        transport.Discover(Harness.Tag(message: NdefMessage.FromText("EQ-12")));
        var tag = await lockTask;

        Assert.False(tag.IsWritable);
        Assert.Equal("EQ-12", tag.Text);
    }

    [Fact]
    public async Task TagDetected_fires_during_listen()
    {
        var (nfc, transport) = Harness.Create();
        NfcTag? seen = null;
        nfc.TagDetected += (_, e) => seen = e.Tag;

        await nfc.StartAsync(new NfcSessionOptions { AlertMessage = "Hold the badge" });
        transport.Discover(Harness.Tag("AA", NdefMessage.FromText("badge")));

        Assert.Equal("badge", seen?.Text);
        Assert.Equal("Hold the badge", transport.LastRequest?.Options.AlertMessage);
        Assert.False(transport.LastRequest!.Options.InvalidateAfterFirstRead);
    }

    [Fact]
    public async Task ReadAsync_times_out_when_no_tag_arrives()
    {
        var (nfc, _) = Harness.Create();

        var error = await Assert.ThrowsAsync<NfcPlusException>(() =>
            nfc.ReadAsync(new NfcReadOptions { Timeout = TimeSpan.FromMilliseconds(40) }));

        Assert.Equal(NfcPlusError.Timeout, error.Error);
    }

    [Fact]
    public async Task Concurrent_read_is_rejected()
    {
        var (nfc, _) = Harness.Create();
        var first = nfc.ReadAsync();

        var error = await Assert.ThrowsAsync<NfcPlusException>(() => nfc.ReadAsync());
        Assert.Equal(NfcPlusError.InvalidOperation, error.Error);

        await nfc.StopAsync();
        await Assert.ThrowsAsync<NfcPlusException>(() => first);
    }

    [Fact]
    public async Task Disabled_adapter_throws_unavailable()
    {
        var (nfc, _) = Harness.Create(transport: t => t.Availability = NfcAvailability.Disabled);

        var error = await Assert.ThrowsAsync<NfcPlusException>(() => nfc.StartAsync());
        Assert.Equal(NfcPlusError.Unavailable, error.Error);
        Assert.Equal(NfcAvailability.Disabled, nfc.Availability);
    }

    [Fact]
    public async Task Unsupported_transport_throws()
    {
        var nfc = NfcPlus.Create(new NfcPlusOptions(), new UnsupportedNfcTransport());

        var error = await Assert.ThrowsAsync<NfcPlusException>(() => nfc.ReadAsync());
        Assert.Equal(NfcPlusError.NotSupported, error.Error);
        Assert.False(nfc.IsSupported);
        Assert.Equal(NfcAvailability.Unsupported, nfc.Availability);
    }

    [Fact]
    public void Create_without_transport_is_unsupported_on_net()
    {
        var nfc = NfcPlus.Create();
        Assert.False(nfc.IsSupported);
        Assert.Equal(NfcPlatformInfo.Unsupported, nfc.Platform);
    }

    [Fact]
    public async Task User_cancel_completes_pending_read_as_cancelled()
    {
        var (nfc, transport) = Harness.Create();
        var read = nfc.ReadAsync();
        transport.EndSession("cancelled");

        var error = await Assert.ThrowsAsync<NfcPlusException>(() => read);
        Assert.Equal(NfcPlusError.Cancelled, error.Error);
    }

    [Fact]
    public async Task OpenSettingsAsync_forwards_to_transport()
    {
        var (nfc, transport) = Harness.Create();
        await nfc.OpenSettingsAsync();
        Assert.Equal(1, transport.OpenSettingsCount);
    }

    [Fact]
    public async Task Snapshot_includes_last_tag_and_platform()
    {
        var (nfc, transport) = Harness.Create();
        await nfc.StartAsync();
        transport.Discover(Harness.Tag("DEADBEEF", NdefMessage.FromText("VIN-1")));

        var snapshot = nfc.Snapshot;
        Assert.Equal(NfcAvailability.Available, snapshot.Availability);
        Assert.Equal("DEADBEEF", snapshot.LastTag?.IdHex);
        Assert.True(snapshot.Platform.IsNative);
        Assert.Equal("VIN-1", snapshot.LastTag?.Text);
    }

    [Fact]
    public async Task AvailabilityChanged_is_forwarded()
    {
        var (nfc, transport) = Harness.Create();
        NfcAvailability? seen = null;
        nfc.AvailabilityChanged += (_, e) => seen = e.Availability;

        transport.RaiseAvailability(NfcAvailability.Disabled);

        Assert.Equal(NfcAvailability.Disabled, seen);
        Assert.Equal(NfcAvailability.Disabled, nfc.Availability);
    }

    [Fact]
    public async Task GetAvailabilityAsync_returns_transport_state()
    {
        var (nfc, transport) = Harness.Create(transport: t => t.Availability = NfcAvailability.Restricted);
        Assert.Equal(NfcAvailability.Restricted, await nfc.GetAvailabilityAsync());
        Assert.Equal(NfcAvailability.Restricted, transport.GetAvailability());
    }

    [Fact]
    public async Task AddNfcPlus_resolves_injected_transport()
    {
        var services = new ServiceCollection();
        services.AddSingleton<INfcTransport, FakeNfcTransport>();
        services.AddNfcPlus(options => options.DefaultAlertMessage = "Tap equipment");

        await using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<INfcPlus>();

        Assert.True(resolved.IsSupported);
        Assert.Same(resolved, NfcPlus.Current);
        Assert.Equal(NfcAvailability.Available, resolved.Availability);
    }

    [Fact]
    public void HandlePlatformIntent_is_false_on_net()
    {
        Assert.False(NfcPlus.HandlePlatformIntent(new object()));
    }

    [Fact]
    public void Tag_ToString_prefers_text_then_uri()
    {
        var text = Harness.Tag("AA", NdefMessage.FromText("asset-9"));
        Assert.Contains("asset-9", text.ToString(), StringComparison.Ordinal);

        var uri = Harness.Tag("BB", NdefMessage.FromUri("https://example.com"));
        Assert.Contains("https://example.com", uri.ToString(), StringComparison.Ordinal);
    }
}
