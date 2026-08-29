#if IOS
using CoreFoundation;
using CoreNFC;
using Foundation;
using UIKit;

namespace Plugin.Maui.NfcPlus;

sealed class IosNfcTransport : INfcTransport
{
    readonly SessionDelegate _delegate;
    readonly object _gate = new();

    NFCNdefReaderSession? _session;
    NfcListenRequest? _request;
    bool _disposed;

    public IosNfcTransport()
    {
        _delegate = new SessionDelegate(this);
    }

    public bool IsSupported => NFCReaderSession.ReadingAvailable;

    public NfcPlatformInfo Platform => NfcPlatformInfo.iOS;

    public event EventHandler<NfcAvailabilityChangedEventArgs>? AvailabilityChanged
    {
        add { }
        remove { }
    }

    public event EventHandler<NfcSessionChangedEventArgs>? NativeSessionEnded;

    public NfcAvailability GetAvailability() =>
        NFCReaderSession.ReadingAvailable ? NfcAvailability.Available : NfcAvailability.Unsupported;

    public Task StartAsync(NfcListenRequest request, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!NFCReaderSession.ReadingAvailable)
            throw new NfcPlusException(NfcPlusError.NotSupported, "NFC reading is not available on this iPhone or simulator.");

        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            lock (_gate)
                _request = request;

            _session?.InvalidateSession();
            _session = new NFCNdefReaderSession(_delegate, DispatchQueue.MainQueue, request.Options.InvalidateAfterFirstRead)
            {
                AlertMessage = request.Options.AlertMessage ?? "Hold your iPhone near the NFC tag"
            };
            _session.BeginSession();
        });
    }

    public Task StopAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            lock (_gate)
                _request = null;

            _session?.InvalidateSession();
            _session = null;
        });
    }

    public Task OpenSettingsAsync()
    {
        var url = new NSUrl(UIApplication.OpenSettingsUrlString);
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (UIApplication.SharedApplication.CanOpenUrl(url))
                UIApplication.SharedApplication.OpenUrl(url, new UIApplicationOpenUrlOptions(), null);
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _session?.InvalidateSession();
        _session = null;
        _delegate.Dispose();
    }

    internal void HandleTags(NFCNdefReaderSession session, INFCNdefTag[] tags)
    {
        if (tags.Length == 0)
            return;

        var tag = tags[0];
        session.ConnectToTag(tag, error =>
        {
            if (error is not null)
            {
                Fail(MapError(error, NfcPlusError.TagLost));
                session.InvalidateSession(error.LocalizedDescription);
                return;
            }

            HandleConnected(session, tag);
        });
    }

    internal void HandleMessages(NFCNdefReaderSession session, NFCNdefMessage[] messages)
    {
        NfcListenRequest? request;
        lock (_gate)
            request = _request;

        if (request is null)
            return;

        if (request.Purpose is NfcSessionPurpose.Write or NfcSessionPurpose.MakeReadOnly)
            return;

        var parsed = new NfcTag(
            [],
            [NfcTagTechnology.Ndef],
            FromIos(messages.FirstOrDefault()),
            true,
            false,
            false,
            null,
            DateTimeOffset.UtcNow);
        Complete(session, request, parsed);
    }

    internal void HandleInvalidate(NFCNdefReaderSession session, NSError error)
    {
        _ = session;
        if (_session == session)
            _session = null;

        var code = (NFCReaderError)(long)error.Code;
        if (code is NFCReaderError.ReaderSessionInvalidationErrorFirstNDEFTagRead)
            return;

        var nfcError = code switch
        {
            NFCReaderError.ReaderSessionInvalidationErrorUserCanceled => NfcPlusError.Cancelled,
            NFCReaderError.ReaderSessionInvalidationErrorSessionTimeout => NfcPlusError.Timeout,
            NFCReaderError.UnsupportedFeature => NfcPlusError.NotSupported,
            _ => NfcPlusError.SessionFailed
        };

        var reason = code switch
        {
            NFCReaderError.ReaderSessionInvalidationErrorUserCanceled => "cancelled",
            NFCReaderError.ReaderSessionInvalidationErrorSessionTimeout => "timeout",
            _ => error.LocalizedDescription
        };

        Fail(new NfcPlusException(nfcError, error.LocalizedDescription ?? "The NFC session ended."));
        NativeSessionEnded?.Invoke(this, new NfcSessionChangedEventArgs(NfcSessionState.Idle, reason));
    }

    void HandleConnected(NFCNdefReaderSession session, INFCNdefTag ndef)
    {
        NfcListenRequest? request;
        lock (_gate)
            request = _request;

        if (request is null)
        {
            session.InvalidateSession();
            return;
        }

        var id = ReadIdentifier(ndef);
        var technologies = MapTechnologies(ndef);

        ndef.QueryNdefStatus((status, capacity, queryError) =>
        {
            if (queryError is not null)
            {
                Fail(MapError(queryError, NfcPlusError.ReadFailed));
                session.InvalidateSession(queryError.LocalizedDescription);
                return;
            }

            var writable = status == NFCNdefStatus.ReadWrite;
            var canLock = writable;
            var maxSize = (int)capacity;

            if (request.Purpose == NfcSessionPurpose.Write)
            {
                Write(session, ndef, request, id, technologies, writable, canLock, maxSize);
                return;
            }

            if (request.Purpose == NfcSessionPurpose.MakeReadOnly)
            {
                Lock(session, ndef, request, id, technologies, maxSize);
                return;
            }

            ndef.ReadNdef((message, readError) =>
            {
                if (readError is not null && !IsEmptyTag(readError))
                {
                    Fail(MapError(readError, NfcPlusError.ReadFailed));
                    session.InvalidateSession(readError.LocalizedDescription);
                    return;
                }

                var parsed = new NfcTag(
                    id,
                    technologies,
                    FromIos(message),
                    message is not null,
                    writable,
                    canLock,
                    maxSize,
                    DateTimeOffset.UtcNow);
                Complete(session, request, parsed);
            });
        });
    }

    void Write(
        NFCNdefReaderSession session,
        INFCNdefTag ndef,
        NfcListenRequest request,
        byte[] id,
        IReadOnlyList<NfcTagTechnology> technologies,
        bool writable,
        bool canLock,
        int maxSize)
    {
        if (!writable)
        {
            Fail(new NfcPlusException(NfcPlusError.NotWritable, "This NFC tag is read-only."));
            session.InvalidateSession("This tag is read-only.");
            return;
        }

        var native = ToIos(request.WriteMessage ?? throw new NfcPlusException(NfcPlusError.InvalidOperation, "Write message is missing."));
        ndef.WriteNdef(native, writeError =>
        {
            if (writeError is not null)
            {
                Fail(MapError(writeError, NfcPlusError.WriteFailed));
                session.InvalidateSession(writeError.LocalizedDescription);
                return;
            }

            if (request.WriteOptions.MakeReadOnly)
            {
                Lock(session, ndef, request, id, technologies, maxSize, request.WriteMessage);
                return;
            }

            var parsed = new NfcTag(id, technologies, request.WriteMessage, true, true, canLock, maxSize, DateTimeOffset.UtcNow);
            session.AlertMessage = "Written";
            Complete(session, request, parsed);
        });
    }

    void Lock(
        NFCNdefReaderSession session,
        INFCNdefTag ndef,
        NfcListenRequest request,
        byte[] id,
        IReadOnlyList<NfcTagTechnology> technologies,
        int maxSize,
        NdefMessage? written = null)
    {
        ndef.WriteLock(lockError =>
        {
            if (lockError is not null)
            {
                Fail(MapError(lockError, NfcPlusError.WriteFailed));
                session.InvalidateSession(lockError.LocalizedDescription);
                return;
            }

            var parsed = new NfcTag(id, technologies, written, written is not null, false, false, maxSize, DateTimeOffset.UtcNow);
            session.AlertMessage = "Locked";
            Complete(session, request, parsed);
        });
    }

    void Complete(NFCNdefReaderSession session, NfcListenRequest request, NfcTag tag)
    {
        request.OnTag(tag);
        if (request.Options.InvalidateAfterFirstRead || request.Purpose is not NfcSessionPurpose.Listen)
        {
            session.InvalidateSession();
            return;
        }

        session.RestartPolling();
    }

    void Fail(Exception exception)
    {
        NfcListenRequest? request;
        lock (_gate)
            request = _request;

        request?.OnFailed?.Invoke(exception);
    }

    static byte[] ReadIdentifier(INFCNdefTag tag)
    {
        NSData? data = tag switch
        {
            INFCMiFareTag mifare => mifare.Identifier,
            INFCIso15693Tag iso15693 => iso15693.Identifier,
            INFCIso7816Tag iso7816 => iso7816.Identifier,
            INFCFeliCaTag feliCa => feliCa.CurrentIdm,
            _ => null
        };

        return data is null ? [] : data.ToArray();
    }

    static IReadOnlyList<NfcTagTechnology> MapTechnologies(INFCNdefTag tag)
    {
        if (tag is INFCMiFareTag)
            return [NfcTagTechnology.Ndef, NfcTagTechnology.NfcA, NfcTagTechnology.MifareUltralight];
        if (tag is INFCIso15693Tag)
            return [NfcTagTechnology.Ndef, NfcTagTechnology.Iso15693, NfcTagTechnology.NfcV];
        if (tag is INFCIso7816Tag)
            return [NfcTagTechnology.Ndef, NfcTagTechnology.Iso7816, NfcTagTechnology.IsoDep];
        if (tag is INFCFeliCaTag)
            return [NfcTagTechnology.Ndef, NfcTagTechnology.FeliCa, NfcTagTechnology.NfcF];
        return [NfcTagTechnology.Ndef];
    }

    static NdefMessage? FromIos(NFCNdefMessage? message)
    {
        if (message?.Records is not { Length: > 0 } records)
            return null;

        var parsed = records.Select(record =>
            NdefCodec.Parse(
                MapTnf(record.TypeNameFormat),
                record.Type?.ToArray() ?? [],
                record.Payload?.ToArray() ?? [],
                record.Identifier?.ToArray() ?? [])).ToArray();

        return new NdefMessage(parsed);
    }

    static NFCNdefMessage ToIos(NdefMessage message)
    {
        return NFCNdefMessage.Create(NSData.FromArray(NdefCodec.EncodeMessage(message)))
            ?? throw new NfcPlusException(NfcPlusError.WriteFailed, "Could not encode the NDEF message.");
    }

    static NdefTypeNameFormat MapTnf(NFCTypeNameFormat format) => format switch
    {
        NFCTypeNameFormat.Empty => NdefTypeNameFormat.Empty,
        NFCTypeNameFormat.NFCWellKnown => NdefTypeNameFormat.WellKnown,
        NFCTypeNameFormat.Media => NdefTypeNameFormat.Media,
        NFCTypeNameFormat.AbsoluteUri => NdefTypeNameFormat.AbsoluteUri,
        NFCTypeNameFormat.NFCExternal => NdefTypeNameFormat.External,
        NFCTypeNameFormat.Unchanged => NdefTypeNameFormat.Unchanged,
        _ => NdefTypeNameFormat.Unknown
    };

    static bool IsEmptyTag(NSError error)
    {
        var code = (NFCReaderError)(long)error.Code;
        return code is NFCReaderError.NdefReaderSessionErrorZeroLengthMessage;
    }

    static NfcPlusException MapError(NSError error, NfcPlusError fallback)
    {
        var code = (NFCReaderError)(long)error.Code;
        var mapped = code switch
        {
            NFCReaderError.ReaderSessionInvalidationErrorUserCanceled => NfcPlusError.Cancelled,
            NFCReaderError.ReaderSessionInvalidationErrorSessionTimeout => NfcPlusError.Timeout,
            NFCReaderError.NdefReaderSessionErrorTagSizeTooSmall => NfcPlusError.MessageTooLarge,
            NFCReaderError.NdefReaderSessionErrorTagNotWritable => NfcPlusError.NotWritable,
            NFCReaderError.UnsupportedFeature => NfcPlusError.NotSupported,
            _ => fallback
        };

        return new NfcPlusException(mapped, error.LocalizedDescription ?? fallback.ToString());
    }

    sealed class SessionDelegate : NFCNdefReaderSessionDelegate
    {
        readonly IosNfcTransport _owner;

        public SessionDelegate(IosNfcTransport owner) => _owner = owner;

        public override void DidDetectTags(NFCNdefReaderSession session, INFCNdefTag[] tags) =>
            _owner.HandleTags(session, tags);

        public override void DidDetect(NFCNdefReaderSession session, NFCNdefMessage[] messages) =>
            _owner.HandleMessages(session, messages);

        public override void DidInvalidate(NFCNdefReaderSession session, NSError error) =>
            _owner.HandleInvalidate(session, error);
    }
}
#endif
