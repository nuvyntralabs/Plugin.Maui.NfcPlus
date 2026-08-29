using System.Text;

namespace Plugin.Maui.NfcPlus;

/// <summary>
/// One NDEF record. Prefer the typed subclasses
/// (<see cref="NdefTextRecord"/>, <see cref="NdefUriRecord"/>,
/// <see cref="NdefMimeRecord"/>, <see cref="NdefExternalRecord"/>)
/// when you know the payload kind.
/// </summary>
public class NdefRecord
{
    /// <summary>
    /// Initializes a record from TNF fields. Prefer the factory methods
    /// on the typed subclasses for new payloads.
    /// </summary>
    public NdefRecord(
        NdefTypeNameFormat typeNameFormat,
        byte[] type,
        byte[] payload,
        byte[]? id = null,
        NdefRecordKind? kind = null)
    {
        TypeNameFormat = typeNameFormat;
        Type = type ?? [];
        Payload = payload ?? [];
        Id = id ?? [];
        Kind = kind ?? NdefCodec.Classify(typeNameFormat, Type);
    }

    /// <summary>Gets the NFC Forum Type Name Format.</summary>
    public NdefTypeNameFormat TypeNameFormat { get; }

    /// <summary>Gets the high-level record kind.</summary>
    public NdefRecordKind Kind { get; }

    /// <summary>Gets the type field (well-known letter, MIME string, or external name).</summary>
    public byte[] Type { get; }

    /// <summary>Gets the optional record identifier.</summary>
    public byte[] Id { get; }

    /// <summary>Gets the raw payload bytes.</summary>
    public byte[] Payload { get; }

    /// <summary>Gets <see cref="Type"/> decoded as UTF-8.</summary>
    public string TypeString => Encoding.UTF8.GetString(Type);

    /// <summary>Creates a well-known text record.</summary>
    public static NdefTextRecord CreateText(string text, string language = "en") =>
        new(text, language);

    /// <summary>Creates a well-known URI record.</summary>
    public static NdefUriRecord CreateUri(System.Uri uri) => new(uri);

    /// <summary>Creates a well-known URI record from a string.</summary>
    public static NdefUriRecord CreateUri(string uri) =>
        new(new System.Uri(uri, UriKind.RelativeOrAbsolute));

    /// <summary>Creates a MIME record.</summary>
    public static NdefMimeRecord CreateMime(string mimeType, byte[] data) =>
        new(mimeType, data);

    /// <summary>Creates an external-type record (<c>domain:type</c>).</summary>
    public static NdefExternalRecord CreateExternal(string domainType, byte[] payload) =>
        new(domainType, payload);
}

/// <summary>
/// NFC Forum well-known text record (<c>T</c>).
/// </summary>
public sealed class NdefTextRecord : NdefRecord
{
    /// <summary>
    /// Initializes a UTF-8 text record.
    /// </summary>
    public NdefTextRecord(string text, string language = "en")
        : base(
            NdefTypeNameFormat.WellKnown,
            "T"u8.ToArray(),
            NdefCodec.EncodeText(text, language),
            kind: NdefRecordKind.Text)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Language = string.IsNullOrWhiteSpace(language) ? "en" : language;
    }

    internal NdefTextRecord(string text, string language, byte[] payload)
        : base(NdefTypeNameFormat.WellKnown, "T"u8.ToArray(), payload, kind: NdefRecordKind.Text)
    {
        Text = text;
        Language = language;
    }

    /// <summary>Gets the decoded text.</summary>
    public string Text { get; }

    /// <summary>Gets the ISO 639 language code stored on the tag.</summary>
    public string Language { get; }
}

/// <summary>
/// NFC Forum well-known URI record (<c>U</c>) or absolute URI.
/// </summary>
public sealed class NdefUriRecord : NdefRecord
{
    /// <summary>
    /// Initializes a well-known URI record with identifier-code compression.
    /// </summary>
    public NdefUriRecord(System.Uri uri)
        : base(
            NdefTypeNameFormat.WellKnown,
            "U"u8.ToArray(),
            NdefCodec.EncodeUri(uri),
            kind: NdefRecordKind.Uri)
    {
        Uri = uri ?? throw new ArgumentNullException(nameof(uri));
    }

    internal NdefUriRecord(System.Uri uri, NdefTypeNameFormat tnf, byte[] type, byte[] payload)
        : base(tnf, type, payload, kind: NdefRecordKind.Uri)
    {
        Uri = uri;
    }

    /// <summary>Gets the reconstructed URI.</summary>
    public System.Uri Uri { get; }
}

/// <summary>
/// MIME media record (TNF Media).
/// </summary>
public sealed class NdefMimeRecord : NdefRecord
{
    /// <summary>
    /// Initializes a MIME record.
    /// </summary>
    public NdefMimeRecord(string mimeType, byte[] data)
        : base(
            NdefTypeNameFormat.Media,
            Encoding.UTF8.GetBytes(mimeType ?? throw new ArgumentNullException(nameof(mimeType))),
            data ?? throw new ArgumentNullException(nameof(data)),
            kind: NdefRecordKind.Mime)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
            throw new ArgumentException("MIME type is required.", nameof(mimeType));

        MimeType = mimeType;
        Data = data;
    }

    /// <summary>Gets the MIME type (for example <c>application/json</c>).</summary>
    public string MimeType { get; }

    /// <summary>Gets the MIME payload.</summary>
    public byte[] Data { get; }
}

/// <summary>
/// External type record (TNF External), typically <c>domain:type</c>.
/// </summary>
public sealed class NdefExternalRecord : NdefRecord
{
    /// <summary>
    /// Initializes an external-type record.
    /// </summary>
    public NdefExternalRecord(string domainType, byte[] payload)
        : base(
            NdefTypeNameFormat.External,
            Encoding.UTF8.GetBytes(domainType ?? throw new ArgumentNullException(nameof(domainType))),
            payload ?? throw new ArgumentNullException(nameof(payload)),
            kind: NdefRecordKind.External)
    {
        if (string.IsNullOrWhiteSpace(domainType))
            throw new ArgumentException("External type name is required.", nameof(domainType));

        DomainType = domainType;
    }

    /// <summary>Gets the external type name (<c>android.com:pkg</c>, <c>mauiessentials.com:asset</c>).</summary>
    public string DomainType { get; }
}
