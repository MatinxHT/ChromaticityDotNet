using System.Globalization;
using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace Chromaticity.Tools.Services;

public sealed record ChromaticityCoordinates(double X, double Y, double Luminance)
{
    public CIExyY ToXyY() => new() { CIEx = X, CIEy = Y, CIEY = Luminance };
}

public sealed record WavelengthCalculation(CalculationTable Table, ChromaticityCoordinates Sample,
    ChromaticityCoordinates White, StandardObserver Observer, ChromaticityWavelengthResult Wavelengths);

/// <summary>Input snapshots and result presentation for tool 06. Wavelengths are computed by the library.</summary>
public static class WavelengthCalculations
{
    /// <summary>Accepts CIE 1976 u'v' coordinates, preserving the entered values in exports.</summary>
    public static WavelengthCalculation CalculateUv(CIEuv sample, StandardObserver observer,
        string illuminant, CIEuv? customWhite = null)
    {
        ArgumentNullException.ThrowIfNull(sample);
        var point = UvToXy(sample);
        var white = customWhite is null ? null : UvToXy(customWhite, isWhite: true);
        WavelengthCalculation calculation;
        try
        {
            calculation = Calculate(point, observer, illuminant, white);
        }
        catch (ArgumentException ex) when (ex.InnerException is ArgumentOutOfRangeException geometry
            && geometry.ParamName is "color" or "whitePoint")
        {
            throw new ArgumentException(geometry.ParamName == "color"
                ? "样品 u′v′ 必须位于所选观察者的色域内或边界上。"
                : "参考白点 u′v′ 必须严格位于所选观察者的色域内部。", ex);
        }
        var projection = new ChromaticityDiagramProjection(ChromaticityDiagramSpace.UvPrime);
        var whiteUv = customWhite is null ? projection.Project(calculation.White.X, calculation.White.Y)
            : new ChromaticityDiagramPoint(customWhite.CIEu, customWhite.CIEv);
        static string Coordinate(double value) => value.ToString("G", CultureInfo.InvariantCulture);
        var row = calculation.Table.Rows[0];
        var table = new CalculationTable(
            ["样品 u′", "样品 v′", "样品 Y", "参考白点", "白点 u′", "白点 v′", "观察者",
                "主波长 / nm", "补色波长 / nm", "样品 x", "样品 y", "白点 x", "白点 y", "说明"],
            [[Coordinate(sample.CIEu), Coordinate(sample.CIEv), "100", row[3],
                Coordinate(whiteUv.X), Coordinate(whiteUv.Y), row[6], row[7], row[8],
                row[0], row[1], row[4], row[5], row[^1]]],
            "输入坐标：CIE 1976 u′v′；转换为 xy 后计算，原始 u′v′ 坐标保留在导出中。\n" + calculation.Table.Conditions);
        return calculation with { Table = table };
    }

    public static CIExyY UvToXy(CIEuv coordinates, bool isWhite = false)
    {
        ArgumentNullException.ThrowIfNull(coordinates);
        double u = coordinates.CIEu, v = coordinates.CIEv;
        double denominator = 6.0 * u - 16.0 * v + 12.0;
        if (!double.IsFinite(u) || !double.IsFinite(v) || u < 0 || v <= 0
            || !double.IsFinite(denominator) || denominator <= 0)
            throw new ArgumentException(isWhite
                ? "白点 u′、v′ 必须为有限数字，u′ ≥ 0、v′ > 0，且 6u′ − 16v′ + 12 > 0。"
                : "样品 u′、v′ 必须为有限数字，u′ ≥ 0、v′ > 0，且 6u′ − 16v′ + 12 > 0。");
        var xy = new ChromaticityDiagramProjection(ChromaticityDiagramSpace.UvPrime).ToXy(u, v);
        return new CIExyY { CIEx = xy.X, CIEy = xy.Y, CIEY = 100 };
    }

    public static ChromaticityCoordinates StandardWhite(Standardilluminant illuminant, StandardObserver observer)
        => StandardWhite(ToolCalculations.IlluminantId(illuminant), observer);

    public static ChromaticityCoordinates StandardWhite(string illuminant, StandardObserver observer)
    {
        var xyz = ChromaticityMatch.GetStandardWhitePoint(illuminant, observer);
        double sum = xyz.CIEX + xyz.CIEY + xyz.CIEZ;
        return new(xyz.CIEX / sum, xyz.CIEY / sum, xyz.CIEY);
    }

    public static WavelengthCalculation Calculate(CIExyY sample, StandardObserver observer,
        Standardilluminant illuminant, CIExyY? customWhite = null) =>
        Calculate(sample, observer, ToolCalculations.IlluminantId(illuminant), customWhite);

    public static WavelengthCalculation Calculate(CIExyY sample, StandardObserver observer,
        string illuminant, CIExyY? customWhite = null)
    {
        ArgumentNullException.ThrowIfNull(sample);
        var point = new ChromaticityCoordinates(sample.CIEx, sample.CIEy, sample.CIEY);
        var white = customWhite is null ? StandardWhite(illuminant, observer)
            : new ChromaticityCoordinates(customWhite.CIEx, customWhite.CIEy, customWhite.CIEY);
        ChromaticityWavelengthResult result;
        try
        {
            result = ChromaticityConversion.xyYToWavelengths(point.ToXyY(), observer, white.ToXyY());
        }
        catch (ArgumentOutOfRangeException ex) when (ex.ParamName is "color" or "whitePoint")
        {
            throw new ArgumentException(ex.ParamName == "color"
                ? "样品 xy 必须位于所选观察者的马蹄色域内或边界上，x、y、Y 必须有限且 Y ≥ 0。"
                : "参考白点 xy 必须严格位于所选观察者的马蹄色域内部。", ex);
        }
        string description = result.IsAchromatic ? "样品与白点重合，波长未定义。"
            : result.DominantWavelength is null ? "紫色区域：主波长未定义，使用补色波长。"
            : result.ComplementaryWavelength is null ? "反向落在紫边：补色波长未定义。"
            : "主波长和补色波长均可定义。";
        string reference = customWhite is null ? illuminant.ToString() : "自定义白点";
        string observerName = observer == StandardObserver.Degree2 ? "2° · CIE 1931" : "10° · CIE 1964";
        static string Coordinate(double value) => value.ToString("G", CultureInfo.InvariantCulture);
        string whiteMethod = "";
        if (customWhite is null)
        {
            var info = CieSpectralData.Illuminants.First(item => string.Equals(item.Id, illuminant, StringComparison.OrdinalIgnoreCase));
            whiteMethod = $" · 光谱积分白点 {Math.Max(360, info.StartingWavelength)}–{Math.Min(830, info.EndingWavelength)} nm，Y = 100";
        }
        string whiteX = ToolCalculations.F(white.X), whiteY = ToolCalculations.F(white.Y);
        var table = new CalculationTable(
            ["样品 x", "样品 y", "样品 Y", "参考白点", "白点 x", "白点 y", "观察者", "主波长 / nm", "补色波长 / nm", "说明"],
            [[Coordinate(point.X), Coordinate(point.Y), Coordinate(point.Luminance), reference,
                whiteX, whiteY, observerName,
                Format(result.DominantWavelength), Format(result.ComplementaryWavelength), description]],
            $"{reference} / {observerName} · 白点 xy = ({whiteX}, {whiteY}){whiteMethod}\n" +
            "波长单位为 nm，显示两位小数。白点坐标显示四位小数。Y 不影响波长；计算和图形使用未舍入坐标。");
        return new(table, point, white, observer, result);
    }

    public static string Format(double? wavelength) => wavelength?.ToString("F2", CultureInfo.InvariantCulture) ?? "—";
}
