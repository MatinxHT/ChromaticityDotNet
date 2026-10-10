using Avalonia.Controls;
using System.Globalization;
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
        var x = SmallInput("0.5253"); x.Name = "WavelengthX";
        var y = SmallInput("0.3485"); y.Name = "WavelengthY";
        var inputSpace = Select(["CIE xy", "CIE 1976 u′v′"]); inputSpace.Name = "WavelengthInputSpace";
        var u = SmallInput(""); u.Name = "WavelengthU";
        var v = SmallInput(""); v.Name = "WavelengthV";
        var xyFields = Fields(("样品 x", x), ("样品 y", y));
        var uvFields = Fields(("样品 u′", u), ("样品 v′", v)); uvFields.IsVisible = false;
        var observer = Observer(); observer.Name = "WavelengthObserver";
        var light = Light(); light.Name = "WavelengthIlluminant";
        var source = Select(["标准光源白点", "自定义白点"]); source.Name = "WavelengthWhiteSource";
        var whiteX = SmallInput("0.3127"); whiteX.Name = "WavelengthWhiteX";
        var whiteY = SmallInput("0.3290"); whiteY.Name = "WavelengthWhiteY";
        var whiteU = SmallInput(""); whiteU.Name = "WavelengthWhiteU";
        var whiteV = SmallInput(""); whiteV.Name = "WavelengthWhiteV";
        var standardFields = Fields(("参考照明体", light));
        var customFields = Fields(("白点 x", whiteX), ("白点 y", whiteY)); customFields.IsVisible = false;
        var customUvFields = Fields(("白点 u′", whiteU), ("白点 v′", whiteV)); customUvFields.IsVisible = false;
        void SyncCoordinates(TextBox first, TextBox second, TextBox targetFirst, TextBox targetSecond, bool toUv)
        {
            try
            {
                double a = ToolCalculations.Number(first.Text ?? "", toUv ? "x" : "u′");
                double b = ToolCalculations.Number(second.Text ?? "", toUv ? "y" : "v′");
                double nextFirst, nextSecond;
                if (toUv)
                {
                    var projected = new ChromaticityDiagramProjection(ChromaticityDiagramSpace.UvPrime).Project(a, b);
                    nextFirst = projected.X; nextSecond = projected.Y;
                }
                else
                {
                    var xy = WavelengthCalculations.UvToXy(new CIEuv { CIEu = a, CIEv = b });
                    nextFirst = xy.CIEx; nextSecond = xy.CIEy;
                }
                if (!double.IsFinite(nextFirst) || !double.IsFinite(nextSecond)) return;
                targetFirst.Text = nextFirst.ToString("G", CultureInfo.InvariantCulture);
                targetSecond.Text = nextSecond.ToString("G", CultureInfo.InvariantCulture);
            }
            catch (ArgumentException) { /* Keep the other coordinate pair when current input is incomplete. */ }
        }
        SyncCoordinates(x, y, u, v, true);
        SyncCoordinates(whiteX, whiteY, whiteU, whiteV, true);
        void UpdateFields()
        {
            bool uv = inputSpace.SelectedIndex == 1;
            xyFields.IsVisible = !uv; uvFields.IsVisible = uv;
            customFields.IsVisible = source.SelectedIndex == 1 && !uv;
            customUvFields.IsVisible = source.SelectedIndex == 1 && uv;
            standardFields.IsVisible = source.SelectedIndex == 0;
        }
        var plot = new ChromaticityDiagram { Name = "WavelengthDiagram" };
        var uvPlot = new ChromaticityDiagram { Name = "WavelengthUvDiagram", Space = ChromaticityDiagramSpace.UvPrime };
        plot.Clear(SelectedObserver(observer));
        uvPlot.Clear(SelectedObserver(observer));
        void Invalidate()
        {
            calculated = null; result.Invalidate();
            plot.Clear(SelectedObserver(observer)); uvPlot.Clear(SelectedObserver(observer));
        }
        foreach (var box in new[] { x, y, u, v, whiteX, whiteY, whiteU, whiteV })
            box.PropertyChanged += (_, change) => { if (change.Property == TextBox.TextProperty) Invalidate(); };
        light.SelectionChanged += (_, _) => Invalidate();
        observer.SelectionChanged += (_, _) => Invalidate();
        inputSpace.SelectionChanged += (_, _) =>
        {
            bool uv = inputSpace.SelectedIndex == 1;
            SyncCoordinates(uv ? x : u, uv ? y : v, uv ? u : x, uv ? v : y, uv);
            SyncCoordinates(uv ? whiteX : whiteU, uv ? whiteY : whiteV,
                uv ? whiteU : whiteX, uv ? whiteV : whiteY, uv);
            UpdateFields();
            Invalidate();
        };
        source.SelectionChanged += (_, _) =>
        {
            UpdateFields();
            Invalidate();
        };
        void Calculate()
        {
            plot.Clear(SelectedObserver(observer));
            uvPlot.Clear(SelectedObserver(observer));
            result.Run(() =>
            {
                if (inputSpace.SelectedIndex == 1)
                {
                    var sample = new CIEuv
                    {
                        CIEu = ToolCalculations.Number(u.Text ?? "", "样品 u′"),
                        CIEv = ToolCalculations.Number(v.Text ?? "", "样品 v′")
                    };
                    var white = source.SelectedIndex == 1 ? new CIEuv
                    {
                        CIEu = ToolCalculations.Number(whiteU.Text ?? "", "白点 u′"),
                        CIEv = ToolCalculations.Number(whiteV.Text ?? "", "白点 v′")
                    } : null;
                    calculated = WavelengthCalculations.CalculateUv(sample, SelectedObserver(observer), SelectedLight(light), white);
                }
                else
                {
                    var sample = new CIExyY
                    {
                        CIEx = ToolCalculations.Number(x.Text ?? "", "x"),
                        CIEy = ToolCalculations.Number(y.Text ?? "", "y"), CIEY = 100
                    };
                    var white = source.SelectedIndex == 1 ? new CIExyY
                    {
                        CIEx = ToolCalculations.Number(whiteX.Text ?? "", "白点 x"),
                        CIEy = ToolCalculations.Number(whiteY.Text ?? "", "白点 y"), CIEY = 100
                    } : null;
                    calculated = WavelengthCalculations.Calculate(sample, SelectedObserver(observer), SelectedLight(light), white);
                }
                plot.SetData(calculated);
                uvPlot.SetData(calculated);
                return calculated.Table;
            });
        }
        var calculate = Primary("计算波长", Calculate); calculate.Name = "WavelengthCalculate";
        void LoadExample(string exampleX, string exampleY)
        {
            x.Text = exampleX; y.Text = exampleY;
            SyncCoordinates(x, y, u, v, true);
        }
        var columns = new WavelengthLayout { Name = "WavelengthInputsAndDiagram" };
        columns.Children.Add(Stack(
            Text("主波长与补色波长计算", 24, true),
            Note("选择 xy 或 u′v′ 输入样品和自定义白点。样品 Y 固定为 100；Y 不影响波长。"),
            Fields(("输入坐标", inputSpace)), xyFields, uvFields,
            Fields(("观察者", observer), ("白点来源", source)), standardFields, customFields, customUvFields,
            Note("标准光源可选全部 50 条 CIE 光源；白点在其与观察者的共同波段积分，Y = 100。"),
            Actions(Button("载入红色示例", () => LoadExample("0.5253", "0.3485")),
                Button("载入紫色示例", () => LoadExample("0.4", "0.2")), calculate),
            Note("W 为白点，P 为样品。实线连接两点，虚线延伸至边界；D 为正向交点，C 为反向交点。两张图显示同一颜色的主波长与补色波长。"),
            Note("u′v′ 为 CIELuv 对应的色度图。边界包含光谱轨迹与紫边，背景为裁剪到 sRGB 的屏幕近似。"),
            Note("紫色区域没有主波长；某些颜色没有单色补色波长，显示为 —。样品与白点重合时两者均未定义。两位小数表示输出格式，不代表测量精度。")));
        var diagrams = new ResponsiveColumns { Name = "WavelengthDiagrams" };
        diagrams.Children.Add(Stack(Text("CIE xy 色度图", 20, true), plot));
        diagrams.Children.Add(Stack(Text("CIE 1976 u′v′ 色度图", 20, true), uvPlot));
        columns.Children.Add(diagrams);
        return Stack(Card(columns), result);
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
