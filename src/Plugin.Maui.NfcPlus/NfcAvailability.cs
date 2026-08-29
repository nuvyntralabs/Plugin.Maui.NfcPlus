namespace Plugin.Maui.NfcPlus;

/// <summary>
/// Hardware and radio state for NFC on this device.
/// </summary>
public enum NfcAvailability
{
    /// <summary>Availability has not been probed yet.</summary>
    Unknown = 0,

    /// <summary>NFC hardware is present and the radio / reader is ready.</summary>
    Available = 1,

    /// <summary>Hardware exists but NFC is turned off (typical on Android).</summary>
    Disabled = 2,

    /// <summary>The OS denied NFC reading (restricted / no entitlement / parental controls).</summary>
    Restricted = 3,

    /// <summary>No NFC hardware, simulator, or unsupported TFM.</summary>
    Unsupported = 4
}
