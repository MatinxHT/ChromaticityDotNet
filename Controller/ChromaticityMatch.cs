using ChromaticityDotNet.Model;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.Controller
{
    /// <summary>
    /// Provides standard illuminant data, reference white points and color comparison evaluations.
    /// </summary>
    public class ChromaticityMatch
    {
        private static readonly IReadOnlyDictionary<Standardilluminant, IStandardilluminant> _illuminantRegistry =
            new Dictionary<Standardilluminant, IStandardilluminant>
            {
                [Standardilluminant.D65]  = new D65(),
                [Standardilluminant.A]    = new A(),
                [Standardilluminant.CWF]  = new CWF(),
                [Standardilluminant.F7]   = new F7(),
                [Standardilluminant.TL84] = new TL84(),
                [Standardilluminant.U30]  = new U30(),
            };

        /// <summary>Returns cached CIE-derived data for any catalog illuminant.
        /// Preserves the original six concrete classes and the unknown-value D65 fallback.</summary>
        public static IStandardilluminant GetStandardilluminantdata(Standardilluminant illuminant)
        {
            if (_illuminantRegistry.TryGetValue(illuminant, out var data)) return data;
            return Enum.IsDefined(typeof(Standardilluminant), illuminant)
                ? CieStandardIlluminant.Get(illuminant) : _illuminantRegistry[Standardilluminant.D65];
        }

        /// <summary>
        /// Returns the cached CIE reference white integrated on a 1 nm grid over the
        /// illuminant and observer's complete common coverage, normalized to Y = 100.
        /// </summary>
        /// <param name="illuminant">Standard illuminant type</param>
        /// <param name="observer">Standard observer degree</param>
        /// <returns>StandardWhitePoint in choosen illuminant and observer </returns>
        public static CIEXYZ GetStandardWhitePoint(Standardilluminant illuminant, StandardObserver observer)
        {
            IStandardilluminant data = GetStandardilluminantdata(illuminant);

            switch (observer)
            {
                case StandardObserver.Degree2:
                    return data.WhitePoint_Degree2.WhitePointXnYnZn!;
                case StandardObserver.Degree10:
                default:
                    return data.WhitePoint_Degree10.WhitePointXnYnZn!;
            }
        }

        /// <summary>Integrates a catalog illuminant with the selected CIE observer on a 1 nm grid,
        /// normalizing Y to 100 and retaining full precision.</summary>
        /// <remarks>Default range is the intersection of the illuminant's native coverage with 360–830 nm.
        /// Explicit bounds must be covered by both datasets. 5 nm spectra are linearly interpolated;
        /// no extrapolation occurs. Use the same bounds as REFToXYZ for reflectance Lab/Luv.</remarks>
        public static CIEXYZ GetStandardWhitePoint(string illuminantId, StandardObserver observer,
            int? startingWavelength = null, int? endingWavelength = null) =>
            GetStandardWhitePoint(CieSpectralData.GetIlluminantSpectrum(illuminantId), observer,
                startingWavelength, endingWavelength);

        /// <summary>Integrates a supplied illuminant spectrum into an unrounded Y = 100 reference white.</summary>
        public static CIEXYZ GetStandardWhitePoint(Spectrum illuminant, StandardObserver observer,
            int? startingWavelength = null, int? endingWavelength = null) =>
            SpectralCalculations.CalculateWhitePoint(illuminant, observer, startingWavelength, endingWavelength);

        /// <summary>Compares two Lab colors using CIE76, CMC and CIEDE2000, with structured and English evaluations.</summary>
        /// <remarks>Inputs must share the same reference white and observer. All differences describe sample minus
        /// reference. Defaults: CMC 1:1, CIEDE2000 1:1:1; L* &lt; 10 OR C* &lt; 5 is achromatic.
        /// Hue bias is skipped if either color is achromatic or has zero chroma. Otherwise LabToLch supplies h:
        /// its shortest signed difference selects the first main axis ahead of the sample hue in that direction.
        /// A 180-degree tie selects increasing hue. No overall acceptance/perceptibility limits are assumed.
        /// Inputs and options are not modified. Returned snapshots are read-only.</remarks>
        public static ColorComparisonResult CompareColors(CIELAB reference, CIELAB sample, ColorComparisonOptions? options = null)
        {
            if (reference is null) throw new ArgumentNullException(nameof(reference));
            if (sample is null) throw new ArgumentNullException(nameof(sample));
            var parameters = new ColorComparisonParameters(options ?? new ColorComparisonOptions());
            // Copy mutable inputs once so all calculations and returned snapshots use the same coordinates.
            var referenceLab = new CIELAB(reference.CIEL, reference.CIEA, reference.CIEB);
            var sampleLab = new CIELAB(sample.CIEL, sample.CIEA, sample.CIEB);
            var referenceLch = ChromaticityConversion.LabToLch(referenceLab);
            var sampleLch = ChromaticityConversion.LabToLch(sampleLab);
            bool referenceAchromatic = IsAchromatic(referenceLch, parameters);
            bool sampleAchromatic = IsAchromatic(sampleLch, parameters);
            bool hueApplicable = !referenceAchromatic && !sampleAchromatic && referenceLch.CIEC > 0 && sampleLch.CIEC > 0;

            double de76 = ChromaticityDeltaEFormulations.DeltaE1976(referenceLab, sampleLab);
            double cmc = ChromaticityDeltaEFormulations.DeltaEcmc(referenceLab, sampleLab, parameters.Cmc.L, parameters.Cmc.C);
            double de00 = ChromaticityDeltaEFormulations.DeltaE2000(referenceLab, sampleLab,
                parameters.Ciede2000.KL, parameters.Ciede2000.KC, parameters.Ciede2000.KH).DeltaE;
            RequireFiniteComparison(de76, cmc, de00);

            double deltaL = sampleLab.CIEL - referenceLab.CIEL;
            double deltaA = sampleLab.CIEA - referenceLab.CIEA;
            double deltaB = sampleLab.CIEB - referenceLab.CIEB;
            double deltaC = sampleLch.CIEC - referenceLch.CIEC;
            double? deltaHue = hueApplicable ? SignedHueDifference(referenceLch.CIEH, sampleLch.CIEH) : null;
            var differences = new LabColorDifferences(deltaL, deltaA, deltaB, deltaC, deltaHue);
            var lightness = EvaluateAxis(deltaL, parameters.LightnessTolerance,
                "Darker", "Lighter", "No change");
            var chroma = EvaluateAxis(deltaC, parameters.ChromaTolerance,
                "Lower chroma", "Higher chroma", "No change");
            var hue = EvaluateHue(sampleLch.CIEH, deltaHue, parameters);
            var neutrality = new ColorNeutralityEvaluation(referenceAchromatic, sampleAchromatic);
            double overallDelta = parameters.EvaluationFormula switch
            {
                ComparisonFormula.Cie76 => de76,
                ComparisonFormula.Cmc => cmc,
                _ => de00
            };
            var overall = EvaluateOverall(overallDelta, parameters);
            var evaluation = new ColorComparisonEvaluation(lightness, chroma, hue, neutrality, overall);
            return new ColorComparisonResult(
                new ColorComparisonColor(referenceLab, referenceLch, referenceAchromatic),
                new ColorComparisonColor(sampleLab, sampleLch, sampleAchromatic),
                de76, cmc, de00, differences, evaluation, parameters);
        }

        private static bool IsAchromatic(CIELCH lch, ColorComparisonParameters parameters) =>
            lch.CIEL < parameters.AchromaticLightnessThreshold || lch.CIEC < parameters.AchromaticChromaThreshold;

        private static double SignedHueDifference(double referenceHue, double sampleHue)
        {
            double difference = sampleHue - referenceHue;
            if (difference > 180) difference -= 360;
            if (difference <= -180) difference += 360;
            return difference;
        }

        private static ColorAxisEvaluation EvaluateAxis(double difference, double tolerance, string lower, string higher, string unchanged)
        {
            if (Math.Abs(difference) <= tolerance) return new ColorAxisEvaluation(ColorDifferenceDirection.Unchanged, unchanged);
            return difference < 0
                ? new ColorAxisEvaluation(ColorDifferenceDirection.Lower, lower)
                : new ColorAxisEvaluation(ColorDifferenceDirection.Higher, higher);
        }

        private static HueShiftEvaluation EvaluateHue(double sampleHue, double? difference, ColorComparisonParameters parameters)
        {
            if (!difference.HasValue)
                return new HueShiftEvaluation(HueShiftStatus.NotApplicable, HueShiftDirection.None, null, null,
                    "Not applicable");

            int mainIndex = 0;
            double nearestDistance = double.PositiveInfinity;
            for (int i = 0; i < parameters.HueAxes.Count; i++)
            {
                double distance = Math.Abs(SignedHueDifference(sampleHue, parameters.HueAxes[i].HueAngleDegrees));
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    mainIndex = i;
                }
            }
            var mainAxis = parameters.HueAxes[mainIndex];
            if (Math.Abs(difference.Value) <= parameters.HueAngleToleranceDegrees)
                return new HueShiftEvaluation(HueShiftStatus.Unchanged, HueShiftDirection.None, mainAxis, null,
                    "No change");

            bool increasing = difference.Value > 0;
            int targetIndex = 0;
            double nearestInDirection = double.PositiveInfinity;
            for (int i = 0; i < parameters.HueAxes.Count; i++)
            {
                double distance = increasing
                    ? parameters.HueAxes[i].HueAngleDegrees - sampleHue
                    : sampleHue - parameters.HueAxes[i].HueAngleDegrees;
                // Skip an axis at the sample hue, including tiny conversion round-off.
                if (distance <= 1e-10) distance += 360;
                if (distance < nearestInDirection)
                {
                    nearestInDirection = distance;
                    targetIndex = i;
                }
            }
            var targetAxis = parameters.HueAxes[targetIndex];
            return new HueShiftEvaluation(HueShiftStatus.Shifted,
                increasing ? HueShiftDirection.Increasing : HueShiftDirection.Decreasing,
                mainAxis, targetAxis, targetAxis.ShiftDescriptionEnglish);
        }

        private static OverallColorEvaluation EvaluateOverall(double deltaE, ColorComparisonParameters parameters)
        {
            var acceptance = !parameters.AcceptanceTolerance.HasValue ? ColorAcceptanceStatus.NotEvaluated :
                deltaE <= parameters.AcceptanceTolerance.Value ? ColorAcceptanceStatus.WithinTolerance : ColorAcceptanceStatus.OutsideTolerance;
            var perceptibility = !parameters.PerceptibilityThreshold.HasValue ? ColorPerceptibilityStatus.NotEvaluated :
                deltaE < parameters.PerceptibilityThreshold.Value ? ColorPerceptibilityStatus.BelowThreshold : ColorPerceptibilityStatus.AtOrAboveThreshold;
            string acceptanceText = acceptance switch
            {
                ColorAcceptanceStatus.WithinTolerance => "Within tolerance",
                ColorAcceptanceStatus.OutsideTolerance => "Outside tolerance",
                _ => ""
            };
            string perceptibilityText = perceptibility switch
            {
                ColorPerceptibilityStatus.BelowThreshold => "Below perceptibility threshold",
                ColorPerceptibilityStatus.AtOrAboveThreshold => "At or above perceptibility threshold",
                _ => ""
            };
            var comments = new[] { acceptanceText, perceptibilityText }.Where(value => value.Length > 0).ToArray();
            return new OverallColorEvaluation(parameters.EvaluationFormula, deltaE, acceptance, perceptibility,
                comments.Length == 0 ? "Not evaluated" : string.Join("; ", comments));
        }

        private static void RequireFiniteComparison(params double[] values)
        {
            if (values.Any(value => double.IsNaN(value) || double.IsInfinity(value)))
                throw new ArgumentException("Color coordinates or weights exceed the finite color-difference calculation range.");
        }
    }
}
