using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Chromaticity.Tools.Services;

namespace Chromaticity.Tools;

public partial class App : Application
{
    public string Tool { get; init; } = "spectrum";
    public IResultDownloader? ResultDownloader { get; init; }
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime single)
            single.MainView = new MainView(ResultDownloader, Tool);
        base.OnFrameworkInitializationCompleted();
    }
}
