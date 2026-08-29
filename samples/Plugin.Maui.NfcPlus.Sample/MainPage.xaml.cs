using Plugin.Maui.NfcPlus;

namespace Plugin.Maui.NfcPlus.Sample;

public partial class MainPage : ContentPage
{
    readonly INfcPlus _nfc;

    public MainPage(INfcPlus nfc)
    {
        InitializeComponent();
        _nfc = nfc;
        _nfc.TagDetected += OnTagDetected;
        _nfc.SessionChanged += OnSessionChanged;
        _nfc.AvailabilityChanged += OnAvailabilityChanged;
        RefreshStatus();
    }

    async void OnStartClicked(object? sender, EventArgs e) =>
        await RunAsync(() => _nfc.StartAsync());

    async void OnReadClicked(object? sender, EventArgs e) =>
        await RunAsync(async () => ShowTag(await _nfc.ReadAsync()));

    async void OnStopClicked(object? sender, EventArgs e) =>
        await RunAsync(() => _nfc.StopAsync());

    async void OnWriteTextClicked(object? sender, EventArgs e) =>
        await RunAsync(async () => ShowTag(await _nfc.WriteTextAsync(PayloadOr("SKU-1042"))));

    async void OnWriteUriClicked(object? sender, EventArgs e)
    {
        var value = PayloadOr("https://shop.example.com/p/SKU-1042");
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            TagLabel.Text = "Enter an absolute URI to write.";
            return;
        }

        await RunAsync(async () => ShowTag(await _nfc.WriteUriAsync(uri)));
    }

    async void OnProductClicked(object? sender, EventArgs e) =>
        await WriteScenarioAsync("Retail · product → inventory", NdefMessage.FromUri("mauiessentials://product/SKU-1042"));

    async void OnEmployeeClicked(object? sender, EventArgs e) =>
        await WriteScenarioAsync("Attendance · badge → employee", NdefMessage.FromText("EMP-204"));

    async void OnAssetClicked(object? sender, EventArgs e) =>
        await WriteScenarioAsync("Asset · tag → equipment", NdefMessage.FromUri("mauiessentials://asset/EQ-88"));

    async void OnVehicleClicked(object? sender, EventArgs e) =>
        await WriteScenarioAsync("Vehicle · tag → inspection", NdefMessage.FromText("VIN-1HGCM82633A004352"));

    async void OnSettingsClicked(object? sender, EventArgs e) =>
        await RunAsync(() => _nfc.OpenSettingsAsync());

    async Task WriteScenarioAsync(string title, NdefMessage message)
    {
        SessionLabel.Text = title;
        await RunAsync(async () => ShowTag(await _nfc.WriteAsync(message)));
    }

    void OnTagDetected(object? sender, NfcTagDetectedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() => ShowTag(e.Tag));

    void OnSessionChanged(object? sender, NfcSessionChangedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(RefreshStatus);

    void OnAvailabilityChanged(object? sender, NfcAvailabilityChangedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(RefreshStatus);

    async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
            RefreshStatus();
        }
        catch (Exception ex)
        {
            TagLabel.Text = ex.Message;
            RefreshStatus();
        }
    }

    void ShowTag(NfcTag tag)
    {
        var lines = new List<string>
        {
            tag.ToString(),
            $"id={tag.IdHex}",
            $"ndef={tag.IsNdef}  writable={tag.IsWritable}  max={tag.MaxNdefSize?.ToString() ?? "n/a"}",
            $"tech={string.Join(", ", tag.Technologies)}"
        };

        if (tag.Text is not null)
            lines.Add($"text={tag.Text}");
        if (tag.Uri is not null)
            lines.Add($"uri={tag.Uri}");
        if (tag.Mime is not null)
            lines.Add($"mime={tag.Mime.MimeType} ({tag.Mime.Data.Length} bytes)");

        TagLabel.Text = string.Join(Environment.NewLine, lines);
        RefreshStatus();
    }

    void RefreshStatus()
    {
        var snapshot = _nfc.Snapshot;
        AvailabilityLabel.Text =
            $"Supported={_nfc.IsSupported}  Availability={snapshot.Availability}  Stack={snapshot.Platform.Stack}";
        SessionLabel.Text =
            $"Session={snapshot.SessionState}  active={snapshot.IsSessionActive}";
    }

    string PayloadOr(string fallback)
    {
        var value = PayloadEntry.Text?.Trim();
        return string.IsNullOrEmpty(value) ? fallback : value;
    }
}
