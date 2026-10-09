using static ChromaticityDotNet.Model.DataModel;

namespace ChromaticityDotNet.Model
{
    public enum ComparisonFormula { Cie76, Cmc, Ciede2000 }
    public enum ColorDifferenceDirection { Lower, Unchanged, Higher }
    public enum HueShiftStatus { NotApplicable, Unchanged, Shifted }
    public enum HueShiftDirection { None, Decreasing, Increasing }
    public enum ColorAcceptanceStatus { NotEvaluated, WithinTolerance, OutsideTolerance }
    public enum ColorPerceptibilityStatus { NotEvaluated, BelowThreshold, AtOrAboveThreshold }

    /// <summary>CMC lightness/chroma factors. Values are used as supplied, without ratio normalization.</summary>
    public sealed class CmcParameters
    {
        public double L { get; }
        public double C { get; }

        public CmcParameters(double l = 1, double c = 1)
        {
            L = ColorComparisonValidation.Positive(l, nameof(l));
            C = ColorComparisonValidation.Positive(c, nameof(c));
        }
    }

    /// <summary>CIEDE2000 parametric factors, used without ratio normalization.</summary>
    public sealed class Ciede2000Parameters
    {
        public double KL { get; }
        public double KC { get; }
        public double KH { get; }

        public Ciede2000Parameters(double kL = 1, double kC = 1, double kH = 1)
        {
            KL = ColorComparisonValidation.Positive(kL, nameof(kL));
            KC = ColorComparisonValidation.Positive(kC, nameof(kC));
            KH = ColorComparisonValidation.Positive(kH, nameof(kH));
        }
    }

    /// <summary>A configurable main hue axis with a stable name and one English bias phrase.</summary>
    public sealed class HueAxis
    {
        public string Name { get; }
        public double HueAngleDegrees { get; }
        /// <summary>A single phrase such as "More reddish", without a trailing full stop.</summary>
        public string ShiftDescriptionEnglish { get; }

        public HueAxis(string name, double hueAngleDegrees, string shiftDescriptionEnglish)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A hue axis requires a name.", nameof(name));
            ColorComparisonValidation.NonNegative(hueAngleDegrees, nameof(hueAngleDegrees));
            if (hueAngleDegrees >= 360)
                throw new ArgumentOutOfRangeException(nameof(hueAngleDegrees), "Hue axes must be in [0, 360) degrees.");
            if (string.IsNullOrWhiteSpace(shiftDescriptionEnglish))
                throw new ArgumentException("A hue axis requires an English shift description.", nameof(shiftDescriptionEnglish));
            Name = name;
            HueAngleDegrees = hueAngleDegrees;
            ShiftDescriptionEnglish = shiftDescriptionEnglish;
        }
    }

    /// <summary>Direction tolerances and optional application-specific overall thresholds.</summary>
    public sealed class ColorEvaluationOptions
    {
        public ComparisonFormula Formula { get; set; } = ComparisonFormula.Ciede2000;
        /// <summary>Acceptance uses DeltaE less than or equal to this value; null skips acceptance.</summary>
        public double? AcceptanceTolerance { get; set; }
        /// <summary>Perceptibility uses DeltaE greater than or equal to this value; null skips this assessment.</summary>
        public double? PerceptibilityThreshold { get; set; }
        public double LightnessTolerance { get; set; } = 0.0001;
        public double ChromaTolerance { get; set; } = 0.0001;
        public double HueAngleToleranceDegrees { get; set; } = 0.0001;
    }

    /// <summary>Settings for ChromaticityMatch.CompareColors. Each instance owns its default axis list.</summary>
    public sealed class ColorComparisonOptions
    {
        public CmcParameters Cmc { get; set; } = new CmcParameters();
        public Ciede2000Parameters Ciede2000 { get; set; } = new Ciede2000Parameters();
        /// <summary>A color is achromatic when L* is strictly below this threshold OR C* is below the chroma threshold.</summary>
        public double AchromaticLightnessThreshold { get; set; } = 10;
        /// <summary>Adjustable C* threshold. Either L* or C* below its threshold marks a color achromatic; equality does not.</summary>
        public double AchromaticChromaThreshold { get; set; } = 5;
        /// <summary>At least two uniquely named axes with distinct angles. Input order does not matter.</summary>
        public IList<HueAxis> HueAxes { get; set; } = new List<HueAxis>
        {
            new HueAxis("Red", 22, "More reddish"),
            new HueAxis("Yellow", 85, "More yellowish"),
            new HueAxis("Green", 158, "More greenish"),
            new HueAxis("Blue", 263, "More bluish"),
            new HueAxis("Purple", 310, "More purplish")
        };
        public ColorEvaluationOptions Evaluation { get; set; } = new ColorEvaluationOptions();
    }

    /// <summary>Read-only snapshot of the parameters actually used, including sorted hue axes.</summary>
    public sealed class ColorComparisonParameters
    {
        public CmcParameters Cmc { get; }
        public Ciede2000Parameters Ciede2000 { get; }
        public double AchromaticLightnessThreshold { get; }
        public double AchromaticChromaThreshold { get; }
        public IReadOnlyList<HueAxis> HueAxes { get; }
        public ComparisonFormula EvaluationFormula { get; }
        public double? AcceptanceTolerance { get; }
        public double? PerceptibilityThreshold { get; }
        public double LightnessTolerance { get; }
        public double ChromaTolerance { get; }
        public double HueAngleToleranceDegrees { get; }

        internal ColorComparisonParameters(ColorComparisonOptions options)
        {
            Cmc = options.Cmc ?? throw new ArgumentException("Cmc parameters are required.", nameof(options));
            Ciede2000 = options.Ciede2000 ?? throw new ArgumentException("Ciede2000 parameters are required.", nameof(options));
            AchromaticLightnessThreshold = ColorComparisonValidation.NonNegative(options.AchromaticLightnessThreshold, nameof(options.AchromaticLightnessThreshold));
            AchromaticChromaThreshold = ColorComparisonValidation.NonNegative(options.AchromaticChromaThreshold, nameof(options.AchromaticChromaThreshold));
            if (options.HueAxes is null || options.HueAxes.Count < 2 || options.HueAxes.Any(axis => axis is null))
                throw new ArgumentException("At least two non-null hue axes are required.", nameof(options.HueAxes));
            var axes = options.HueAxes.OrderBy(axis => axis.HueAngleDegrees).ToArray();
            if (axes.Select(axis => axis.HueAngleDegrees).Distinct().Count() != axes.Length ||
                axes.Select(axis => axis.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != axes.Length)
                throw new ArgumentException("Hue axes must have distinct angles and unique names.", nameof(options.HueAxes));
            HueAxes = Array.AsReadOnly(axes);

            var evaluation = options.Evaluation ?? throw new ArgumentException("Evaluation settings are required.", nameof(options));
            if (!Enum.IsDefined(typeof(ComparisonFormula), evaluation.Formula))
                throw new ArgumentOutOfRangeException(nameof(evaluation.Formula), "Unknown comparison formula.");
            EvaluationFormula = evaluation.Formula;
            AcceptanceTolerance = ColorComparisonValidation.OptionalNonNegative(evaluation.AcceptanceTolerance, nameof(evaluation.AcceptanceTolerance));
            PerceptibilityThreshold = ColorComparisonValidation.OptionalNonNegative(evaluation.PerceptibilityThreshold, nameof(evaluation.PerceptibilityThreshold));
            LightnessTolerance = ColorComparisonValidation.NonNegative(evaluation.LightnessTolerance, nameof(evaluation.LightnessTolerance));
            ChromaTolerance = ColorComparisonValidation.NonNegative(evaluation.ChromaTolerance, nameof(evaluation.ChromaTolerance));
            HueAngleToleranceDegrees = ColorComparisonValidation.NonNegative(evaluation.HueAngleToleranceDegrees, nameof(evaluation.HueAngleToleranceDegrees));
            if (HueAngleToleranceDegrees > 180)
                throw new ArgumentOutOfRangeException(nameof(evaluation.HueAngleToleranceDegrees), "Hue angle tolerance must not exceed 180 degrees.");
        }
    }

    /// <summary>Read-only Lab/LCh input snapshot. Derived C/h retain double precision.</summary>
    public sealed class ColorComparisonColor
    {
        public double L { get; }
        public double A { get; }
        public double B { get; }
        public double Chroma { get; }
        public double HueAngleDegrees { get; }
        public bool IsAchromatic { get; }

        internal ColorComparisonColor(CIELABCH lab, CIELCH lch, bool isAchromatic)
        {
            L = lab.CIEL; A = lab.CIEA; B = lab.CIEB;
            Chroma = lch.CIEC; HueAngleDegrees = lch.CIEH; IsAchromatic = isAchromatic;
        }

        public CIELABCH ToLab() => new CIELABCH(L, A, B);
    }

    /// <summary>Raw sample-minus-reference differences, rounded to four decimals.</summary>
    public sealed class LabColorDifferences
    {
        public double DeltaL { get; }
        public double DeltaA { get; }
        public double DeltaB { get; }
        public double DeltaChroma { get; }
        /// <summary>Shortest signed Lab hue angle difference; null when hue comparison is inapplicable.
        /// The range is (-180, 180], with a 180-degree tie resolved as increasing hue.</summary>
        public double? HueAngleDifferenceDegrees { get; }

        internal LabColorDifferences(double l, double a, double b, double chroma, double? hue)
        {
            DeltaL = NumericPrecision.Round(l); DeltaA = NumericPrecision.Round(a); DeltaB = NumericPrecision.Round(b);
            DeltaChroma = NumericPrecision.Round(chroma);
            HueAngleDifferenceDegrees = hue.HasValue ? NumericPrecision.Round(hue.Value) : null;
        }
    }

    public sealed class ColorAxisEvaluation
    {
        public ColorDifferenceDirection Direction { get; }
        public string DescriptionEnglish { get; }
        internal ColorAxisEvaluation(ColorDifferenceDirection direction, string description)
        { Direction = direction; DescriptionEnglish = description; }
    }

    /// <summary>One hue-bias result. No simultaneous bias labels are emitted.</summary>
    public sealed class HueShiftEvaluation
    {
        public HueShiftStatus Status { get; }
        public HueShiftDirection Direction { get; }
        /// <summary>The axis nearest the sample hue by circular distance. Ties use the lower axis angle.</summary>
        public HueAxis? SampleMainAxis { get; }
        /// <summary>The first main axis ahead of the sample hue in the signed hue-difference direction,
        /// or null when no bias is evaluated. An axis at the sample hue itself is skipped.</summary>
        public HueAxis? TargetAxis { get; }
        public string DescriptionEnglish { get; }
        internal HueShiftEvaluation(HueShiftStatus status, HueShiftDirection direction, HueAxis? mainAxis, HueAxis? targetAxis, string description)
        { Status = status; Direction = direction; SampleMainAxis = mainAxis; TargetAxis = targetAxis; DescriptionEnglish = description; }
    }

    public sealed class ColorNeutralityEvaluation
    {
        public bool ReferenceIsAchromatic { get; }
        public bool SampleIsAchromatic { get; }
        public string DescriptionEnglish { get; }
        internal ColorNeutralityEvaluation(bool reference, bool sample)
        {
            ReferenceIsAchromatic = reference; SampleIsAchromatic = sample;
            DescriptionEnglish = reference && sample ? "Both achromatic"
                : reference ? "Reference achromatic" : sample ? "Sample achromatic" : "Both chromatic";
        }
    }

    public sealed class OverallColorEvaluation
    {
        public ComparisonFormula Formula { get; }
        public double DeltaE { get; }
        public ColorAcceptanceStatus Acceptance { get; }
        public ColorPerceptibilityStatus Perceptibility { get; }
        public string DescriptionEnglish { get; }
        internal OverallColorEvaluation(ComparisonFormula formula, double deltaE, ColorAcceptanceStatus acceptance, ColorPerceptibilityStatus perceptibility, string description)
        { Formula = formula; DeltaE = deltaE; Acceptance = acceptance; Perceptibility = perceptibility; DescriptionEnglish = description; }
    }

    public sealed class ColorComparisonEvaluation
    {
        public ColorAxisEvaluation Lightness { get; }
        public ColorAxisEvaluation Chroma { get; }
        public HueShiftEvaluation Hue { get; }
        public ColorNeutralityEvaluation Neutrality { get; }
        public OverallColorEvaluation Overall { get; }
        /// <summary>True when either input falls in the configured achromatic range.</summary>
        public bool IsAchromatic => Neutrality.ReferenceIsAchromatic || Neutrality.SampleIsAchromatic;
        public string AchromaticCommentsEnglish => Neutrality.DescriptionEnglish;
        public string LightnessCommentsEnglish => Lightness.DescriptionEnglish;
        public string ChromaCommentsEnglish => Chroma.DescriptionEnglish;
        /// <summary>One short bias phrase, "No change", or "Not applicable".</summary>
        public string HueCommentsEnglish => Hue.DescriptionEnglish;
        /// <summary>Short threshold assessment; "Not evaluated" when no thresholds were supplied.</summary>
        public string TotalCommentsEnglish => Overall.DescriptionEnglish;
        internal ColorComparisonEvaluation(ColorAxisEvaluation lightness, ColorAxisEvaluation chroma, HueShiftEvaluation hue, ColorNeutralityEvaluation neutrality, OverallColorEvaluation overall)
        { Lightness = lightness; Chroma = chroma; Hue = hue; Neutrality = neutrality; Overall = overall; }
    }

    /// <summary>Three color differences, independently accessible evaluations, and immutable input/parameter snapshots.</summary>
    public sealed class ColorComparisonResult
    {
        public ColorComparisonColor Reference { get; }
        public ColorComparisonColor Sample { get; }
        public double DeltaE1976 { get; }
        public double DeltaECmc { get; }
        public double DeltaE2000 { get; }
        public LabColorDifferences Differences { get; }
        public ColorComparisonEvaluation Evaluation { get; }
        public ColorComparisonParameters AppliedParameters { get; }

        internal ColorComparisonResult(ColorComparisonColor reference, ColorComparisonColor sample,
            double de76, double cmc, double de00, LabColorDifferences differences,
            ColorComparisonEvaluation evaluation, ColorComparisonParameters parameters)
        {
            Reference = reference; Sample = sample; DeltaE1976 = de76; DeltaECmc = cmc; DeltaE2000 = de00;
            Differences = differences; Evaluation = evaluation; AppliedParameters = parameters;
        }
    }

    internal static class ColorComparisonValidation
    {
        internal static double NonNegative(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                throw new ArgumentOutOfRangeException(name, "Value must be finite and nonnegative.");
            return value;
        }

        internal static double Positive(double value, string name)
        {
            NonNegative(value, name);
            if (value == 0) throw new ArgumentOutOfRangeException(name, "Weight must be greater than zero.");
            return value;
        }

        internal static double? OptionalNonNegative(double? value, string name) =>
            value.HasValue ? NonNegative(value.Value, name) : null;
    }
}
