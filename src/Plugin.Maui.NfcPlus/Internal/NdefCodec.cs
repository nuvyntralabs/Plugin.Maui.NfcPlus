using System.Text;

namespace Plugin.Maui.NfcPlus;

/// <summary>
/// NFC Forum NDEF text / URI encode and decode. Shared so tests and both
/// platforms produce the same bytes.
/// </summary>
static class NdefCodec
{
    static readonly (byte Code, string Prefix)[] UriPrefixes =
    [
        (0x01, "http://www."),
        (0x02, "https://www."),
        (0x03, "http://"),
        (0x04, "https://"),
        (0x05, "tel:"),
        (0x06, "mailto:"),
        (0x07, "ftp://anonymous:anonymous@"),
        (0x08, "ftp://ftp."),
        (0x09, "ftps://"),
        (0x0A, "sftp://"),
        (0x0B, "smb://"),
        (0x0C, "nfs://"),
        (0x0D, "ftp://"),
        (0x0E, "dav://"),
        (0x0F, "news:"),
        (0x10, "telnet://"),
        (0x11, "imap:"),
        (0x12, "rtsp://"),
        (0x13, "urn:"),
        (0x14, "pop:"),
        (0x15, "sip:"),
        (0x16, "sips:"),
        (0x17, "tftp:"),
        (0x18, "btspp://"),
        (0x19, "btl2cap://"),
        (0x1A, "btgoep://"),
        (0x1B, "tcpobex://"),
        (0x1C, "irdaobex://"),
        (0x1D, "file://"),
        (0x1E, "urn:epc:id:"),
        (0x1F, "urn:epc:tag:"),
        (0x20, "urn:epc:pat:"),
        (0x21, "urn:epc:raw:"),
        (0x22, "urn:epc:"),
        (0x23, "urn:nfc:")
    ];

    public static NdefRecordKind Classify(NdefTypeNameFormat tnf, byte[] type)
    {
        if (tnf == NdefTypeNameFormat.Empty)
            return NdefRecordKind.Empty;

        if (tnf == NdefTypeNameFormat.Media)
            return NdefRecordKind.Mime;

        if (tnf == NdefTypeNameFormat.AbsoluteUri)
            return NdefRecordKind.Uri;

        if (tnf == NdefTypeNameFormat.External)
            return NdefRecordKind.External;

        if (tnf == NdefTypeNameFormat.WellKnown)
        {
            if (type.Length == 1 && type[0] == (byte)'T')
                return NdefRecordKind.Text;
            if (type.Length == 1 && type[0] == (byte)'U')
                return NdefRecordKind.Uri;
        }

        return NdefRecordKind.Unknown;
    }

    public static NdefRecord Parse(NdefTypeNameFormat tnf, byte[] type, byte[] payload, byte[]? id = null)
    {
        type ??= [];
        payload ??= [];
        id ??= [];

        return Classify(tnf, type) switch
        {
            NdefRecordKind.Empty => new NdefRecord(NdefTypeNameFormat.Empty, [], [], id, NdefRecordKind.Empty),
            NdefRecordKind.Text when TryParseText(payload, out var text, out var language) =>
                new NdefTextRecord(text, language, payload),
            NdefRecordKind.Uri when tnf == NdefTypeNameFormat.AbsoluteUri && TryCreateUri(Encoding.UTF8.GetString(type), out var abs) =>
                new NdefUriRecord(abs, tnf, type, payload),
            NdefRecordKind.Uri when TryParseUri(payload, out var uri) =>
                new NdefUriRecord(uri, tnf, type, payload),
            NdefRecordKind.Mime => new NdefMimeRecord(Encoding.UTF8.GetString(type), payload),
            NdefRecordKind.External => new NdefExternalRecord(Encoding.UTF8.GetString(type), payload),
            _ => new NdefRecord(tnf, type, payload, id)
        };
    }

    public static byte[] EncodeText(string text, string language)
    {
        ArgumentNullException.ThrowIfNull(text);
        language = string.IsNullOrWhiteSpace(language) ? "en" : language;
        var langBytes = Encoding.ASCII.GetBytes(language);
        if (langBytes.Length > 63)
            throw new ArgumentException("Language code must be 63 bytes or fewer.", nameof(language));

        var textBytes = Encoding.UTF8.GetBytes(text);
        var payload = new byte[1 + langBytes.Length + textBytes.Length];
        payload[0] = (byte)langBytes.Length; // UTF-8, bit 7 clear
        Buffer.BlockCopy(langBytes, 0, payload, 1, langBytes.Length);
        Buffer.BlockCopy(textBytes, 0, payload, 1 + langBytes.Length, textBytes.Length);
        return payload;
    }

    public static bool TryParseText(byte[] payload, out string text, out string language)
    {
        text = string.Empty;
        language = "en";
        if (payload.Length < 1)
            return false;

        var status = payload[0];
        var langLen = status & 0x3F;
        var utf16 = (status & 0x80) != 0;
        if (payload.Length < 1 + langLen)
            return false;

        language = Encoding.ASCII.GetString(payload, 1, langLen);
        var body = payload.AsSpan(1 + langLen);
        text = utf16 ? Encoding.Unicode.GetString(body) : Encoding.UTF8.GetString(body);
        return true;
    }

    public static byte[] EncodeUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        var value = uri.IsAbsoluteUri ? uri.AbsoluteUri : uri.OriginalString;
        var (code, prefix) = BestPrefix(value);
        var remainder = value[prefix.Length..];
        var remainderBytes = Encoding.UTF8.GetBytes(remainder);
        var payload = new byte[1 + remainderBytes.Length];
        payload[0] = code;
        Buffer.BlockCopy(remainderBytes, 0, payload, 1, remainderBytes.Length);
        return payload;
    }

    public static bool TryParseUri(byte[] payload, out Uri uri)
    {
        uri = null!;
        if (payload.Length < 1)
            return false;

        var prefix = PrefixFor(payload[0]);
        var remainder = Encoding.UTF8.GetString(payload, 1, payload.Length - 1);
        return TryCreateUri(prefix + remainder, out uri);
    }

    public static byte[] EncodeMessage(NdefMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        using var stream = new MemoryStream();
        var records = message.Records;
        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            var type = record.Type ?? [];
            var payload = record.Payload ?? [];
            var id = record.Id ?? [];
            var shortRecord = payload.Length < 256;
            byte flags = (byte)((int)record.TypeNameFormat & 0x07);
            if (i == 0)
                flags |= 0x80;
            if (i == records.Count - 1)
                flags |= 0x40;
            if (shortRecord)
                flags |= 0x10;
            if (id.Length > 0)
                flags |= 0x08;

            stream.WriteByte(flags);
            stream.WriteByte((byte)type.Length);
            if (shortRecord)
            {
                stream.WriteByte((byte)payload.Length);
            }
            else
            {
                stream.WriteByte((byte)((payload.Length >> 24) & 0xFF));
                stream.WriteByte((byte)((payload.Length >> 16) & 0xFF));
                stream.WriteByte((byte)((payload.Length >> 8) & 0xFF));
                stream.WriteByte((byte)(payload.Length & 0xFF));
            }

            if (id.Length > 0)
                stream.WriteByte((byte)id.Length);

            stream.Write(type, 0, type.Length);
            if (id.Length > 0)
                stream.Write(id, 0, id.Length);
            stream.Write(payload, 0, payload.Length);
        }

        return stream.ToArray();
    }

    public static string ToHex(byte[] bytes)
    {
        if (bytes.Length == 0)
            return string.Empty;

        var chars = new char[bytes.Length * 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            chars[i * 2] = ToHexChar(b >> 4);
            chars[i * 2 + 1] = ToHexChar(b & 0xF);
        }

        return new string(chars);
    }

    static (byte Code, string Prefix) BestPrefix(string value)
    {
        (byte Code, string Prefix) best = (0x00, string.Empty);
        foreach (var candidate in UriPrefixes)
        {
            if (value.StartsWith(candidate.Prefix, StringComparison.OrdinalIgnoreCase)
                && candidate.Prefix.Length > best.Prefix.Length)
            {
                best = candidate;
            }
        }

        return best;
    }

    static string PrefixFor(byte code)
    {
        foreach (var entry in UriPrefixes)
        {
            if (entry.Code == code)
                return entry.Prefix;
        }

        return string.Empty;
    }

    static bool TryCreateUri(string value, out Uri uri) =>
        System.Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out uri!) && uri is not null;

    static char ToHexChar(int nibble) =>
        (char)(nibble < 10 ? '0' + nibble : 'A' + (nibble - 10));
}
