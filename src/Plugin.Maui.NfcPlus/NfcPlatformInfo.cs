namespace Plugin.Maui.NfcPlus;

/// <summary>
/// Describes the native NFC stack used on this platform.
/// </summary>
/// <param name="IsNative">Whether a real OS stack is backing the plugin.</param>
/// <param name="Stack">Reader API name (<c>Android NfcAdapter</c>, <c>iOS CoreNFC</c>).</param>
/// <param name="SessionModel">How the OS presents a session (reader mode vs system sheet).</param>
/// <param name="TagIdSource">Where <see cref="NfcTag.Id"/> comes from.</param>
public sealed record NfcPlatformInfo(
    bool IsNative,
    string Stack,
    string SessionModel,
    string TagIdSource)
{
    /// <summary>Gets a description for the shared net10.0 reference assembly.</summary>
    public static NfcPlatformInfo Unsupported { get; } =
        new(false, "None", "Not supported", "None");

    /// <summary>Gets the Android reader-mode / foreground-dispatch stack.</summary>
    public static NfcPlatformInfo Android { get; } =
        new(true, "Android NfcAdapter", "Reader mode or foreground dispatch", "Tag.GetId()");

    /// <summary>Gets the iOS CoreNFC stack.</summary>
    public static NfcPlatformInfo iOS { get; } =
        new(true, "iOS CoreNFC", "NFCNdefReaderSession system sheet", "INFCNdefTag identifier (MiFare / ISO15693 / ISO7816 / FeliCa)");
}
