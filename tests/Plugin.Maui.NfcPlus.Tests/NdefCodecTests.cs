using System.Text;

namespace Plugin.Maui.NfcPlus.Tests;

public sealed class NdefCodecTests
{
    [Fact]
    public void Text_round_trips_utf8_and_language()
    {
        var record = NdefRecord.CreateText("SKU-1042", "en");

        Assert.Equal(NdefRecordKind.Text, record.Kind);
        Assert.Equal("SKU-1042", record.Text);
        Assert.Equal("en", record.Language);

        var parsed = NdefCodec.Parse(record.TypeNameFormat, record.Type, record.Payload);
        var text = Assert.IsType<NdefTextRecord>(parsed);
        Assert.Equal("SKU-1042", text.Text);
        Assert.Equal("en", text.Language);
    }

    [Theory]
    [InlineData("https://www.example.com/p/1", (byte)0x02)]
    [InlineData("https://example.com/p/1", (byte)0x04)]
    [InlineData("http://shop.local/item", (byte)0x03)]
    [InlineData("tel:+15551212", (byte)0x05)]
    [InlineData("mailto:ops@example.com", (byte)0x06)]
    public void Uri_uses_nfc_forum_identifier_code(string value, byte expectedCode)
    {
        var record = NdefRecord.CreateUri(value);

        Assert.Equal(NdefRecordKind.Uri, record.Kind);
        Assert.Equal(expectedCode, record.Payload[0]);

        var parsed = Assert.IsType<NdefUriRecord>(
            NdefCodec.Parse(record.TypeNameFormat, record.Type, record.Payload));
        Assert.Equal(value, parsed.Uri.OriginalString);
    }

    [Fact]
    public void Mime_preserves_type_and_bytes()
    {
        var json = """{"assetId":"EQ-88"}"""u8.ToArray();
        var record = NdefRecord.CreateMime("application/json", json);

        Assert.Equal(NdefRecordKind.Mime, record.Kind);
        Assert.Equal("application/json", record.MimeType);
        Assert.Equal(json, record.Data);

        var parsed = Assert.IsType<NdefMimeRecord>(
            NdefCodec.Parse(record.TypeNameFormat, record.Type, record.Payload));
        Assert.Equal("application/json", parsed.MimeType);
        Assert.Equal(json, parsed.Data);
    }

    [Fact]
    public void External_round_trips_domain_type()
    {
        var payload = "EMP-204"u8.ToArray();
        var record = NdefRecord.CreateExternal("mauiessentials.com:employee", payload);

        var parsed = Assert.IsType<NdefExternalRecord>(
            NdefCodec.Parse(record.TypeNameFormat, record.Type, record.Payload));
        Assert.Equal("mauiessentials.com:employee", parsed.DomainType);
        Assert.Equal(payload, parsed.Payload);
    }

    [Fact]
    public void Message_convenience_accessors_use_first_matching_record()
    {
        var message = NdefMessage.FromRecords(
            NdefRecord.CreateText("forklift-12"),
            NdefRecord.CreateUri("https://assets.example.com/forklift-12"),
            NdefRecord.CreateMime("text/plain", "ok"u8.ToArray()));

        Assert.Equal("forklift-12", message.Text);
        Assert.Equal("https://assets.example.com/forklift-12", message.Uri?.OriginalString);
        Assert.Equal("text/plain", message.Mime?.MimeType);
        Assert.Equal(3, message.Records.Count);
    }

    [Fact]
    public void Empty_and_unknown_records_are_classified()
    {
        var empty = NdefCodec.Parse(NdefTypeNameFormat.Empty, [], []);
        Assert.Equal(NdefRecordKind.Empty, empty.Kind);

        var unknown = NdefCodec.Parse(NdefTypeNameFormat.WellKnown, "Sp"u8.ToArray(), [1, 2, 3]);
        Assert.Equal(NdefRecordKind.Unknown, unknown.Kind);
    }

    [Fact]
    public void EncodeMessage_sets_message_begin_and_end_flags()
    {
        var bytes = NdefCodec.EncodeMessage(NdefMessage.FromText("SKU-1042"));

        Assert.True(bytes.Length > 3);
        Assert.Equal(0xD1, bytes[0]); // MB | ME | SR | WellKnown
        Assert.Equal((byte)'T', bytes[3]);
    }

    [Fact]
    public void ToHex_is_uppercase_without_separators()
    {
        Assert.Equal("04A2B3", NdefCodec.ToHex([0x04, 0xA2, 0xB3]));
        Assert.Equal(string.Empty, NdefCodec.ToHex([]));
    }

    [Fact]
    public void Absolute_uri_tnf_parses_as_uri_record()
    {
        var type = Encoding.UTF8.GetBytes("https://inspect.example.com/v/88");
        var parsed = Assert.IsType<NdefUriRecord>(
            NdefCodec.Parse(NdefTypeNameFormat.AbsoluteUri, type, []));
        Assert.Equal("https://inspect.example.com/v/88", parsed.Uri.OriginalString);
    }
}
