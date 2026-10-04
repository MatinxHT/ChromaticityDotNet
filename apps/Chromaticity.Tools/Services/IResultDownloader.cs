namespace Chromaticity.Tools.Services;

/// <summary>Host-specific file delivery; calculation and UI projects do not depend on browser APIs.</summary>
public interface IResultDownloader
{
    Task DownloadAsync(string fileName, string csv);
}
