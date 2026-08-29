namespace Plugin.Maui.NfcPlus;

/// <summary>
/// Tag technology reported by the platform. A tag may expose several.
/// </summary>
public enum NfcTagTechnology
{
    /// <summary>Unknown or unlisted technology.</summary>
    Unknown = 0,

    /// <summary>NDEF already formatted.</summary>
    Ndef = 1,

    /// <summary>Can be formatted as NDEF.</summary>
    NdefFormatable = 2,

    /// <summary>NFC-A (ISO 14443-3A), including most MIFARE Ultralight.</summary>
    NfcA = 3,

    /// <summary>NFC-B (ISO 14443-3B).</summary>
    NfcB = 4,

    /// <summary>NFC-F (FeliCa).</summary>
    NfcF = 5,

    /// <summary>NFC-V (ISO 15693).</summary>
    NfcV = 6,

    /// <summary>ISO-DEP (ISO 14443-4).</summary>
    IsoDep = 7,

    /// <summary>MIFARE Classic.</summary>
    MifareClassic = 8,

    /// <summary>MIFARE Ultralight.</summary>
    MifareUltralight = 9,

    /// <summary>ISO 15693 (iOS tag family).</summary>
    Iso15693 = 10,

    /// <summary>ISO 7816 / NFC-A Type 4 (iOS tag family).</summary>
    Iso7816 = 11,

    /// <summary>FeliCa (iOS tag family).</summary>
    FeliCa = 12,

    /// <summary>Barcode / NFC-Barcode.</summary>
    Barcode = 13
}
