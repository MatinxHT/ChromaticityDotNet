namespace ChromaticityDotNet.Diagrams;

/// <summary>Display axes with room for a legend. Background image extents remain in the projection.</summary>
public sealed class ChromaticityDiagramViewport
{
    public double MaximumX { get; }
    public double MaximumY { get; }
    public bool LegendAtTop { get; }

    public ChromaticityDiagramViewport(ChromaticityDiagramSpace space, double? maximumX = null, double? maximumY = null)
    {
        _ = new ChromaticityDiagramProjection(space);
        MaximumX = maximumX ?? (space == ChromaticityDiagramSpace.Xy ? 1.0 : 0.8);
        MaximumY = maximumY ?? (space == ChromaticityDiagramSpace.Xy ? 1.0 : 0.7);
        DiagramValidation.Finite(MaximumX, nameof(maximumX)); DiagramValidation.Finite(MaximumY, nameof(maximumY));
        if (MaximumX <= 0) throw new ArgumentOutOfRangeException(nameof(maximumX));
        if (MaximumY <= 0) throw new ArgumentOutOfRangeException(nameof(maximumY));
        LegendAtTop = space == ChromaticityDiagramSpace.Xy;
    }
}
