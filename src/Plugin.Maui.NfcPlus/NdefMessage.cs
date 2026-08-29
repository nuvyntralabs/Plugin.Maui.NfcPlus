namespace Plugin.Maui.NfcPlus;

/// <summary>
/// An NDEF message: an ordered list of records plus convenience accessors
/// for the first text, URI, and MIME payloads.
/// </summary>
public sealed class NdefMessage
{
    /// <summary>
    /// Initializes a message from records.
    /// </summary>
    public NdefMessage(IEnumerable<NdefRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        Records = records.ToArray();
    }

    /// <summary>Gets the records in message order.</summary>
    public IReadOnlyList<NdefRecord> Records { get; }

    /// <summary>Gets the first text record, if any.</summary>
    public NdefTextRecord? TextRecord => Records.OfType<NdefTextRecord>().FirstOrDefault();

    /// <summary>Gets the first URI record, if any.</summary>
    public NdefUriRecord? UriRecord => Records.OfType<NdefUriRecord>().FirstOrDefault();

    /// <summary>Gets the first MIME record, if any.</summary>
    public NdefMimeRecord? MimeRecord => Records.OfType<NdefMimeRecord>().FirstOrDefault();

    /// <summary>Gets the first text payload, if any.</summary>
    public string? Text => TextRecord?.Text;

    /// <summary>Gets the first URI, if any.</summary>
    public Uri? Uri => UriRecord?.Uri;

    /// <summary>Gets the first MIME record, if any.</summary>
    public NdefMimeRecord? Mime => MimeRecord;

    /// <summary>Creates a single-record text message.</summary>
    public static NdefMessage FromText(string text, string language = "en") =>
        new([NdefRecord.CreateText(text, language)]);

    /// <summary>Creates a single-record URI message.</summary>
    public static NdefMessage FromUri(Uri uri) =>
        new([NdefRecord.CreateUri(uri)]);

    /// <summary>Creates a single-record URI message from a string.</summary>
    public static NdefMessage FromUri(string uri) =>
        new([NdefRecord.CreateUri(uri)]);

    /// <summary>Creates a single-record MIME message.</summary>
    public static NdefMessage FromMime(string mimeType, byte[] data) =>
        new([NdefRecord.CreateMime(mimeType, data)]);

    /// <summary>Creates a message from records.</summary>
    public static NdefMessage FromRecords(params NdefRecord[] records) =>
        new(records);
}
