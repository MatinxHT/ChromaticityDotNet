using ChromaticityDotNet.Model;
using static ChromaticityDotNet.Model.ColorChips;
using static ChromaticityDotNet.Model.ConfigEnum;
using Xunit;

namespace ChromaticityDotNet.Tests;

public class ColorChipsTests
{
    [Fact]
    public void Gbt250MetadataAndThresholdsAreComplete()
    {
        IColorStandard standard = new GBT250Info();

        Assert.Equal("纺织品色牢度试验评定变色用灰色样卡标准", standard.StandardName);
        Assert.Equal(new[] { "5", "4-5", "4", "3-4", "3", "2-3", "2", "1-2", "1" }, standard.FastnessRank);
        Assert.Equal(new[] { 0.0, 0.8, 1.7, 2.5, 3.4, 4.8, 6.8, 9.6, 13.6 }, standard.DeltaE);
        Assert.Equal(new[] { 0.2, 0.2, 0.3, 0.35, 0.4, 0.5, 0.6, 0.7, 1.0 }, standard.DeltaEOffset);
        Assert.Equal(DeltaEType.DeltaE1976, standard.DeltaEFormula);
        Assert.Equal(2008, standard.GBVersion);
    }

    [Fact]
    public void Gbt251MetadataAndThresholdsAreComplete()
    {
        IColorStandard standard = new GBT251Info();

        Assert.Equal("纺织品色牢度试验评定沾色用灰色样卡标准", standard.StandardName);
        Assert.Equal(new[] { "5", "4-5", "4", "3-4", "3", "2-3", "2", "1-2", "1" }, standard.FastnessRank);
        Assert.Equal(new[] { 0.0, 2.2, 4.3, 6.0, 8.5, 12.0, 16.9, 24.0, 34.1 }, standard.DeltaE);
        Assert.Equal(new[] { 0.2, 0.3, 0.3, 0.4, 0.5, 0.7, 1.0, 1.5, 2.0 }, standard.DeltaEOffset);
        Assert.Equal(DeltaEType.DeltaE1976, standard.DeltaEFormula);
        Assert.Equal(2008, standard.GBVersion);
    }

    [Fact]
    public void ColorFastnessEnumContainsBothSupportedStandards()
    {
        Assert.Equal(
            new[] { ColorFastnessChinaStandard.GBT250, ColorFastnessChinaStandard.GBT251 },
            Enum.GetValues<ColorFastnessChinaStandard>());
    }
}
