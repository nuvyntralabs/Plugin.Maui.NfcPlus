using Microsoft.Maui.Hosting;

namespace Plugin.Maui.NfcPlus;

sealed class NfcPlusInitializer : IMauiInitializeService
{
    public void Initialize(IServiceProvider services)
    {
        var nfc = services.GetService<INfcPlus>() ?? NfcPlus.Current;
        NfcPlus.SetDefault(nfc);
    }
}
