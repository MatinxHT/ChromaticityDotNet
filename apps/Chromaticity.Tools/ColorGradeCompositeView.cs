using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Chromaticity.Tools.Services;
using ChromaticityDotNet.Model;

namespace Chromaticity.Tools;

public sealed partial class MainView
{
    private static Control CompositeGradePlaceholder() => Card(Stack(Text("复合色差色卡", 20, true),
        Note("请先生成一维色差分级色卡，复合色卡将使用同一标样、公式、阈值和展开级数。")));

    private static Control CompositeGradeCard(ColorGradeResult source, ColorComparisonOptions comparisonOptions)
    {
        var layer = 0;
        var selectedC = 0;
        var selectedH = 0;
        var plane = ColorGradeCalculations.GenerateComposite(source, comparisonOptions: comparisonOptions);
        var coordinateSystem = Select(["参考原点", "555分色法"]);
        coordinateSystem.Name = "CompositeCoordinateSystem";
        ColorGradeCoordinates Coordinates(int l, int c, int h) => ColorGradeCoordinates.FromGrades(l, c, h,
            coordinateSystem.SelectedIndex == 1 ? ColorGradeCoordinateSystem.FiveFiveFive : ColorGradeCoordinateSystem.ReferenceOrigin);
        string Grade(int level)
        {
            var value = Coordinates(level, 0, 0).Lightness;
            return value.ToString(coordinateSystem.SelectedIndex == 1 ? "0" : "+0;-0;0", CultureInfo.InvariantCulture);
        }
        var standardHex = source.Scales.Single(scale => scale.Axis == ColorGradeAxis.Lightness)
            .Chips.Single(chip => chip.Level == 0).Hex!;
        var layerLabel = Text("", 16, true); layerLabel.Name = "CompositeLightnessLabel";
        layerLabel.TextAlignment = TextAlignment.Center;
        layerLabel.HorizontalAlignment = HorizontalAlignment.Center;
        var details = new ContentControl
        {
            Name = "CompositeGradeDetails", HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        var evaluation = Text("", 15, true);
        evaluation.Name = "CompositeGradeEvaluation";
        evaluation.TextAlignment = TextAlignment.Center;
        var grid = new Grid { Name = "CompositeGradeGrid", HorizontalAlignment = HorizontalAlignment.Center };
        var swatches = new List<(ColorGradeCompositeChip Chip, Border Swatch)>();
        var scroll = new ScrollViewer
        {
            Content = grid, HorizontalContentAlignment = HorizontalAlignment.Center,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        static TextBlock Center(string value, double size = 13, bool bold = false)
        {
            var text = Text(value, size, bold);
            text.TextAlignment = TextAlignment.Center;
            text.HorizontalAlignment = HorizontalAlignment.Center;
            text.VerticalAlignment = VerticalAlignment.Center;
            return text;
        }
        void ShowSelection()
        {
            var chip = plane.Chips.Single(chip => chip.ChromaLevel == selectedC && chip.HueLevel == selectedH);
            evaluation.IsVisible = chip.Comparison is not null;
            if (chip.Comparison is { } comparison)
            {
                var comments = ColorEvaluationPresentation.Comments(comparison);
                evaluation.Text = $"明度 L*：{comments[0]} · 彩度 C*：{comments[1]} · 色相 h°：{comments[2]}";
            }
            else evaluation.Text = "";
            foreach (var item in swatches)
            {
                var selected = item.Chip == chip;
                item.Swatch.BorderThickness = new Thickness(selected ? 3 : 1);
                item.Swatch.BorderBrush = selected ? Brushes.Black : Brushes.White;
            }
            static StackPanel ColorValues(string title, ColorGradeLab lab, string footer)
            {
                var values = Stack(Text(title, 15, true),
                    Text($"L* {ToolCalculations.F(lab.L)} · C* {ToolCalculations.F(lab.Chroma)} · h° {(lab.Chroma == 0 ? "—" : ToolCalculations.F(lab.Hue))}", 13),
                    Text($"L* {ToolCalculations.F(lab.L)} · a* {ToolCalculations.F(lab.A)} · b* {ToolCalculations.F(lab.B)}", 13),
                    Note(footer));
                values.Spacing = 6;
                return values;
            }
            var standardValues = ColorValues($"标样：{Coordinates(0, 0, 0)}", source.Standard, standardHex);
            standardValues.Name = "CompositeReferenceValues";
            standardValues.Margin = new Thickness(0, 0, 16, 0);
            foreach (var text in standardValues.Children.OfType<TextBlock>()) text.TextAlignment = TextAlignment.Right;
            var selectedTitle = $"所选色块：{Coordinates(layer, selectedC, selectedH)}";
            var selectedValues = chip.Lab is { } lab
                ? ColorValues(selectedTitle, lab, $"{chip.Hex} · 标样 ΔE {ToolCalculations.F(chip.StandardDeltaE!.Value)}")
                : Stack(Text(selectedTitle, 15, true), Note(chip.UnavailableReason ?? "不可生成"));
            selectedValues.Name = "CompositeSelectedValues";
            selectedValues.Margin = new Thickness(16, 0, 0, 0);
            var referencePreview = new Border
            {
                Name = "CompositeReferenceSwatch", Background = Brush.Parse(standardHex),
                CornerRadius = new CornerRadius(4, 0, 0, 4), MinHeight = 96
            };
            var selectedPreview = new Border
            {
                Name = "CompositeSelectedSwatch", Background = Brush.Parse(chip.Hex ?? "#EEEEEE"),
                CornerRadius = new CornerRadius(0, 4, 4, 0), MinHeight = 96
            };
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,76,76,*"), MinWidth = 760, MaxWidth = 960,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            row.Children.Add(standardValues);
            Grid.SetColumn(referencePreview, 1); row.Children.Add(referencePreview);
            Grid.SetColumn(selectedPreview, 2); row.Children.Add(selectedPreview);
            Grid.SetColumn(selectedValues, 3); row.Children.Add(selectedValues);
            details.Content = new ScrollViewer
            {
                Content = row, HorizontalContentAlignment = HorizontalAlignment.Center,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
        }
        Button? decrease = null;
        Button? increase = null;
        Button? reset = null;
        void UpdateLayer()
        {
            plane = ColorGradeCalculations.GenerateComposite(source, layer, comparisonOptions);
            var lightness = source.Scales.Single(scale => scale.Axis == ColorGradeAxis.Lightness).Chips;
            decrease!.IsEnabled = lightness.Any(chip => chip.Level == layer - 1 && chip.Lab.HasValue);
            increase!.IsEnabled = lightness.Any(chip => chip.Level == layer + 1 && chip.Lab.HasValue);
            reset!.IsEnabled = layer != 0;
            RenderPlane();
        }
        void RenderPlane()
        {
            layerLabel.Text = $"当前明度层：L {Grade(layer)} 级 · L* {ToolCalculations.F(plane.Center!.Value.L)}";
            grid.Children.Clear(); grid.ColumnDefinitions.Clear(); grid.RowDefinitions.Clear(); swatches.Clear();
            grid.ColumnDefinitions.Add(new ColumnDefinition(64, GridUnitType.Pixel));
            grid.RowDefinitions.Add(new RowDefinition(36, GridUnitType.Pixel));
            var count = source.Levels * 2 + 1;
            for (var i = 0; i < count; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(76, GridUnitType.Pixel));
                grid.RowDefinitions.Add(new RowDefinition(64, GridUnitType.Pixel));
                var cLabel = Center($"C {Grade(i - source.Levels)}", 12, i == source.Levels);
                Grid.SetColumn(cLabel, i + 1); grid.Children.Add(cLabel);
                var hLabel = Center($"h {Grade(source.Levels - i)}", 12, i == source.Levels);
                Grid.SetRow(hLabel, i + 1); grid.Children.Add(hLabel);
            }
            foreach (var chip in plane.Chips)
            {
                var center = chip.ChromaLevel == 0 && chip.HueLevel == 0;
                var label = Center(Coordinates(layer, chip.ChromaLevel, chip.HueLevel).ToString(), 11, center);
                var cellContent = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
                if (center) cellContent.Children.Add(Center(layer == 0 ? "标样" : "本层中心", 12, true));
                else if (chip.Lab is null) cellContent.Children.Add(Center("不可生成", 11));
                cellContent.Children.Add(label);
                var background = chip.Hex is { } hex ? Brush.Parse(hex) : Brush.Parse("#EEEEEE");
                var foreground = CompositeSwatchText(chip.Hex);
                foreach (var text in cellContent.Children.OfType<TextBlock>()) text.Foreground = foreground;
                var swatch = new Border
                {
                    Background = background, Child = cellContent, BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(1), Width = 76, Height = 64
                };
                var button = new Button
                {
                    Name = $"CompositeChipC{chip.ChromaLevel}H{chip.HueLevel}",
                    Content = swatch, Padding = new Thickness(0), BorderThickness = new Thickness(0),
                    CornerRadius = new CornerRadius(0), HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                button.Click += (_, _) => { selectedC = chip.ChromaLevel; selectedH = chip.HueLevel; ShowSelection(); };
                Grid.SetColumn(button, chip.ChromaLevel + source.Levels + 1);
                Grid.SetRow(button, source.Levels - chip.HueLevel + 1);
                grid.Children.Add(button); swatches.Add((chip, swatch));
            }
            ShowSelection();
        }
        decrease = Button("L −1 级", () => { layer--; UpdateLayer(); }); decrease.Name = "CompositeDecreaseL";
        increase = Button("L +1 级", () => { layer++; UpdateLayer(); }); increase.Name = "CompositeIncreaseL";
        reset = Button("回到 L 0 级", () => { layer = 0; UpdateLayer(); }); reset.Name = "CompositeResetL";
        UpdateLayer();
        coordinateSystem.SelectionChanged += (_, _) => RenderPlane();
        var layerActions = Actions(decrease, reset, increase);
        layerActions.Name = "CompositeLightnessActions";
        layerActions.HorizontalAlignment = HorizontalAlignment.Center;
        foreach (var button in new[] { decrease, reset, increase }) button.Margin = new Thickness(5, 0, 5, 8);
        return Card(Stack(Text("复合色差色卡", 20, true),
            Note("横轴为彩度 C* 等级（向右增加），纵轴为色相 h° 等级（向上增加）。坐标按 (L,C,h) 等级显示；参考原点的标样为 (0,0,0)，555分色法将三个坐标各加 5，标样为 (5,5,5)。"),
            Note($"{source.Formula} · 一级 ΔE = {ToolCalculations.F(source.Settings.StepDeltaE)}"),
            Fields(("坐标系", coordinateSystem)),
            scroll, layerLabel, layerActions, details, evaluation,
            Note("L 按钮使用一维色卡对应等级的 L*，整体切换明度层，所有格子的 C*、h° 保持不变。每格将组合后的 LCh 转为 Lab；复合色块的相邻 ΔE 不一定等于一级阈值。点击色块查看坐标与色差；窄屏可横向滚动。")));
    }

    private static IBrush CompositeSwatchText(string? hex)
    {
        if (hex is null) return Muted;
        var color = Color.Parse(hex);
        static double Linear(byte channel)
        {
            var value = channel / 255.0;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        var luminance = 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        return luminance > 0.179 ? Brushes.Black : Brushes.White;
    }
}
