namespace Plugin.Maui.NfcPlus;

/// <summary>
/// A tag that entered the field: identifier, technologies, and optional NDEF.
/// </summary>
public sealed class NfcTag
{
    /// <summary>
    /// Initializes a parsed tag snapshot.
    /// </summary>
    public NfcTag(
        byte[] id,
        IReadOnlyList<NfcTagTechnology> technologies,
        NdefMessage? message,
        bool isNdef,
        bool isWritable,
        bool canMakeReadOnly,
        int? maxNdefSize,
        DateTimeOffset detectedAt)
    {
        Id = id ?? [];
        IdHex = NdefCodec.ToHex(Id);
        Technologies = technologies ?? [];
        Message = message;
        IsNdef = isNdef;
        IsWritable = isWritable;
        CanMakeReadOnly = canMakeReadOnly;
        MaxNdefSize = maxNdefSize;
        DetectedAt = detectedAt;
    }

    /// <summary>Gets the raw UID / identifier bytes. Empty when the OS does not expose one.</summary>
    public byte[] Id { get; }

    /// <summary>Gets <see cref="Id"/> as uppercase hex (<c>04A2B3C4</c>).</summary>
    public string IdHex { get; }

    /// <summary>Gets the technologies advertised by the tag.</summary>
    public IReadOnlyList<NfcTagTechnology> Technologies { get; }

    /// <summary>Gets the parsed NDEF message, or <c>null</c> when the tag has none.</summary>
    public NdefMessage? Message { get; }

    /// <summary>Gets a value indicating whether the tag already contains NDEF.</summary>
    public bool IsNdef { get; }

    /// <summary>Gets a value indicating whether NDEF can be written.</summary>
    public bool IsWritable { get; }

    /// <summary>Gets a value indicating whether the tag can be permanently locked.</summary>
    public bool CanMakeReadOnly { get; }

    /// <summary>Gets the NDEF capacity in bytes, when the platform reports it.</summary>
    public int? MaxNdefSize { get; }

    /// <summary>Gets when this snapshot was captured (UTC).</summary>
    public DateTimeOffset DetectedAt { get; }

    /// <summary>Gets the first text payload, if any.</summary>
    public string? Text => Message?.Text;

    /// <summary>Gets the first URI, if any.</summary>
    public Uri? Uri => Message?.Uri;

    /// <summary>Gets the first MIME record, if any.</summary>
    public NdefMimeRecord? Mime => Message?.Mime;

    /// <summary>Gets the NDEF records, or an empty list.</summary>
    public IReadOnlyList<NdefRecord> Records => Message?.Records ?? [];

    /// <inheritdoc />
    public override string ToString()
    {
        if (!string.IsNullOrEmpty(Text))
            return $"NFC {IdHex} text={Text}";
        if (Uri is not null)
            return $"NFC {IdHex} uri={Uri}";
        if (Mime is not null)
            return $"NFC {IdHex} mime={Mime.MimeType}";
        return string.IsNullOrEmpty(IdHex) ? "NFC tag" : $"NFC {IdHex}";
    }
}
