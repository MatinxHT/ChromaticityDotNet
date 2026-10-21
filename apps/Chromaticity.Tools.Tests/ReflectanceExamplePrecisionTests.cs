using System.Globalization;
using Chromaticity.Tools.Services;
using Xunit;

namespace Chromaticity.Tools.Tests;

public class ReflectanceExamplePrecisionTests
{
    [Fact]
    public void DefaultAndTextileExamplesUseEquivalentPercentAndFractionValues()
    {
        foreach (var example in new Func<bool, string>[] { ReflectanceExamples.Default,
            TextileMetamerismExamples.GetStandard, TextileMetamerismExamples.GetMetamericSample,
            TextileMetamerismExamples.GetDifferentSample })
        {
            var percent = ToolCalculations.ParseRows(example(false));
            var fraction = ToolCalculations.ParseRows(example(true));
            Assert.Equal(41, percent.Length); Assert.Equal(percent.Length, fraction.Length);
            for (var i = 0; i < percent.Length; i++)
            {
                Assert.Equal(percent[i][0], fraction[i][0]);
                Assert.Matches(@"^\d+\.\d{2}$", percent[i][1]);
                Assert.Matches(@"^\d+\.\d{4}$", fraction[i][1]);
                Assert.Equal(decimal.Parse(percent[i][1], CultureInfo.InvariantCulture) / 100,
                    decimal.Parse(fraction[i][1], CultureInfo.InvariantCulture));
            }
        }
        Assert.Equal(ReflectanceExamples.Default(), TextileMetamerismExamples.Standard);
    }

    [Theory]
    [InlineData(false, "18.00")]
    [InlineData(true, "0.1800")]
    public void CsvTemplateUsesTheSameExamplePrecision(bool fraction, string expected)
    {
        var template = CsvDataFiles.SpectrumTemplate(380, 780, 10, fraction);
        var rows = ToolCalculations.ParseRows(CsvDataFiles.Normalize(template.Csv, CsvInputKind.Spectrum));
        Assert.All(rows, row => Assert.Equal(expected, row[1]));
    }

    [Fact]
    public void ImportedMeasuredSpectraKeepTheirSuppliedPrecision()
    {
        const string measured = "380,18.12345678\n390,20.98765432";
        Assert.Equal(measured, CsvDataFiles.Normalize("wavelength,reflectance\n" + measured, CsvInputKind.Spectrum));
    }
}
