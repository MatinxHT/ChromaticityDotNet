using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Chromaticity.Tools.Services;
using ChromaticityDotNet.AvaloniaDiagram.Controls;
using Xunit;

namespace Chromaticity.Tools.Tests;

public class TextileMetamerismViewTests
{
    [AvaloniaTheory]
    [InlineData(1200, false)]
    [InlineData(390, false)]
    [InlineData(390, true)]
    public void TwoSpectraCalculateAdaptToScreenAndKeepResultsAcrossLanguageChanges(double width, bool english)
    {
        var downloader = new CaptureDownloader();
        var page = new MainView(downloader, tool: "metamerism");
        var window = new Window { Width = width, Height = 1100, Content = page };
        window.Show();
        try
        {
            T Find<T>(string name) where T : Control => page.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);
            var standard = Find<TextBox>("MetamerismStandardInput");
            var sample = Find<TextBox>("MetamerismSampleInput");
            Assert.Equal(TextileMetamerismExamples.Standard, standard.Text);
            Assert.Equal(TextileMetamerismExamples.MetamericSample, sample.Text);
            Find<Button>("MetamerismCalculate").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (english) UiLanguage.Set(UiLanguage.English);
            using var frame = window.CaptureRenderedFrame();
            var table = Find<Grid>("MetamerismTable");
            var tableScroll = table.GetVisualAncestors().OfType<ScrollViewer>().First();
            Assert.Equal(78, table.Children.Count); // 13 visible columns, one header and five illuminants.
            Assert.True(table.Bounds.Width >= 1290);
            if (width == 390) Assert.True(tableScroll.Extent.Width > tableScroll.Viewport.Width);
            Assert.True(Find<Border>("TextileMetamerismResult").Bounds.Width <= width);
            Assert.True(Find<ResponsiveColumns>("MetamerismInputs").Children[1].Bounds.Y > 0 == (width == 390));
            var plot = Find<SpectrumPlot>("MetamerismSpectrumPlot");
            Assert.NotNull(plot.StandardData); Assert.NotNull(plot.Data);
            Assert.Equal(41, plot.Data.Count); Assert.Equal(380, plot.SelectedWavelength);
            string Cell(int row, int column) => ((TextBlock)((Border)table.Children.Single(cell =>
                Grid.GetRow(cell) == row && Grid.GetColumn(cell) == column)).Child!).Text!;
            Assert.InRange(double.Parse(Cell(1, 9), CultureInfo.InvariantCulture), double.Epsilon, 0.1);
            Assert.Equal(new[] { "D65", "CWF", "A", "U3000", "TL84" }, Enumerable.Range(1, 5).Select(row => Cell(row, 0)));
            var pairs = table.GetVisualDescendants().OfType<Border>().Where(border => border.Name == "MetamerismSwatchPair").ToArray();
            Assert.Equal(5, pairs.Length);
            foreach (var pair in pairs)
            {
                var swatches = pair.GetVisualDescendants().OfType<Border>().ToArray();
                Assert.Equal(swatches[0].Bounds.Width, swatches[1].Bounds.Width);
                Assert.Equal(swatches[0].Bounds.Right, swatches[1].Bounds.Left);
            }
            var inputText = sample.Text;
            UiLanguage.Set(english ? UiLanguage.Chinese : UiLanguage.English);
            Assert.Equal(inputText, sample.Text); Assert.NotNull(Find<Grid>("MetamerismTable"));
            UiLanguage.Set(english ? UiLanguage.English : UiLanguage.Chinese);
            if (english) Assert.All(page.GetVisualDescendants().OfType<TextBlock>(), block =>
                Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", block.Text ?? ""));
            Find<Border>("TextileMetamerismResult").GetVisualDescendants().OfType<Button>()
                .Single(button => button.Content is TextBlock block && block.Text == UiLanguage.Translate("下载 CSV"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.NotNull(downloader.Csv);
            Assert.Contains(english ? "Standard L*" : "标样 L*", downloader.Csv);
            Assert.Contains("FL12", downloader.Csv);
            var directory = Environment.GetEnvironmentVariable("CHROMATICITY_METAMERISM_ARTIFACTS");
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
                using var inputs = window.CaptureRenderedFrame();
                inputs!.Save(Path.Combine(directory, $"inputs-{width}-{(english ? "en" : "zh")}.png"), PngBitmapEncoderOptions.Default);
                var scroll = (ScrollViewer)page.Content!;
                var result = Find<Border>("TextileMetamerismResult");
                scroll.Offset = new Vector(0, result.TranslatePoint(default, scroll)!.Value.Y - 12);
                using var results = window.CaptureRenderedFrame();
                results!.Save(Path.Combine(directory, $"results-{width}-{(english ? "en" : "zh")}.png"), PngBitmapEncoderOptions.Default);
            }
        }
        finally { UiLanguage.Set(UiLanguage.Chinese); window.Close(); }
    }

    [AvaloniaFact]
    public void FormulaUpdatesUseExistingSpectraAndChangingInputsClearsAnalysisAndBothCurves()
    {
        var page = new MainView(tool: "metamerism");
        var window = new Window { Width = 1200, Height = 1000, Content = page };
        window.Show();
        try
        {
            T Find<T>(string name) where T : Control => page.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);
            void Calculate() => Find<Button>("MetamerismCalculate").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            string DeltaA()
            {
                using var frame = window.CaptureRenderedFrame();
                return ((TextBlock)((Border)Find<Grid>("MetamerismTable").Children.Single(cell =>
                    Grid.GetRow(cell) == 3 && Grid.GetColumn(cell) == 9)).Child!).Text!;
            }
            Calculate();
            var original = DeltaA();
            var plot = Find<SpectrumPlot>("MetamerismSpectrumPlot");
            var source = plot.Data;
            Find<TextBox>("MetamerismKL").Text = "2";
            Find<TextBox>("MetamerismKC").Text = "2";
            Find<TextBox>("MetamerismKH").Text = "2";
            Assert.NotEqual(original, DeltaA()); Assert.Same(source, plot.Data);
            Find<TextBox>("MetamerismKL").Text = "0";
            using var invalid = window.CaptureRenderedFrame();
            Assert.DoesNotContain(page.GetVisualDescendants(), control => control.Name == "MetamerismTable");
            Assert.Same(source, plot.Data);
            Find<ComboBox>("MetamerismFormula").SelectedIndex = 0;
            Assert.NotNull(DeltaA()); // Hidden invalid CIEDE2000 weights do not affect CIE76.
            Find<ComboBox>("MetamerismFormula").SelectedIndex = 1;
            original = DeltaA();
            Find<TextBox>("MetamerismCmcC").Text = "2";
            Assert.NotEqual(original, DeltaA());
            foreach (var name in new[] { "MetamerismObserver", "MetamerismUnits" })
            {
                var combo = Find<ComboBox>(name);
                combo.SelectedIndex = combo.SelectedIndex == 0 ? 1 : 0;
                Assert.Null(plot.Data); Assert.Null(plot.StandardData);
                Calculate(); Assert.NotNull(DeltaA());
            }
            Find<TextBox>("MetamerismStep").Text = "5";
            Assert.Null(plot.Data); Assert.Null(plot.StandardData);
            Calculate();
            using var mismatch = window.CaptureRenderedFrame();
            Assert.DoesNotContain(page.GetVisualDescendants(), control => control.Name == "MetamerismTable");
            page.GetVisualDescendants().OfType<Button>().Single(button => button.Content is TextBlock block && block.Text == "载入相同光谱示例")
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Calculate(); Assert.Equal("0.0000", DeltaA());
            Find<TextBox>("MetamerismSampleInput").Text = "50,20,-30";
            Assert.Null(plot.Data); Assert.Null(plot.StandardData);
            Calculate();
            using var failed = window.CaptureRenderedFrame();
            Assert.DoesNotContain(page.GetVisualDescendants(), control => control.Name == "MetamerismTable");
            Assert.All(Find<Border>("TextileMetamerismResult").GetVisualDescendants().OfType<Button>(), button => Assert.False(button.IsEnabled));
        }
        finally { window.Close(); }
    }

    private sealed class CaptureDownloader : IResultDownloader
    {
        public string? Csv { get; private set; }
        public Task DownloadAsync(string fileName, string csv) { Csv = csv; return Task.CompletedTask; }
    }
}
