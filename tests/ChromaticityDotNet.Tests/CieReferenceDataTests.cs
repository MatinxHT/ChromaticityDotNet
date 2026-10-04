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

    [Fact]
    public void CatalogCoversEveryArchivedLightColumnOnItsOriginalGrid()
    {
        var catalog = CieSpectralData.Illuminants;
        Assert.Equal(50, catalog.Count);
        Assert.Equal(catalog.Count, catalog.Select(item => item.Id).Distinct().Count());
        // The two 5 nm FL/LED archives are duplicate versions of the preferred
        // official 1 nm tables. Every other illuminant file and every column is used.
        var sources = Directory.GetFiles(Path.Combine(ReferenceRoot, "illuminants"), "*.csv")
            .Select(Path.GetFileName).Where(name => name is not ("CIE_illum_FLs.csv" or "CIE_illum_LEDs.csv")).OrderBy(name => name);
        Assert.Equal(sources, catalog.Select(item => item.SourceFile).Distinct().OrderBy(name => name));
        foreach (var source in sources)
        {
            var rows = ReadRows("illuminants", source!);
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(ReferenceRoot, "illuminants", Path.ChangeExtension(source!, ".json"))));
            var metadata = json.RootElement.GetProperty("officialMetadata").GetProperty("datatableInfo");
            var columns = metadata.GetProperty("columnHeaders");
            var lights = catalog.Where(item => item.SourceFile == source).ToArray();
            Assert.Equal(columns.GetArrayLength() - 1, lights.Length);
            foreach (var light in lights)
            {
                var column = columns.GetArrayLength() == 2 ? 1 : Enumerable.Range(1, columns.GetArrayLength() - 1)
                    .Single(i => columns[i].GetProperty("title").GetString() == light.Id);
                var spectrum = CieSpectralData.GetIlluminantSpectrum(light.Id);
                AssertColumn(spectrum, rows, column);
                Assert.Equal(spectrum.StartingWavelength, light.StartingWavelength);
                Assert.Equal(spectrum.EndingWavelength, light.EndingWavelength);
                Assert.Equal(spectrum.WavelengthInterval, light.WavelengthInterval);
                Assert.Equal(metadata.GetProperty("dataQuality").GetString() == "approximated", light.IsApproximated);
                spectrum.Spectrums![0] = -1;
                Assert.True(CieSpectralData.GetIlluminantSpectrum(light.Id).Spectrums![0] >= 0);
            }
        }
        Assert.Equal(CieSpectralData.GetIlluminantSpectrum("D50").Spectrums, CieSpectralData.GetIlluminantSpectrum("d50").Spectrums);
        Assert.Throws<ArgumentOutOfRangeException>(() => CieSpectralData.GetIlluminantSpectrum("unknown"));
    }

    private static string[][] ReadRows(string category, string filename) => File.ReadLines(Path.Combine(ReferenceRoot, category, filename))
        .Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.TrimStart('\uFEFF').Split(',')).ToArray();

    private static void AssertColumn(Spectrum actual, string[][] rows, int column, bool zeroUndefinedTail = false)
    {
        Assert.Equal(rows.Length, actual.Spectrums!.Length);
        var step = int.Parse(rows[1][0], CultureInfo.InvariantCulture) - int.Parse(rows[0][0], CultureInfo.InvariantCulture);
        Assert.Equal(step, actual.WavelengthInterval);
        Assert.Equal(int.Parse(rows[0][0], CultureInfo.InvariantCulture), actual.StartingWavelength);
        Assert.Equal(int.Parse(rows[^1][0], CultureInfo.InvariantCulture), actual.EndingWavelength);
        for (int i = 0; i < rows.Length; i++)
        {
            int wavelength = int.Parse(rows[i][0], CultureInfo.InvariantCulture);
            Assert.Equal(actual.StartingWavelength + i * step, wavelength);
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
