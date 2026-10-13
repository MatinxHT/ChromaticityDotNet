using global::Avalonia;
using global::Avalonia.Headless;
using global::Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(ChromaticityDotNet.AvaloniaDiagram.Tests.TestApplication))]

namespace ChromaticityDotNet.AvaloniaDiagram.Tests;

public sealed class TestApplication : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApplication>().UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
