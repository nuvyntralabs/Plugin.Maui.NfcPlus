namespace Plugin.Maui.NfcPlus;

/// <summary>
/// Thrown when an NFC session or tag operation cannot complete.
/// </summary>
public sealed class NfcPlusException : Exception
{
    /// <summary>
    /// Initializes a new exception with an error code and message.
    /// </summary>
    public NfcPlusException(NfcPlusError error, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Error = error;
    }

    /// <summary>
    /// Gets the classified failure.
    /// </summary>
    public NfcPlusError Error { get; }
}
