namespace Plugin.Maui.NfcPlus;

sealed class NfcPlusImplementation : INfcPlus, IDisposable
{
    readonly NfcPlusOptions _options;
    readonly INfcTransport _transport;
    readonly object _gate = new();
    readonly SemaphoreSlim _sessionLock = new(1, 1);

    NfcSessionState _state = NfcSessionState.Idle;
    NfcSessionOptions? _listenOptions;
    TaskCompletionSource<NfcTag>? _pendingTag;
    bool _keepListening;
    bool _disposed;

    public NfcPlusImplementation(NfcPlusOptions options, INfcTransport transport)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _transport.AvailabilityChanged += OnAvailabilityChanged;
        _transport.NativeSessionEnded += OnNativeSessionEnded;
    }

    public bool IsSupported => _transport.IsSupported;

    public NfcAvailability Availability => _transport.GetAvailability();

    public NfcPlatformInfo Platform => _transport.Platform;

    public NfcSessionState SessionState
    {
        get
        {
            lock (_gate)
                return _state;
        }
    }

    public bool IsSessionActive
    {
        get
        {
            lock (_gate)
                return _state is NfcSessionState.Active or NfcSessionState.AwaitingTag or NfcSessionState.Starting;
        }
    }

    public NfcTag? LastTag { get; private set; }

    public NfcSnapshot Snapshot
    {
        get
        {
            lock (_gate)
            {
                return new NfcSnapshot(
                    DateTimeOffset.UtcNow,
                    _transport.GetAvailability(),
                    _state,
                    _state is NfcSessionState.Active or NfcSessionState.AwaitingTag or NfcSessionState.Starting,
                    LastTag,
                    _transport.Platform);
            }
        }
    }

    public event EventHandler<NfcTagDetectedEventArgs>? TagDetected;

    public event EventHandler<NfcSessionChangedEventArgs>? SessionChanged;

    public event EventHandler<NfcAvailabilityChangedEventArgs>? AvailabilityChanged;

    public Task<NfcAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_transport.GetAvailability());
    }

    public async Task StartAsync(NfcSessionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureReady();

        options ??= new NfcSessionOptions();
        options.InvalidateAfterFirstRead = false;

        await _sessionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsSessionActive)
                return;

            SetState(NfcSessionState.Starting);
            _keepListening = true;
            _listenOptions = options;
            await _transport.StartAsync(CreateRequest(NfcSessionPurpose.Listen, options, new NfcWriteOptions()), cancellationToken)
                .ConfigureAwait(false);
            SetState(NfcSessionState.Active);
        }
        catch
        {
            _keepListening = false;
            SetState(NfcSessionState.Idle, "start failed");
            throw;
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    public async Task StopAsync()
    {
        if (_disposed)
            return;

        FailPending(new NfcPlusException(NfcPlusError.Cancelled, "The NFC session was stopped."));
        _keepListening = false;

        await _sessionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            SetState(NfcSessionState.Stopping, "stop");
            await _transport.StopAsync().ConfigureAwait(false);
            SetState(NfcSessionState.Idle, "stop");
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    public Task<NfcTag> ReadAsync(NfcReadOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new NfcReadOptions();
        var session = new NfcSessionOptions
        {
            AlertMessage = options.AlertMessage ?? _options.DefaultAlertMessage,
            InvalidateAfterFirstRead = true
        };

        return AwaitTagAsync(
            NfcSessionPurpose.Read,
            session,
            new NfcWriteOptions { Timeout = options.Timeout, AlertMessage = options.AlertMessage },
            cancellationToken);
    }

    public Task<NfcTag> WriteAsync(NdefMessage message, NfcWriteOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        options ??= new NfcWriteOptions();
        var session = new NfcSessionOptions
        {
            AlertMessage = options.AlertMessage ?? "Hold your phone near the tag to write",
            InvalidateAfterFirstRead = true
        };

        return AwaitTagAsync(NfcSessionPurpose.Write, session, options, cancellationToken, message);
    }

    public Task<NfcTag> WriteTextAsync(string text, string language = "en", NfcWriteOptions? options = null, CancellationToken cancellationToken = default) =>
        WriteAsync(NdefMessage.FromText(text, language), options, cancellationToken);

    public Task<NfcTag> WriteUriAsync(Uri uri, NfcWriteOptions? options = null, CancellationToken cancellationToken = default) =>
        WriteAsync(NdefMessage.FromUri(uri), options, cancellationToken);

    public Task<NfcTag> WriteMimeAsync(string mimeType, byte[] payload, NfcWriteOptions? options = null, CancellationToken cancellationToken = default) =>
        WriteAsync(NdefMessage.FromMime(mimeType, payload), options, cancellationToken);

    public Task<NfcTag> MakeReadOnlyAsync(NfcWriteOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new NfcWriteOptions();
        var session = new NfcSessionOptions
        {
            AlertMessage = options.AlertMessage ?? "Hold your phone near the tag to lock it",
            InvalidateAfterFirstRead = true
        };

        return AwaitTagAsync(NfcSessionPurpose.MakeReadOnly, session, options, cancellationToken);
    }

    public Task OpenSettingsAsync() => _transport.OpenSettingsAsync();

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _transport.AvailabilityChanged -= OnAvailabilityChanged;
        _transport.NativeSessionEnded -= OnNativeSessionEnded;
        _transport.Dispose();
        _sessionLock.Dispose();
    }

    async Task<NfcTag> AwaitTagAsync(
        NfcSessionPurpose purpose,
        NfcSessionOptions sessionOptions,
        NfcWriteOptions writeOptions,
        CancellationToken cancellationToken,
        NdefMessage? writeMessage = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureReady();

        var timeout = writeOptions.Timeout ?? _options.DefaultTimeout;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout > TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
            cts.CancelAfter(timeout);

        var tcs = new TaskCompletionSource<NfcTag>(TaskCreationOptions.RunContinuationsAsynchronously);
        var wasListening = false;

        await _sessionLock.WaitAsync(cts.Token).ConfigureAwait(false);
        try
        {
            lock (_gate)
            {
                if (_pendingTag is not null)
                {
                    throw new NfcPlusException(
                        NfcPlusError.InvalidOperation,
                        "Another NFC read or write is already waiting for a tag.");
                }

                _pendingTag = tcs;
                wasListening = _keepListening && _state is NfcSessionState.Active or NfcSessionState.AwaitingTag;
                _state = NfcSessionState.AwaitingTag;
            }

            SessionChanged?.Invoke(this, new NfcSessionChangedEventArgs(NfcSessionState.AwaitingTag));

            if (purpose == NfcSessionPurpose.Read && wasListening)
            {
                // Next TagDetected from the live session completes the waiter.
            }
            else
            {
                if (wasListening)
                    await _transport.StopAsync().ConfigureAwait(false);

                await _transport.StartAsync(CreateRequest(purpose, sessionOptions, writeOptions, writeMessage), cts.Token)
                    .ConfigureAwait(false);
            }
        }
        catch
        {
            ClearPending(tcs);
            if (wasListening)
                _keepListening = true;
            throw;
        }
        finally
        {
            _sessionLock.Release();
        }

        try
        {
            return await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new NfcPlusException(NfcPlusError.Timeout, "No NFC tag was presented before the timeout.");
        }
        finally
        {
            ClearPending(tcs);

            if (purpose != NfcSessionPurpose.Read || !wasListening)
            {
                await _transport.StopAsync().ConfigureAwait(false);
                if (wasListening && _listenOptions is not null)
                {
                    _keepListening = true;
                    await _transport.StartAsync(
                            CreateRequest(NfcSessionPurpose.Listen, _listenOptions, new NfcWriteOptions()),
                            CancellationToken.None)
                        .ConfigureAwait(false);
                    SetState(NfcSessionState.Active);
                }
                else
                {
                    _keepListening = wasListening && _keepListening;
                    SetState(_keepListening ? NfcSessionState.Active : NfcSessionState.Idle);
                }
            }
            else
            {
                SetState(NfcSessionState.Active);
            }
        }
    }

    NfcListenRequest CreateRequest(
        NfcSessionPurpose purpose,
        NfcSessionOptions sessionOptions,
        NfcWriteOptions writeOptions,
        NdefMessage? writeMessage = null)
    {
        sessionOptions.AlertMessage ??= purpose switch
        {
            NfcSessionPurpose.Write => "Hold your phone near the tag to write",
            NfcSessionPurpose.MakeReadOnly => "Hold your phone near the tag to lock it",
            _ => _options.DefaultAlertMessage
        };
        sessionOptions.AndroidListenMode ??= _options.AndroidListenMode;

        return new NfcListenRequest
        {
            Purpose = purpose,
            WriteMessage = writeMessage,
            Options = sessionOptions,
            WriteOptions = writeOptions,
            OnTag = OnTag,
            OnFailed = OnTransportFailed
        };
    }

    void OnTag(NfcTag tag)
    {
        LastTag = tag;
        TagDetected?.Invoke(this, new NfcTagDetectedEventArgs(tag));

        TaskCompletionSource<NfcTag>? pending;
        lock (_gate)
            pending = _pendingTag;

        pending?.TrySetResult(tag);
    }

    void OnTransportFailed(Exception exception)
    {
        var wrapped = exception as NfcPlusException
            ?? new NfcPlusException(NfcPlusError.SessionFailed, exception.Message, exception);
        FailPending(wrapped);
    }

    void OnNativeSessionEnded(object? sender, NfcSessionChangedEventArgs e)
    {
        if (e.State != NfcSessionState.Idle)
            return;

        FailPending(new NfcPlusException(
            ReasonToError(e.Reason),
            e.Reason ?? "The NFC session ended."));

        lock (_gate)
        {
            if (!_keepListening)
                _state = NfcSessionState.Idle;
        }

        SessionChanged?.Invoke(this, e);
    }

    void OnAvailabilityChanged(object? sender, NfcAvailabilityChangedEventArgs e) =>
        AvailabilityChanged?.Invoke(this, e);

    void EnsureReady()
    {
        if (!_transport.IsSupported)
        {
            throw new NfcPlusException(
                NfcPlusError.NotSupported,
                "NFC is supported on Android and iOS. The net10.0 reference assembly is for tests; inject INfcTransport.");
        }

        var availability = _transport.GetAvailability();
        if (availability == NfcAvailability.Available)
            return;

        throw new NfcPlusException(
            availability == NfcAvailability.Disabled ? NfcPlusError.Unavailable : NfcPlusError.NotSupported,
            availability switch
            {
                NfcAvailability.Disabled => "NFC is turned off. Ask the user to enable it, or call OpenSettingsAsync.",
                NfcAvailability.Restricted => "NFC reading is restricted on this device.",
                _ => "This device does not support NFC."
            });
    }

    void SetState(NfcSessionState state, string? reason = null)
    {
        lock (_gate)
            _state = state;

        SessionChanged?.Invoke(this, new NfcSessionChangedEventArgs(state, reason));
    }

    void FailPending(Exception exception)
    {
        TaskCompletionSource<NfcTag>? pending;
        lock (_gate)
        {
            pending = _pendingTag;
            _pendingTag = null;
        }

        pending?.TrySetException(exception);
    }

    void ClearPending(TaskCompletionSource<NfcTag> tcs)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_pendingTag, tcs))
                _pendingTag = null;
        }
    }

    static NfcPlusError ReasonToError(string? reason)
    {
        if (string.IsNullOrEmpty(reason))
            return NfcPlusError.SessionFailed;
        if (reason.Contains("cancel", StringComparison.OrdinalIgnoreCase))
            return NfcPlusError.Cancelled;
        if (reason.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            return NfcPlusError.Timeout;
        return NfcPlusError.SessionFailed;
    }
}
