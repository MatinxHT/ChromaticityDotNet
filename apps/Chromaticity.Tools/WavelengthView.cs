using Avalonia.Controls;
using Avalonia.Layout;
using Chromaticity.Tools.Services;
using static ChromaticityDotNet.Model.DataModel;

namespace Chromaticity.Tools;

public sealed partial class MainView
{
    private Control WavelengthPage()
    {
        WavelengthCalculation? calculated = null;
        var result = new ResultPanel(_downloader, renderResult: _ => WavelengthResults(calculated!),
            successMessage: "已完成波长计算。复制及导出包含样品、白点、观察者和波长结果。");
        result.Name = "WavelengthResult";
        var x = SmallInput("0.3"); x.Name = "WavelengthX";
        var y = SmallInput("0.6"); y.Name = "WavelengthY";
        var luminance = SmallInput("100"); luminance.Name = "WavelengthLuminance";
        var observer = Observer(); observer.Name = "WavelengthObserver";
        var light = Light(); light.Name = "WavelengthIlluminant";
        var source = Select(["标准光源白点", "自定义白点"]); source.Name = "WavelengthWhiteSource";
        var whiteX = SmallInput("0.3127"); whiteX.Name = "WavelengthWhiteX";
        var whiteY = SmallInput("0.3290"); whiteY.Name = "WavelengthWhiteY";
        var standardFields = Fields(("参考照明体", light));
        var customFields = Fields(("白点 x", whiteX), ("白点 y", whiteY)); customFields.IsVisible = false;
        var plot = new ChromaticityDiagram { Name = "WavelengthDiagram", HorizontalAlignment = HorizontalAlignment.Stretch };
        plot.Clear(SelectedObserver(observer));
        void Invalidate()
        {
            calculated = null; result.Invalidate(); plot.Clear(SelectedObserver(observer));
        }
        foreach (var box in new[] { x, y, luminance, whiteX, whiteY })
            box.PropertyChanged += (_, change) => { if (change.Property == TextBox.TextProperty) Invalidate(); };
        light.SelectionChanged += (_, _) => Invalidate();
        observer.SelectionChanged += (_, _) => Invalidate();
        source.SelectionChanged += (_, _) =>
        {
            customFields.IsVisible = source.SelectedIndex == 1;
            standardFields.IsVisible = source.SelectedIndex == 0;
            Invalidate();
        };
        void Calculate()
        {
            plot.Clear(SelectedObserver(observer));
            result.Run(() =>
            {
                var sample = new CIExyY
                {
                    CIEx = ToolCalculations.Number(x.Text ?? "", "x"),
                    CIEy = ToolCalculations.Number(y.Text ?? "", "y"),
                    CIEY = ToolCalculations.Number(luminance.Text ?? "", "Y")
                };
                var white = source.SelectedIndex == 1 ? new CIExyY
                {
                    CIEx = ToolCalculations.Number(whiteX.Text ?? "", "白点 x"),
                    CIEy = ToolCalculations.Number(whiteY.Text ?? "", "白点 y"), CIEY = 100
                } : null;
                calculated = WavelengthCalculations.Calculate(sample, SelectedObserver(observer), SelectedLight(light), white);
                plot.SetData(calculated);
                return calculated.Table;
            });
        }
        var calculate = Primary("计算波长", Calculate); calculate.Name = "WavelengthCalculate";
        return Stack(Card(Stack(
            Text("主波长与补色波长计算", 24, true),
            Note("输入样品 x、y、Y，选择观察者与参考白点。波长由白点到样品的方向确定，Y 不影响计算。"),
            Fields(("样品 x", x), ("样品 y", y), ("样品 Y", luminance)),
            Fields(("观察者", observer), ("白点来源", source)), standardFields, customFields,
            Note("标准光源可选全部 50 条 CIE 光源；白点在其与观察者的共同波段积分，Y = 100。"),
            Actions(Button("载入绿色示例", () => { x.Text = "0.3"; y.Text = "0.6"; luminance.Text = "100"; }),
                Button("载入紫色示例", () => { x.Text = "0.4"; y.Text = "0.2"; luminance.Text = "100"; }), calculate))),
            result, Card(Stack(Text("CIE xy 色度图", 20, true), plot,
                Note("W 为白点，P 为样品。实线连接两点，虚线沿同一直线延伸到色域边界。马蹄边界包含光谱轨迹与紫边，背景为裁剪到 sRGB 的屏幕近似。"),
                Note("紫色区域没有主波长；某些颜色没有单色补色波长，显示为 —。样品与白点重合时两者均未定义。两位小数表示输出格式，不代表测量精度。"))));
    }

    private static Control WavelengthResults(WavelengthCalculation calculation)
    {
        var values = new ResponsiveColumns();
        Control Metric(string title, double? value) => Stack(Text(title, 15, true),
            Text(value.HasValue ? $"{WavelengthCalculations.Format(value)} nm" : "—", 28, true));
        values.Children.Add(Metric("主波长", calculation.Wavelengths.DominantWavelength));
        values.Children.Add(Metric("补色波长", calculation.Wavelengths.ComplementaryWavelength));
        return Stack(values, Note(calculation.Table.Rows[0][^1]), Note(calculation.Table.Conditions));
    }
}
