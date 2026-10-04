namespace ChromaticityDotNet.Model
{
    /// <summary>Metadata for one archived CIE illuminant or reference-lamp spectrum.
    /// The wavelength grid and relative power scale are preserved from its source CSV.</summary>
    public sealed class CieIlluminantInfo
    {
        public string Id { get; }
        public string DisplayName { get; }
        public int StartingWavelength { get; }
        public int EndingWavelength { get; }
        public int WavelengthInterval { get; }
        public bool IsApproximated { get; }
        public string SourceFile { get; }
        public string SourcePage { get; }

        internal CieIlluminantInfo(string id, string displayName, int start, int end, int interval,
            bool isApproximated, string sourceFile, string sourcePage)
        {
            Id = id; DisplayName = displayName;
            StartingWavelength = start; EndingWavelength = end; WavelengthInterval = interval;
            IsApproximated = isApproximated; SourceFile = sourceFile; SourcePage = sourcePage;
        }
    }
}
