using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.VisualTree;
using Chromaticity.Tools.Services;
using ChromaticityDotNet.AvaloniaDiagram.Controls;
using Xunit;

namespace Chromaticity.Tools.Tests;

public class ColorInconstancyViewTests
{
    [AvaloniaFact]
    public void FormulaAndWeightsUpdateOnlyAnalysisAndSourceChangesClearBothResults()
    {
        var page = new MainView(tool: "spectrum");
        var window = new Window { Width = 1200, Height = 1000, Content = page };
        window.Show();
        try
        {
            Control[] Controls() => page.GetVisualDescendants().OfType<Control>().ToArray();
            T Find<T>(string name) where T : Control => Controls().OfType<T>().Single(control => control.Name == name);
            var analysis = Find<Border>("ColorInconstancyAnalysis");
            var original = Find<Border>("SpectrumResult");
            var formula = Find<ComboBox>("InconstancyFormula");
            var input = Find<TextBox>("SpectrumInput");
            input.Text = ColorInconstancyTests.Ramp(380, 780, 10);
            void Calculate() => Controls().OfType<Button>().Single(button => button.Content is TextBlock text && text.Text == "计算颜色值")
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Calculate();
            using var frame = window.CaptureRenderedFrame();
            Grid Table()
            {
                using var updated = window.CaptureRenderedFrame();
                return Find<Grid>("InconstancyTable");
            }
            string Cell(int row, int column) => ((TextBlock)((Border)Table().Children.Single(cell =>
                Grid.GetRow(cell) == row && Grid.GetColumn(cell) == column)).Child!).Text!;
            Assert.Equal("0.0000", Cell(1, 6));
            Assert.Equal(new[] { "无变化", "无变化", "无变化" }, Enumerable.Range(7, 3).Select(column => Cell(1, column)));
            Assert.Equal("U3000", Cell(4, 0));
            Assert.True(analysis.TranslatePoint(default, page)!.Value.Y > original.TranslatePoint(default, page)!.Value.Y);
            var plot = Find<SpectrumPlot>("ReflectanceSpectrumPlot");
            var originalData = plot.Data;
            var originalValues = original.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).ToArray();
            var previous = Cell(3, 6);
            Find<TextBox>("InconstancyKC").Text = "2";
            Assert.NotEqual(previous, Cell(3, 6));
            Assert.Same(originalData, plot.Data);
            Assert.Equal(originalValues, original.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text));
            Find<TextBox>("InconstancyKL").Text = "0";
            using var invalid = window.CaptureRenderedFrame();
            Assert.DoesNotContain(Controls(), control => control.Name == "InconstancyTable");
            Assert.Contains(analysis.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "错误：kL 必须大于 0。");
            Assert.Same(originalData, plot.Data);
            formula.SelectedIndex = 0; // Invalid hidden coefficients do not affect CIE76.
            Assert.Equal("0.0000", Cell(1, 6));
            formula.SelectedIndex = 1;
            previous = Cell(3, 6);
            Find<TextBox>("InconstancyCmcC").Text = "0.8";
            Assert.NotEqual(previous, Cell(3, 6));
            Find<ComboBox>("SpectrumObserver").SelectedIndex = 0;
            Assert.Null(plot.Data);
            Assert.DoesNotContain(Controls(), control => control.Name == "InconstancyTable");
            Calculate(); Assert.NotNull(Table());
            input.Text = "18";
            Assert.Null(plot.Data);
            Assert.DoesNotContain(Controls(), control => control.Name == "InconstancyTable");
            Assert.All(analysis.GetVisualDescendants().OfType<Button>(), button => Assert.False(button.IsEnabled));
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(1200, false)]
    [InlineData(390, false)]
    [InlineData(390, true)]
    public void AnalysisSupportsNarrowScreensAndLanguageChangesWithoutLosingResults(double width, bool english)
    {
        var page = new MainView(tool: "spectrum");
        var window = new Window { Width = width, Height = 1100, Content = page };
        window.Show();
        try
        {
            var input = page.GetVisualDescendants().OfType<TextBox>().Single(box => box.Name == "SpectrumInput");
            var selectedLight = page.GetVisualDescendants().OfType<ComboBox>().Single(combo => combo.Name == "SpectrumIlluminant");
            selectedLight.SelectedIndex = Array.FindIndex(ToolCalculations.Illuminants, light => light.Id == "D50");
            var data = ToolCalculations.Reflectance(input.Text!, 380, 780, 10, false, "D50", ToolCalculations.DefaultObserver);
            Assert.Equal(18, data.Values.Min()); Assert.Equal(65, data.Values.Max());
            Assert.True(data.Values.Distinct().Count() > 10);
            page.GetVisualDescendants().OfType<Button>().Single(button => button.Content is TextBlock text && text.Text == "计算颜色值")
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (english) UiLanguage.Set(UiLanguage.English);
            var scroll = (ScrollViewer)page.Content!;
            var analysis = page.GetVisualDescendants().OfType<Border>().Single(control => control.Name == "ColorInconstancyAnalysis");
            using var layout = window.CaptureRenderedFrame();
            var table = analysis.GetVisualDescendants().OfType<Grid>().Single(control => control.Name == "InconstancyTable");
            var tableScroll = table.GetVisualAncestors().OfType<ScrollViewer>().First();
            Assert.True(analysis.Bounds.Width <= width);
            Assert.True(table.Bounds.Width >= 1090);
            if (width == 390) Assert.True(tableScroll.Extent.Width > tableScroll.Viewport.Width);
            Assert.Contains(analysis.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == (english ? "Color inconstancy analysis" : "颜色不恒常性分析"));
            var cells = table.Children.OfType<Border>().ToArray();
            string TextAt(int row, int column) => ((TextBlock)cells[row * 10 + column].Child!).Text!;
            Assert.Equal(60, cells.Length); // Ten display columns, with a paired swatch in each data row.
            Assert.Equal(english ? "Illuminant reference" : "照明体参考", TextAt(0, 1));
            Assert.Equal(UiLanguage.Translate("sRGB 对比\n左标样 / 右样品"), TextAt(0, 2));
            Assert.Equal(new[] { "D65", "CWF", "A", "U3000", "TL84" }, Enumerable.Range(1, 5).Select(row => TextAt(row, 0)));
            Assert.Equal(ColorEvaluationPresentation.Headers[..3].Select(UiLanguage.Translate), Enumerable.Range(7, 3).Select(column => TextAt(0, column)));
            Assert.DoesNotContain(table.GetVisualDescendants().OfType<TextBlock>(), cell => cell.Text is "状态" or "有效" or "Status" or "Valid");
            var expected = ColorInconstancyCalculations.Analyze(data, new());
            for (var i = 0; i < 5; i++)
            {
                var pair = Assert.IsType<Border>(cells[(i + 1) * 10 + 2].Child);
                Assert.Equal("InconstancySwatchPair", pair.Name);
                var swatches = pair.GetVisualDescendants().OfType<Border>().ToArray();
                var standard = swatches.Single(swatch => swatch.Name == "InconstancyReferenceSwatch");
                var sample = swatches.Single(swatch => swatch.Name == "InconstancyComparedSwatch");
                Assert.Equal(Color.Parse(expected.Rows[i][^3]), Assert.IsAssignableFrom<ISolidColorBrush>(standard.Background).Color);
                Assert.Equal(Color.Parse(expected.Rows[i][^2]), Assert.IsAssignableFrom<ISolidColorBrush>(sample.Background).Color);
                Assert.Equal(standard.Bounds.Right, sample.Bounds.Left);
                Assert.Equal(standard.Bounds.Width, sample.Bounds.Width);
                Assert.Equal(120, pair.Bounds.Width); Assert.Equal(52, pair.Bounds.Height);
            }
            Assert.Contains(analysis.GetVisualDescendants().OfType<TextBlock>(), block => (block.Text ?? "").Contains("D50 / 10°"));
            var notice = analysis.GetVisualDescendants().OfType<TextBlock>().Single(block =>
                block.Text == UiLanguage.Translate(ColorInconstancyCalculations.IlluminantReferenceNotice));
            Assert.True(notice.TranslatePoint(default, analysis)!.Value.Y >= tableScroll.TranslatePoint(default, analysis)!.Value.Y + tableScroll.Bounds.Height);
            if (english) Assert.All(analysis.GetVisualDescendants().OfType<TextBlock>(), block =>
                Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", block.Text ?? ""));
            var directory = Environment.GetEnvironmentVariable("CHROMATICITY_INCONSTANCY_ARTIFACTS");
            if (directory is not null)
            {
                scroll.Offset = new Vector(0, analysis.TranslatePoint(default, scroll)!.Value.Y - 12);
                using var frame = window.CaptureRenderedFrame();
                Directory.CreateDirectory(directory);
                frame!.Save(Path.Combine(directory, $"analysis-{width}-{(english ? "en" : "zh")}.png"), PngBitmapEncoderOptions.Default);
            }
        }
        finally { UiLanguage.Set(UiLanguage.Chinese); window.Close(); }
    }
}
