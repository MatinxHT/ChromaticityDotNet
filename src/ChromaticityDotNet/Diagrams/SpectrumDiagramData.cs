using static ChromaticityDotNet.Model.DataModel;

namespace ChromaticityDotNet.Diagrams;

/// <summary>Immutable spectral samples on a uniform or irregular wavelength grid, in nanometres.
/// Nonfinite values represent gaps; selection skips them and never interpolates measurements.</summary>
public sealed class SpectrumDiagramData
{
    public IReadOnlyList<double> Wavelengths { get; }
    public IReadOnlyList<double> Values { get; }
    public int Count => Values.Count;
    public double Start => Wavelengths[0];
    public double End => Wavelengths[Count - 1];

    public SpectrumDiagramData(IEnumerable<double> wavelengths, IEnumerable<double> values)
    {
        if (wavelengths is null) throw new ArgumentNullException(nameof(wavelengths));
        if (values is null) throw new ArgumentNullException(nameof(values));
        var grid = wavelengths.ToArray(); var samples = values.ToArray();
        if (grid.Length == 0 || grid.Length != samples.Length)
            throw new ArgumentException("Wavelength and value arrays must have the same nonzero length.");
        for (int i = 0; i < grid.Length; i++)
        {
            DiagramValidation.Finite(grid[i], nameof(wavelengths));
            if (i > 0 && grid[i] <= grid[i - 1])
                throw new ArgumentException("Wavelengths must be strictly increasing.", nameof(wavelengths));
        }
        Wavelengths = Array.AsReadOnly(grid); Values = Array.AsReadOnly(samples);
    }

    public static SpectrumDiagramData FromUniform(IEnumerable<double> values, double start, double step)
    {
        if (values is null) throw new ArgumentNullException(nameof(values));
        DiagramValidation.Finite(start, nameof(start)); DiagramValidation.Finite(step, nameof(step));
        if (step <= 0) throw new ArgumentOutOfRangeException(nameof(step));
        var samples = values.ToArray();
        return new(Enumerable.Range(0, samples.Length).Select(i => start + i * step), samples);
    }

    public static SpectrumDiagramData FromSpectrum(Spectrum spectrum)
    {
        if (spectrum is null) throw new ArgumentNullException(nameof(spectrum));
        var result = FromUniform(spectrum.Spectrums ?? throw new ArgumentException("Spectrum values are required.", nameof(spectrum)),
            spectrum.StartingWavelength, spectrum.WavelengthInterval);
        if (result.End != spectrum.EndingWavelength)
            throw new ArgumentException("Spectrum range and sample count disagree.", nameof(spectrum));
        return result;
    }

    /// <summary>Index of the nearest finite sample; ties select the lower wavelength. -1 if all values are missing.</summary>
    public int FindNearestIndex(double wavelength)
    {
        DiagramValidation.Finite(wavelength, nameof(wavelength));
        int nearest = -1; double distance = double.PositiveInfinity;
        for (int i = 0; i < Count; i++)
        {
            if (!DiagramValidation.IsFinite(Values[i])) continue;
            double next = Math.Abs(Wavelengths[i] - wavelength);
            if (next >= distance) continue;
            nearest = i; distance = next;
        }
        return nearest;
    }
}
