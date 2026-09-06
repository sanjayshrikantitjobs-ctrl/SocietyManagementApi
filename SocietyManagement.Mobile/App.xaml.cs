using Microsoft.Extensions.DependencyInjection;

namespace SocietyManagement.Mobile;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // AppShell must be resolved here, not injected as a constructor
        // parameter — DI resolves constructor parameters BEFORE this
        // constructor body runs, so an injected AppShell would parse its
        // own XAML (including every {StaticResource ...} it uses) before
        // the InitializeComponent() call above has populated
        // Application.Current.Resources from App.xaml, throwing
        // "StaticResource not found" the moment AppShell.xaml uses one.
        var appShell = _services.GetRequiredService<AppShell>();
        return new Window(appShell);
    }
}
