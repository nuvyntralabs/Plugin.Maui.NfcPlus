using Microsoft.Maui.Hosting;

namespace Plugin.Maui.NfcPlus;

/// <summary>
/// Registers the NFC plugin with the MAUI dependency injection container.
/// </summary>
public static class MauiAppBuilderExtensions
{
    /// <summary>
    /// Adds <see cref="INfcPlus"/> as a singleton.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.UseNfcPlus(options =>
    /// {
    ///     options.DefaultAlertMessage = "Hold your phone near the badge";
    ///     options.AndroidListenMode = NfcAndroidListenMode.ReaderMode;
    /// });
    /// </code>
    /// </example>
    public static MauiAppBuilder UseNfcPlus(this MauiAppBuilder builder, Action<NfcPlusOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new NfcPlusOptions();
        configure?.Invoke(options);

        builder.Services.AddNfcPlus(options);
        builder.Services.AddTransient<IMauiInitializeService, NfcPlusInitializer>();
        return builder;
    }
}
