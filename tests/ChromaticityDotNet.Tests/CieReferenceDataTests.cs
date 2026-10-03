using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using ChromaticityDotNet.Model;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;
using Xunit;

namespace ChromaticityDotNet.Tests;

public class CieReferenceDataTests
{
    private static string ReferenceRoot => Path.Combine(AppContext.BaseDirectory, "reference");

    [Fact]
    public void AllArchivedCsvsMatchRecordedAndOfficialChecksums()
    {
        string[] manifests = Directory.GetFiles(ReferenceRoot, "*.json", SearchOption.AllDirectories);
        Assert.Equal(39, manifests.Length);
        foreach (string path in manifests)
        {
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            var root = json.RootElement;
            byte[] csv = File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(path)!, root.GetProperty("csvFile").GetString()!));
            Assert.Equal(root.GetProperty("csvSha256").GetString(), Convert.ToHexString(SHA256.HashData(csv)).ToLowerInvariant());
            foreach (var checksum in root.GetProperty("officialMetadata").GetProperty("checksums").EnumerateArray())
            {
                byte[] hash = checksum.GetProperty("hashMethod").GetString() switch
                {
                    "md5" => MD5.HashData(csv),
                    "sha256" => SHA256.HashData(csv),
                    var method => throw new InvalidOperationException("Unknown checksum: " + method)
                };
                Assert.Equal(checksum.GetProperty("checksum").GetString(), Convert.ToHexString(hash).ToLowerInvariant());
            }
            Assert.StartsWith("https://", root.GetProperty("sourcePage").GetString());
            Assert.StartsWith("https://files.cie.co.at/", root.GetProperty("csvUrl").GetString());
            Assert.StartsWith("https://files.cie.co.at/", root.GetProperty("metadataUrl").GetString());
        }
    }

    [Theory]
    [InlineData(StandardObserver.Degree2, "CIE_xyz_1931_2deg.csv")]
    [InlineData(StandardObserver.Degree10, "CIE_xyz_1964_10deg.csv")]
    public void EveryObserverSampleMatchesOfficialCsv(StandardObserver observer, string filename)
    {
        var (x, y, z) = CieSpectralData.GetColorMatchingFunctions(observer);
        var rows = ReadRows("observers", filename);
        AssertColumn(x, rows, 1);
        AssertColumn(y, rows, 2);
        AssertColumn(z, rows, 3, zeroUndefinedTail: observer == StandardObserver.Degree10);
    }

    [Theory]
    [InlineData(Standardilluminant.D65, "CIE_std_illum_D65.csv", 1)]
    [InlineData(Standardilluminant.A, "CIE_std_illum_A_1nm.csv", 1)]
    [InlineData(Standardilluminant.CWF, "CIE_illum_FLs_1nm.csv", 2)]
    [InlineData(Standardilluminant.F7, "CIE_illum_FLs_1nm.csv", 7)]
    [InlineData(Standardilluminant.TL84, "CIE_illum_FLs_1nm.csv", 11)]
    [InlineData(Standardilluminant.U30, "CIE_illum_FLs_1nm.csv", 12)]
    public void EveryIlluminantSampleMatchesOfficialCsv(Standardilluminant illuminant, string filename, int column)
    {
        AssertColumn(CieSpectralData.GetIlluminantSpectrum(illuminant), ReadRows("illuminants", filename), column);
    }

    [Fact]
    public void ReturnedSpectraCannotMutateReferenceConstants()
    {
        var first = CieSpectralData.GetIlluminantSpectrum(Standardilluminant.D65);
        double expected = first.Spectrums![0];
        first.Spectrums[0] = -1;
        Assert.Equal(expected, CieSpectralData.GetIlluminantSpectrum(Standardilluminant.D65).Spectrums![0]);
        var functions = CieSpectralData.GetColorMatchingFunctions(StandardObserver.Degree2);
        functions.X.Spectrums![0] = -1;
        Assert.True(CieSpectralData.GetColorMatchingFunctions(StandardObserver.Degree2).X.Spectrums![0] >= 0);
    }

    private static string[][] ReadRows(string category, string filename) => File.ReadLines(Path.Combine(ReferenceRoot, category, filename))
        .Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Split(',')).ToArray();

    private static void AssertColumn(Spectrum actual, string[][] rows, int column, bool zeroUndefinedTail = false)
    {
        Assert.Equal(rows.Length, actual.Spectrums!.Length);
        Assert.Equal(1, actual.WavelengthInterval);
        Assert.Equal(int.Parse(rows[0][0], CultureInfo.InvariantCulture), actual.StartingWavelength);
        Assert.Equal(int.Parse(rows[^1][0], CultureInfo.InvariantCulture), actual.EndingWavelength);
        for (int i = 0; i < rows.Length; i++)
        {
            int wavelength = int.Parse(rows[i][0], CultureInfo.InvariantCulture);
            Assert.Equal(actual.StartingWavelength + i, wavelength);
            double expected = double.Parse(rows[i][column], CultureInfo.InvariantCulture);
            if (double.IsNaN(expected))
            {
                Assert.True(zeroUndefinedTail && wavelength >= 560);
                expected = 0;
            }
            Assert.Equal(expected, actual.Spectrums[i]);
        }
    }
}
