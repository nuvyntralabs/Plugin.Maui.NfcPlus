namespace Plugin.Maui.NfcPlus;

/// <summary>
/// Process-wide defaults for <see cref="INfcPlus"/>.
/// </summary>
public sealed class NfcPlusOptions
{
    /// <summary>
    /// iOS system-sheet message and Android reader hint. Default is
    /// <c>Hold your phone near the NFC tag</c>.
    /// </summary>
    public string DefaultAlertMessage { get; set; } = "Hold your phone near the NFC tag";

    /// <summary>
    /// Android listen API. Default is <see cref="NfcAndroidListenMode.ReaderMode"/>.
    /// </summary>
    public NfcAndroidListenMode AndroidListenMode { get; set; } = NfcAndroidListenMode.ReaderMode;

    /// <summary>
    /// Timeout for one-shot <c>ReadAsync</c> / <c>WriteAsync</c> when the caller
    /// does not pass one. Default is 60 seconds.
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(60);
}

/// <summary>
/// Options for <see cref="INfcPlus.StartAsync"/>.
/// </summary>
public sealed class NfcSessionOptions
{
    /// <summary>
    /// iOS alert on the system NFC sheet. Falls back to
    /// <see cref="NfcPlusOptions.DefaultAlertMessage"/>.
    /// </summary>
    public string? AlertMessage { get; set; }

    /// <summary>
    /// Android listen API for this session. <c>null</c> uses
    /// <see cref="NfcPlusOptions.AndroidListenMode"/>.
    /// </summary>
    public NfcAndroidListenMode? AndroidListenMode { get; set; }

    /// <summary>
    /// When <c>true</c>, the session stops after the first tag.
    /// <see cref="INfcPlus.StartAsync"/> defaults this to <c>false</c> (continuous).
    /// One-shot <c>ReadAsync</c> / <c>WriteAsync</c> default to <c>true</c>.
    /// </summary>
    public bool InvalidateAfterFirstRead { get; set; }

    /// <summary>
    /// Android reader-mode presence-check delay. <c>null</c> uses the OS default.
    /// </summary>
    public TimeSpan? PresenceCheckDelay { get; set; }
}

/// <summary>
/// Options for <see cref="INfcPlus.ReadAsync"/>.
/// </summary>
public sealed class NfcReadOptions
{
    /// <summary>
    /// How long to wait for a tag. <c>null</c> uses <see cref="NfcPlusOptions.DefaultTimeout"/>.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// iOS / one-shot alert. Falls back to <see cref="NfcPlusOptions.DefaultAlertMessage"/>.
    /// </summary>
    public string? AlertMessage { get; set; }
}

/// <summary>
/// Options for <see cref="INfcPlus.WriteAsync"/>.
/// </summary>
public sealed class NfcWriteOptions
{
    /// <summary>
    /// How long to wait for a tag. <c>null</c> uses <see cref="NfcPlusOptions.DefaultTimeout"/>.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// iOS / one-shot alert. Falls back to a write-specific default.
    /// </summary>
    public string? AlertMessage { get; set; }

    /// <summary>
    /// When <c>true</c>, lock the tag after a successful write so it cannot be rewritten.
    /// </summary>
    public bool MakeReadOnly { get; set; }

    /// <summary>
    /// When <c>true</c> (default), format an <c>NdefFormatable</c> tag before writing.
    /// </summary>
    public bool FormatIfNeeded { get; set; } = true;
}
