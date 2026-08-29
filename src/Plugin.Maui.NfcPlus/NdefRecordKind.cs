namespace Plugin.Maui.NfcPlus;

/// <summary>
/// High-level classification of a parsed NDEF record.
/// </summary>
public enum NdefRecordKind
{
    /// <summary>Empty TNF.</summary>
    Empty = 0,

    /// <summary>Well-known text (<c>T</c>).</summary>
    Text = 1,

    /// <summary>Well-known URI (<c>U</c>) or absolute URI.</summary>
    Uri = 2,

    /// <summary>MIME media type.</summary>
    Mime = 3,

    /// <summary>External type name.</summary>
    External = 4,

    /// <summary>Any other TNF / type combination.</summary>
    Unknown = 5
}
