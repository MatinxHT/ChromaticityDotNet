using global::Avalonia;
using global::Avalonia.Headless;
using global::Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(Chromaticity.Tools.Tests.AvaloniaDiagram.TestApplication))]

namespace Chromaticity.Tools.Tests.AvaloniaDiagram;

public sealed class TestApplication : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApplication>().UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
