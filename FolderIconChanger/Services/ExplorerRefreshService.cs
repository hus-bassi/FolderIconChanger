using System.IO;
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
            // The folder's icon is displayed inside its PARENT view, so the parent
            // directory must be notified too (this is what usually updates it).
            string root = Path.GetPathRoot(folderPath) ?? folderPath;
            string parent = Path.GetDirectoryName(folderPath.TrimEnd('\\', '/'));
            if (string.IsNullOrEmpty(parent) || string.Equals(parent, folderPath, StringComparison.OrdinalIgnoreCase))
            {
                parent = root;
            }

            Notify(SHCNE_UPDATEDIR, folderPath, null);   // re-scan the folder's own contents
            Notify(SHCNE_UPDATEDIR, parent, null);       // re-scan the parent (shows this folder's icon)
            Notify(SHCNE_UPDATEITEM, folderPath, null);  // refresh this item itself
            Notify(SHCNE_UPDATEITEM, parent, null);
            Notify(SHCNE_ASSOCCHANGED, null, null);      // rebuild the icon cache

            // Explorer can be slow to pick up a re-applied icon; re-announce shortly
            // afterwards so the new icon reliably replaces the cached one.
            _ = Task.Run(async () =>
            {
                await Task.Delay(650);
                Notify(SHCNE_UPDATEDIR, folderPath, null);
                Notify(SHCNE_UPDATEDIR, parent, null);
                Notify(SHCNE_UPDATEITEM, folderPath, null);
                Notify(SHCNE_ASSOCCHANGED, null, null);
            });
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Explorer refresh failed.");
        }
    }

    private static void Notify(int eventId, string? item1, string? item2)
    {
        try
        {
            SHChangeNotify(eventId, SHCNF_PATHW | SHCNF_FLUSH, item1, item2);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, $"SHChangeNotify(0x{eventId:X8}) failed.");
        }
    }
}