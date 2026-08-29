using Microsoft.Extensions.Logging;
using Plugin.Maui.NfcPlus;

namespace Plugin.Maui.NfcPlus.Sample;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.Services.AddSingleton<MainPage>();

        builder
            .UseMauiApp<App>()
            .UseNfcPlus(options =>
            {
                options.DefaultAlertMessage = "Hold your phone near the NFC tag";
                options.AndroidListenMode = NfcAndroidListenMode.ReaderMode;
                options.DefaultTimeout = TimeSpan.FromSeconds(45);
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
