using Chromaticity.Tools.Services;
using Xunit;

namespace Chromaticity.Tools.Tests;

public class LanguageTests
{
    [Theory]
    [InlineData("色差计算", "Color difference")]
    [InlineData("每侧 4 级", "4 grades per side")]
    [InlineData("已复制 3 行，可粘贴到 Excel。", "Copied 3 rows. You can paste them into Excel.")]
    [InlineData("错误：第 2 行第 3 列 必须是有限数字（小数点使用 .）。",
        "Error: Row 2, column 3 must be a finite number (use . as the decimal separator).")]
    [InlineData("请输入一个标样，恰好三列 L*、a*、b*。",
        "Enter one Standard with exactly three columns: L*, a*, b*.")]
    [InlineData("CMC 2:1 · 一级 ΔE = 1.5000 · 等级计算：四舍五入",
        "CMC 2:1 · ΔE per grade = 1.5000 · Grade rounding: Round to nearest")]
    [InlineData("+1 级", "Grade +1")]
    [InlineData("第 2 行有空单元格。\n请先输入数据，或载入示例。",
        "Row 2 contains an empty cell.\nEnter data or load an example first.")]
    public void TranslatesDynamicAndNestedMessages(string chinese, string english)
    {
        Assert.Equal(english, UiLanguage.Translate(chinese, UiLanguage.English));
        Assert.Equal(chinese, UiLanguage.Translate(chinese, UiLanguage.Chinese));
    }

    [Fact]
    public void IlluminantConditionsTranslateEverySentenceAndKeepNumbers()
    {
        var query = ToolCalculations.QueryIlluminant("D65", 10, 380, 780);
        var translated = UiLanguage.Translate(query.Table.Conditions, UiLanguage.English);
        Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", translated);
        Assert.Contains("380–780 nm", translated);
        Assert.Contains("41 samples", translated);
    }

    [Theory]
    [InlineData("50,20,-30", false)]
    [InlineData("0,0,0", true)]
    public void GradeMessagesTranslateWithoutChangingCalculationValues(string lab, bool neutral)
    {
        var chart = ColorGradeCalculations.Generate(lab, ColorGradeFormula.Cmc, 4);
        foreach (var row in chart.Table.Rows)
        {
            foreach (var cell in row)
                Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", UiLanguage.Translate(cell, UiLanguage.English));
            Assert.Equal(row[2], UiLanguage.Translate(row[2], UiLanguage.English));
        }
        if (neutral) Assert.Contains(chart.Table.Rows, row => row.Contains("中性色无确定色相，无法展开色相。"));
    }

    [Fact]
    public void CoordinatesAndTechnicalIdentifiersStayUnchanged()
    {
        const string value = "D65 / 2° · CIEDE2000 kL:kC:kH=1:1:1 · #20A090 · 50.0000";
        Assert.Equal(value, UiLanguage.Translate(value, UiLanguage.English));
    }

    [Fact]
    public void EnglishExportsTranslateHeadersAndKeepTheOriginalSchemaAndNumbers()
    {
        var original = ToolCalculations.Difference("50,20,-30", "52,18,-28", false, 1, 1, 1, 2, 1);
        var translated = UiLanguage.TranslateTable(original, UiLanguage.English);
        Assert.Equal("序号", original.Headers[0]);
        Assert.Equal("No.", translated.Headers[0]);
        Assert.Equal(original.Rows[0], translated.Rows[0]);
        Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", translated.ToCsv());
        Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", translated.ToTsv());
    }
}
