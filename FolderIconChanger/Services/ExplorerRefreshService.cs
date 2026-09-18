using System.Runtime.InteropServices;
using FolderIconChanger.Helpers;

namespace FolderIconChanger.Services;

/// <summary>
/// Asks Windows Explorer to refresh a folder and rebuild the icon cache
/// without restarting explorer.exe.
/// </summary>
public static class ExplorerRefreshService
{
    // SHCNE events
    private const int SHCNE_UPDATEDIR = 0x00001000;
    private const int SHCNE_UPDATEITEM = 0x00002000;
    private const int SHCNE_ASSOCCHANGED = 0x08000000;

    // SHCNF flags
    private const uint SHCNF_PATHW = 0x0005;
    private const uint SHCNF_IDLIST = 0x0000;
    private const uint SHCNF_FLUSH = 0x1000;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, string? dwItem1, string? dwItem2);

    public static void RefreshFolder(string folderPath)
    {
        try
        {
            SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATHW | SHCNF_FLUSH, folderPath, null);
            SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW | SHCNF_FLUSH, folderPath, null);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Explorer folder refresh failed.");
        }

        try
        {
            // Rebuild the icon cache so the new folder icon shows up immediately.
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST | SHCNF_FLUSH, null, null);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Explorer icon cache refresh failed.");
        }
    }
}