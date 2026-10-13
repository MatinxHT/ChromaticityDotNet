using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.Diagrams;

internal static class DiagramValidation
{
    internal static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    internal static void Finite(double value, string name)
    {
        if (!IsFinite(value)) throw new ArgumentOutOfRangeException(name, "A finite coordinate is required.");
    }
    internal static void Observer(StandardObserver observer)
    {
        if (observer != StandardObserver.Degree2 && observer != StandardObserver.Degree10)
            throw new ArgumentOutOfRangeException(nameof(observer));
    }
}
