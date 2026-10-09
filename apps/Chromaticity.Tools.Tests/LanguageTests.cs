using Chromaticity.Tools.Services;
using Xunit;

namespace Chromaticity.Tools.Tests;

public class LanguageTests
{
    [Theory]
    [InlineData("色差计算", "Color difference")]
    [InlineData("无彩色 L* 阈值", "Achromatic L* threshold")]
    [InlineData("无彩色 C* 阈值", "Achromatic C* threshold")]
    [InlineData("色相偏色", "Hue bias")]
    [InlineData("偏红", "More reddish")]
    [InlineData("偏黄", "More yellowish")]
    [InlineData("偏红 ← 标样 → 偏黄", "More reddish ← Standard → More yellowish")]
    [InlineData("下载数据模板", "Download data template")]
    [InlineData("导入 CSV", "Import CSV")]
    [InlineData("导入失败：第 4 行需要恰好 3 列数据。", "Import failed: Row 4 needs exactly 3 data columns.")]
    [InlineData("自动比较：1 行标准 → 3 行样品（一对多）。", "Automatic comparison: 1 standard → 3 samples (one-to-many).")]
    [InlineData("自动比较：2 行标准与样品逐行配对（一对一）。", "Automatic comparison: 2 standard and sample rows are paired (one-to-one).")]
    [InlineData("第 2 行标准 Lab的三列必须同时填写或同时留空。", "Row 2: all three Standard Lab columns must be filled or left blank together.")]
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
    public void EnglishGradeEvaluationAndNewUiNotesHaveNoUntranslatedChinese()
    {
        var result = ColorGradeCalculations.Analyze("50,30,20", "53,32,18", new(ColorGradeFormula.Cmc));
        Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", UiLanguage.TranslateTable(result.Table, UiLanguage.English).ToCsv());
        foreach (var message in new[]
        {
            "L* 低于阈值或 C* 低于阈值，满足任一项即按无彩色评价；标样或样品为无彩色时不评价色相偏色。阈值允许设为 0。",
            "偏红 ← 标样 → 偏黄",
            "无彩色判定：均为有彩色",
            "HSL/HSV 基于显示的 sRGB 值；灰色的 H 使用 0 作为占位值。"
        }) Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", UiLanguage.Translate(message, UiLanguage.English));
    }

    [Fact]
    public void EnglishExportsTranslateHeadersAndKeepTheOriginalSchemaAndNumbers()
    {
        var original = ToolCalculations.Difference("50,20,-30", "52,18,-28", false, 1, 1, 1, 2, 1);
        var translated = UiLanguage.TranslateTable(original, UiLanguage.English);
        Assert.Equal("序号", original.Headers[0]);
        Assert.Equal("No.", translated.Headers[0]);
        Assert.Equal(new[] { "Standard L*", "Standard a*", "Standard b*", "Sample L*", "Sample a*", "Sample b*" }, translated.Headers[3..9]);
        Assert.Equal(original.Rows[0][..17], translated.Rows[0][..17]);
        Assert.Equal(new[] { "Lighter", "Lower chroma", "More bluish", "Both chromatic" }, translated.Rows[0][17..]);
        Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", translated.ToCsv());
        Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", translated.ToTsv());
    }
}
