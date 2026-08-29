namespace Plugin.Maui.NfcPlus;

/// <summary>
/// Registers NFC services without MAUI lifecycle hooks.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="INfcPlus"/> using the supplied options instance.
    /// </summary>
    public static IServiceCollection AddNfcPlus(this IServiceCollection services, NfcPlusOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);
        services.TryAddSingleton<INfcPlus>(sp =>
        {
            var resolved = sp.GetService<NfcPlusOptions>() ?? options;
            var transport = sp.GetService<INfcTransport>() ?? NfcPlus.CreatePlatform();
            var nfc = NfcPlus.Create(resolved, transport);
            NfcPlus.SetDefault(nfc);
            return nfc;
        });

        return services;
    }

    /// <summary>
    /// Adds <see cref="INfcPlus"/> and applies <paramref name="configure"/> to a new options instance.
    /// </summary>
    public static IServiceCollection AddNfcPlus(this IServiceCollection services, Action<NfcPlusOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new NfcPlusOptions();
        configure?.Invoke(options);
        return services.AddNfcPlus(options);
    }
}
