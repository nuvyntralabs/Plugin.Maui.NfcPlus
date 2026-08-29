namespace Plugin.Maui.NfcPlus.Sample;

public partial class App : Application
{
    readonly MainPage _mainPage;

    public App(MainPage mainPage)
    {
        InitializeComponent();
        _mainPage = mainPage;
    }

    protected override Window CreateWindow(IActivationState? activationState)
        => new Window(new NavigationPage(_mainPage));
}
