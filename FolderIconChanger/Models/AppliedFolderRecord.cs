namespace FolderIconChanger.Models;

/// <summary>Stores everything needed to safely undo an icon customization.</summary>
public sealed class AppliedFolderRecord
{
    public string FolderPath { get; set; } = "";
    public string IconFileName { get; set; } = "";
    public bool HadReadOnly { get; set; }
    public bool HadSystemAttribute { get; set; }
    public bool CreatedDesktopIni { get; set; }
    public string? PreviousIconResource { get; set; }
    public DateTime AppliedUtc { get; set; } = DateTime.UtcNow;
}