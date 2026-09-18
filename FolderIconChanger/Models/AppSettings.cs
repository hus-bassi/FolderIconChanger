using System.IO;

namespace FolderIconChanger.Models;

/// <summary>Serializable application settings persisted across sessions.</summary>
public sealed class AppSettings
{
    public string? LastImageDirectory { get; set; }
    public string? LastFolder { get; set; }

    /// <summary>Folders customized by this application (used to restore safely).</summary>
    public List<AppliedFolderRecord> Records { get; set; } = new();

    public AppliedFolderRecord? FindRecord(string folderPath) =>
        Records.FirstOrDefault(r => string.Equals(
            Path.TrimEndingDirectorySeparator(r.FolderPath),
            Path.TrimEndingDirectorySeparator(folderPath),
            StringComparison.OrdinalIgnoreCase));
}