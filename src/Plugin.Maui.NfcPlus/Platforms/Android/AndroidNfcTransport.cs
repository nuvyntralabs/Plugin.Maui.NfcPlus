#if ANDROID
using Android.App;
using Android.Content;
using Android.Nfc;
using Android.Nfc.Tech;
using Android.OS;
using AndroidApp = Android.App.Application;
using AndroidNdefMessage = Android.Nfc.NdefMessage;
using AndroidNdefRecord = Android.Nfc.NdefRecord;
using AndroidUri = Android.Net.Uri;
using MauiPlatform = Microsoft.Maui.ApplicationModel.Platform;

namespace Plugin.Maui.NfcPlus;

sealed class AndroidNfcTransport : INfcTransport
{
    static AndroidNfcTransport? _active;

    readonly ReaderCallback _readerCallback;
    readonly AdapterReceiver _receiver;
    readonly LifecycleCallbacks _lifecycle;
    readonly NfcAdapter? _adapter;
    readonly object _gate = new();

    NfcListenRequest? _request;
    Activity? _boundActivity;
    bool _listening;
    bool _disposed;

    public AndroidNfcTransport()
    {
        _adapter = NfcAdapter.GetDefaultAdapter(MauiPlatform.AppContext);
        _readerCallback = new ReaderCallback(this);
        _receiver = new AdapterReceiver(this);
        _lifecycle = new LifecycleCallbacks(this);

        var filter = new IntentFilter(NfcAdapter.ActionAdapterStateChanged);
        MauiPlatform.AppContext.RegisterReceiver(_receiver, filter);
        if (MauiPlatform.AppContext is AndroidApp app)
            app.RegisterActivityLifecycleCallbacks(_lifecycle);
    }

    public bool IsSupported => _adapter is not null;

    public NfcPlatformInfo Platform => NfcPlatformInfo.Android;

    public event EventHandler<NfcAvailabilityChangedEventArgs>? AvailabilityChanged;

    public event EventHandler<NfcSessionChangedEventArgs>? NativeSessionEnded;

    public NfcAvailability GetAvailability()
    {
        if (_adapter is null)
            return NfcAvailability.Unsupported;
        return _adapter.IsEnabled ? NfcAvailability.Available : NfcAvailability.Disabled;
    }

    public Task StartAsync(NfcListenRequest request, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (_adapter is null)
            throw new NfcPlusException(NfcPlusError.NotSupported, "This device does not have an NFC adapter.");
        if (!_adapter.IsEnabled)
            throw new NfcPlusException(NfcPlusError.Unavailable, "NFC is turned off.");

        var activity = MauiPlatform.CurrentActivity
            ?? throw new NfcPlusException(
                NfcPlusError.InvalidOperation,
                "No current Android activity. Call StartAsync or ReadAsync after the UI is visible.");

        lock (_gate)
        {
            _request = request;
            _listening = true;
            _active = this;
        }

        EnableOn(activity, request);
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        Activity? activity;
        lock (_gate)
        {
            _listening = false;
            _request = null;
            activity = _boundActivity;
            if (ReferenceEquals(_active, this))
                _active = null;
        }

        DisableOn(activity ?? MauiPlatform.CurrentActivity);
        return Task.CompletedTask;
    }

    public Task OpenSettingsAsync()
    {
        var intent = new Intent(Android.Provider.Settings.ActionNfcSettings);
        intent.AddFlags(ActivityFlags.NewTask);
        MauiPlatform.AppContext.StartActivity(intent);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _ = StopAsync();
        try
        {
            MauiPlatform.AppContext.UnregisterReceiver(_receiver);
        }
        catch (Java.Lang.IllegalArgumentException)
        {
        }

        if (MauiPlatform.AppContext is AndroidApp app)
            app.UnregisterActivityLifecycleCallbacks(_lifecycle);

        _receiver.Dispose();
        _lifecycle.Dispose();
        _readerCallback.Dispose();
    }

    internal static bool TryHandleIntent(object? platformIntent)
    {
        if (platformIntent is not Intent intent)
            return false;
        return _active?.HandleIntent(intent) == true;
    }

    internal bool HandleIntent(Intent intent)
    {
        var tag = ReadTagExtra(intent);
        if (tag is null)
            return false;

        OnTagDiscovered(tag);
        return true;
    }

    internal void OnActivityResumed(Activity activity)
    {
        NfcListenRequest? request;
        lock (_gate)
        {
            if (!_listening || _request is null)
                return;
            request = _request;
        }

        EnableOn(activity, request);
    }

    internal void OnActivityPaused(Activity activity)
    {
        lock (_gate)
        {
            if (!_listening)
                return;
        }

        DisableOn(activity);
    }

    internal void OnAdapterStateChanged() =>
        AvailabilityChanged?.Invoke(this, new NfcAvailabilityChangedEventArgs(GetAvailability()));

    internal void OnTagDiscovered(Tag tag)
    {
        NfcListenRequest? request;
        lock (_gate)
            request = _request;

        if (request is null)
            return;

        try
        {
            var parsed = ReadTag(tag);
            parsed = ApplyPurpose(tag, parsed, request);
            request.OnTag(parsed);

            if (request.Options.InvalidateAfterFirstRead)
            {
                lock (_gate)
                    _listening = false;
                DisableOn(_boundActivity ?? MauiPlatform.CurrentActivity);
            }
        }
        catch (Exception ex)
        {
            request.OnFailed?.Invoke(ex is NfcPlusException ? ex : new NfcPlusException(NfcPlusError.ReadFailed, ex.Message, ex));
        }
    }

    void EnableOn(Activity activity, NfcListenRequest request)
    {
        if (_adapter is null)
            return;

        var mode = request.Options.AndroidListenMode ?? NfcAndroidListenMode.ReaderMode;
        if (mode == NfcAndroidListenMode.ForegroundDispatch)
            EnableForegroundDispatch(activity);
        else
            EnableReaderMode(activity, request);

        _boundActivity = activity;
    }

    void EnableReaderMode(Activity activity, NfcListenRequest request)
    {
        Bundle? extras = null;
        if (request.Options.PresenceCheckDelay is { } delay)
        {
            extras = new Bundle();
            extras.PutInt(NfcAdapter.ExtraReaderPresenceCheckDelay, (int)delay.TotalMilliseconds);
        }

        const NfcReaderFlags flags =
            NfcReaderFlags.NfcA |
            NfcReaderFlags.NfcB |
            NfcReaderFlags.NfcF |
            NfcReaderFlags.NfcV |
            NfcReaderFlags.NfcBarcode;

        _adapter!.EnableReaderMode(activity, _readerCallback, flags, extras);
    }

    void EnableForegroundDispatch(Activity activity)
    {
        var launch = new Intent(activity, activity.Class).AddFlags(ActivityFlags.SingleTop);
        var pendingFlags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
            pendingFlags |= PendingIntentFlags.Mutable;

        var pending = PendingIntent.GetActivity(activity, 0, launch, pendingFlags);
        var filters = new[]
        {
            CreateFilter(NfcAdapter.ActionNdefDiscovered),
            CreateFilter(NfcAdapter.ActionTechDiscovered),
            CreateFilter(NfcAdapter.ActionTagDiscovered)
        };

        _adapter!.EnableForegroundDispatch(activity, pending, filters, null);
    }

    void DisableOn(Activity? activity)
    {
        if (_adapter is null || activity is null)
            return;

        try
        {
            _adapter.DisableReaderMode(activity);
        }
        catch (Java.Lang.Exception)
        {
        }

        try
        {
            _adapter.DisableForegroundDispatch(activity);
        }
        catch (Java.Lang.Exception)
        {
        }
    }

    NfcTag ApplyPurpose(Tag tag, NfcTag parsed, NfcListenRequest request)
    {
        if (request.Purpose == NfcSessionPurpose.Write)
        {
            WriteTag(tag, request.WriteMessage ?? throw new NfcPlusException(NfcPlusError.InvalidOperation, "Write message is missing."), request.WriteOptions);
            parsed = ReadTag(tag);
        }
        else if (request.Purpose == NfcSessionPurpose.MakeReadOnly)
        {
            MakeReadOnly(tag);
            parsed = ReadTag(tag);
        }

        return parsed;
    }

    static NfcTag ReadTag(Tag tag)
    {
        var id = tag.GetId() ?? [];
        var technologies = MapTechnologies(tag.GetTechList());
        NdefMessage? message = null;
        var isNdef = false;
        var writable = false;
        var canLock = false;
        int? maxSize = null;

        var ndef = Ndef.Get(tag);
        if (ndef is not null)
        {
            isNdef = true;
            try
            {
                ndef.Connect();
                writable = ndef.IsWritable;
                maxSize = ndef.MaxSize;
                canLock = ndef.CanMakeReadOnly();
                message = FromAndroid(ndef.CachedNdefMessage ?? ndef.NdefMessage);
            }
            catch (TagLostException ex)
            {
                throw new NfcPlusException(NfcPlusError.TagLost, "The NFC tag left the field.", ex);
            }
            catch (Java.Lang.Exception)
            {
                // Empty or unreadable NDEF is still a valid tag snapshot.
            }
            finally
            {
                TryClose(ndef);
            }
        }
        else if (NdefFormatable.Get(tag) is not null)
        {
            writable = true;
            technologies = technologies.Contains(NfcTagTechnology.NdefFormatable)
                ? technologies
                : [.. technologies, NfcTagTechnology.NdefFormatable];
        }

        return new NfcTag(id, technologies, message, isNdef, writable, canLock, maxSize, DateTimeOffset.UtcNow);
    }

    static void WriteTag(Tag tag, NdefMessage message, NfcWriteOptions options)
    {
        var native = ToAndroid(message);
        var ndef = Ndef.Get(tag);
        if (ndef is not null)
        {
            try
            {
                ndef.Connect();
                if (!ndef.IsWritable)
                    throw new NfcPlusException(NfcPlusError.NotWritable, "This NFC tag is read-only.");

                var bytes = native.ToByteArray();
                if (bytes is not null && ndef.MaxSize < bytes.Length)
                {
                    throw new NfcPlusException(
                        NfcPlusError.MessageTooLarge,
                        $"NDEF message is {bytes.Length} bytes; the tag holds {ndef.MaxSize}.");
                }

                ndef.WriteNdefMessage(native);
                if (options.MakeReadOnly && ndef.CanMakeReadOnly() && !ndef.MakeReadOnly())
                    throw new NfcPlusException(NfcPlusError.WriteFailed, "The tag was written but could not be locked.");
            }
            catch (NfcPlusException)
            {
                throw;
            }
            catch (TagLostException ex)
            {
                throw new NfcPlusException(NfcPlusError.TagLost, "The NFC tag left the field during write.", ex);
            }
            catch (Java.Lang.Exception ex)
            {
                throw new NfcPlusException(NfcPlusError.WriteFailed, ex.Message ?? "NDEF write failed.", ex);
            }
            finally
            {
                TryClose(ndef);
            }

            return;
        }

        if (!options.FormatIfNeeded)
            throw new NfcPlusException(NfcPlusError.NotWritable, "The tag is not NDEF-formatted.");

        var formatable = NdefFormatable.Get(tag)
            ?? throw new NfcPlusException(NfcPlusError.NotWritable, "This tag cannot be formatted as NDEF.");

        try
        {
            formatable.Connect();
            if (options.MakeReadOnly)
                formatable.FormatReadOnly(native);
            else
                formatable.Format(native);
        }
        catch (NfcPlusException)
        {
            throw;
        }
        catch (TagLostException ex)
        {
            throw new NfcPlusException(NfcPlusError.TagLost, "The NFC tag left the field during format.", ex);
        }
        catch (Java.Lang.Exception ex)
        {
            throw new NfcPlusException(NfcPlusError.WriteFailed, ex.Message ?? "NDEF format failed.", ex);
        }
        finally
        {
            TryClose(formatable);
        }
    }

    static void MakeReadOnly(Tag tag)
    {
        var ndef = Ndef.Get(tag)
            ?? throw new NfcPlusException(NfcPlusError.NotWritable, "The tag is not NDEF-formatted.");

        try
        {
            ndef.Connect();
            if (!ndef.CanMakeReadOnly())
                throw new NfcPlusException(NfcPlusError.NotWritable, "This tag cannot be locked.");
            if (!ndef.MakeReadOnly())
                throw new NfcPlusException(NfcPlusError.WriteFailed, "The tag could not be locked.");
        }
        catch (NfcPlusException)
        {
            throw;
        }
        catch (TagLostException ex)
        {
            throw new NfcPlusException(NfcPlusError.TagLost, "The NFC tag left the field.", ex);
        }
        catch (Java.Lang.Exception ex)
        {
            throw new NfcPlusException(NfcPlusError.WriteFailed, ex.Message ?? "Lock failed.", ex);
        }
        finally
        {
            TryClose(ndef);
        }
    }

    static NdefMessage? FromAndroid(AndroidNdefMessage? message)
    {
        if (message?.GetRecords() is not { Length: > 0 } records)
            return null;

        var parsed = records.Select(record =>
            NdefCodec.Parse(
                (NdefTypeNameFormat)record.Tnf,
                record.GetTypeInfo() ?? [],
                record.GetPayload() ?? [],
                record.GetId() ?? [])).ToArray();

        return new NdefMessage(parsed);
    }

    static AndroidNdefMessage ToAndroid(NdefMessage message)
    {
        var records = message.Records.Select(ToAndroidRecord).ToArray();
        return new AndroidNdefMessage(records);
    }

    static AndroidNdefRecord ToAndroidRecord(NdefRecord record)
    {
        return record switch
        {
            NdefTextRecord text => AndroidNdefRecord.CreateTextRecord(text.Language, text.Text)!,
            NdefUriRecord uri => AndroidNdefRecord.CreateUri(AndroidUri.Parse(uri.Uri.OriginalString))!,
            NdefMimeRecord mime => AndroidNdefRecord.CreateMime(mime.MimeType, mime.Data)!,
            NdefExternalRecord ext => CreateExternal(ext),
            _ => new AndroidNdefRecord((short)record.TypeNameFormat, record.Type, record.Id, record.Payload)
        };
    }

    static AndroidNdefRecord CreateExternal(NdefExternalRecord record)
    {
        var parts = record.DomainType.Split(':', 2);
        var domain = parts[0];
        var type = parts.Length > 1 ? parts[1] : record.DomainType;
        return AndroidNdefRecord.CreateExternal(domain, type, record.Payload)!;
    }

    static IReadOnlyList<NfcTagTechnology> MapTechnologies(IList<string>? techList)
    {
        if (techList is null || techList.Count == 0)
            return [];

        var mapped = new List<NfcTagTechnology>();
        foreach (var tech in techList)
        {
            var value = tech switch
            {
                "android.nfc.tech.Ndef" => NfcTagTechnology.Ndef,
                "android.nfc.tech.NdefFormatable" => NfcTagTechnology.NdefFormatable,
                "android.nfc.tech.NfcA" => NfcTagTechnology.NfcA,
                "android.nfc.tech.NfcB" => NfcTagTechnology.NfcB,
                "android.nfc.tech.NfcF" => NfcTagTechnology.NfcF,
                "android.nfc.tech.NfcV" => NfcTagTechnology.NfcV,
                "android.nfc.tech.IsoDep" => NfcTagTechnology.IsoDep,
                "android.nfc.tech.MifareClassic" => NfcTagTechnology.MifareClassic,
                "android.nfc.tech.MifareUltralight" => NfcTagTechnology.MifareUltralight,
                "android.nfc.tech.NfcBarcode" => NfcTagTechnology.Barcode,
                _ => NfcTagTechnology.Unknown
            };

            if (!mapped.Contains(value))
                mapped.Add(value);
        }

        return mapped;
    }

    static Tag? ReadTagExtra(Intent intent)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
            return intent.GetParcelableExtra(NfcAdapter.ExtraTag, Java.Lang.Class.FromType(typeof(Tag))) as Tag;

#pragma warning disable CS0618
        return intent.GetParcelableExtra(NfcAdapter.ExtraTag) as Tag;
#pragma warning restore CS0618
    }

    static IntentFilter CreateFilter(string action)
    {
        var filter = new IntentFilter(action);
        filter.AddCategory(Intent.CategoryDefault);
        return filter;
    }

    static void TryClose(ITagTechnology tech)
    {
        try
        {
            if (tech.IsConnected)
                tech.Close();
        }
        catch (Java.Lang.Exception)
        {
        }
    }

    sealed class ReaderCallback : Java.Lang.Object, NfcAdapter.IReaderCallback
    {
        readonly AndroidNfcTransport _owner;

        public ReaderCallback(AndroidNfcTransport owner) => _owner = owner;

        public void OnTagDiscovered(Tag? tag)
        {
            if (tag is not null)
                _owner.OnTagDiscovered(tag);
        }
    }

    sealed class AdapterReceiver : BroadcastReceiver
    {
        readonly AndroidNfcTransport _owner;

        public AdapterReceiver(AndroidNfcTransport owner) => _owner = owner;

        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action == NfcAdapter.ActionAdapterStateChanged)
                _owner.OnAdapterStateChanged();
        }
    }

    sealed class LifecycleCallbacks : Java.Lang.Object, AndroidApp.IActivityLifecycleCallbacks
    {
        readonly AndroidNfcTransport _owner;

        public LifecycleCallbacks(AndroidNfcTransport owner) => _owner = owner;

        public void OnActivityResumed(Activity activity) => _owner.OnActivityResumed(activity);

        public void OnActivityPaused(Activity activity) => _owner.OnActivityPaused(activity);

        public void OnActivityCreated(Activity activity, Bundle? savedInstanceState)
        {
        }

        public void OnActivityDestroyed(Activity activity)
        {
        }

        public void OnActivitySaveInstanceState(Activity activity, Bundle outState)
        {
        }

        public void OnActivityStarted(Activity activity)
        {
        }

        public void OnActivityStopped(Activity activity)
        {
        }
    }
}
#endif
