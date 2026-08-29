namespace Plugin.Maui.NfcPlus;

/// <summary>
/// NFC Forum Type Name Format (TNF) for an NDEF record.
/// </summary>
public enum NdefTypeNameFormat
{
    /// <summary>Empty record.</summary>
    Empty = 0,

    /// <summary>NFC Forum well-known type (<c>T</c>, <c>U</c>, …).</summary>
    WellKnown = 1,

    /// <summary>MIME media type.</summary>
    Media = 2,

    /// <summary>Absolute URI stored in the type field.</summary>
    AbsoluteUri = 3,

    /// <summary>External type (<c>domain:type</c>).</summary>
    External = 4,

    /// <summary>Unknown type. Payload is treated as opaque bytes.</summary>
    Unknown = 5,

    /// <summary>Unchanged (chunked records). Rare in modern tags.</summary>
    Unchanged = 6
}
