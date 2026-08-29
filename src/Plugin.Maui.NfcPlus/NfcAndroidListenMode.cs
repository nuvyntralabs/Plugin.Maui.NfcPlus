namespace Plugin.Maui.NfcPlus;

/// <summary>
/// How Android listens for tags while the app is in the foreground.
/// </summary>
public enum NfcAndroidListenMode
{
    /// <summary>
    /// <c>NfcAdapter.EnableReaderMode</c>. Preferred. No <c>OnNewIntent</c> hook is required.
    /// </summary>
    ReaderMode = 0,

    /// <summary>
    /// Classic foreground dispatch. Call <see cref="NfcPlus.HandlePlatformIntent"/> from
    /// <c>MainActivity.OnNewIntent</c>.
    /// </summary>
    ForegroundDispatch = 1
}
