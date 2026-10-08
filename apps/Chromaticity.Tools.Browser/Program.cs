using Avalonia;
using Avalonia.Browser;
using Chromaticity.Tools;
using Chromaticity.Tools.Services;
using System.Runtime.InteropServices.JavaScript;

internal sealed partial class Program
{
    private static Task Main(string[] args)
    {
        UiLanguage.Set(args.ElementAtOrDefault(1));
        return AppBuilder.Configure(() => new App { ResultDownloader = new BrowserResultDownloader(), Tool = args.FirstOrDefault() ?? "spectrum" })
            .StartBrowserAppAsync("out", new BrowserPlatformOptions { PreferFileDialogPolyfill = true });
    }

    [JSExport]
    public static void SetLanguage(string language) => UiLanguage.Set(language);
}

internal sealed partial class BrowserResultDownloader : IResultDownloader
{
    [JSImport("downloadCsv", "chromaticity-files")]
    private static partial void DownloadCsv(string fileName, string text);

    public Task DownloadAsync(string fileName, string csv)
    {
        DownloadCsv(fileName, csv);
        return Task.CompletedTask;
    }
}
