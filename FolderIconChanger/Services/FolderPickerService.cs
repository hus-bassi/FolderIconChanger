using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FolderIconChanger.Services;

/// <summary>
/// Modern Vista-style folder picker built on the Shell IFileOpenDialog with
/// the FOS_PICKFOLDERS option (no WinForms dependency required).
/// </summary>
public static class FolderPickerService
{
    private const int SIGDN_FILESYSPATH = unchecked((int)0x80058000);
    private const uint ERROR_CANCELLED_HRESULT = 0x800704C7;

    // _FILEOPENDIALOGOPTIONS
    private const uint FOS_PICKFOLDERS = 0x20;
    private const uint FOS_FORCEFILESYSTEM = 0x40;
    private const uint FOS_PATHMUSTEXIST = 0x800;

    private static readonly Guid IidIShellItem = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");

    public static string? PickFolder(Window? owner, string? initialDirectory)
    {
        if (!TryCreate(out var dialog, out string? error))
        {
            return null;
        }

        try
        {
            dialog.SetOptions(FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST);
            dialog.SetTitle("Select a folder");

            if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            {
                if (TryCreateShellItem(initialDirectory, out var item))
                {
                    try
                    {
                        dialog.SetFolder(item);
                    }
                    catch
                    {
                        // initial folder is a nicety, never fatal
                    }
                }
            }

            IntPtr ownerHandle = owner is null ? IntPtr.Zero : new WindowInteropHelper(owner).Handle;
            try
            {
                dialog.Show(ownerHandle);
            }
            catch (COMException ex) when ((uint)ex.ErrorCode == ERROR_CANCELLED_HRESULT)
            {
                return null;
            }

            dialog.GetResult(out var result);
            result.GetDisplayName(SIGDN_FILESYSPATH, out string path);
            return path;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            if (dialog is not null)
            {
                Marshal.FinalReleaseComObject(dialog);
            }
        }
    }

    private static bool TryCreate(out IFileOpenDialog dialog, out string error)
    {
        try
        {
            dialog = (IFileOpenDialog)new FileOpenDialogRCW();
            error = "";
            return true;
        }
        catch (Exception)
        {
            dialog = null!;
            error = "The folder picker could not be created.";
            return false;
        }
    }

    private static bool TryCreateShellItem(string path, out IShellItem item)
    {
        try
        {
            Guid riid = IidIShellItem;
            SHCreateItemFromParsingName(path, IntPtr.Zero, ref riid, out item);
            return true;
        }
        catch (Exception)
        {
            item = null!;
            return false;
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        [In] ref Guid riid,
        [Out, MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);
}

// ---------------------------------------------------------------- COM types

[ComImport, Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
internal class FileOpenDialogRCW
{
}

[ComImport, Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileOpenDialog
{
    [PreserveSig]
    int Show(IntPtr hwndOwner);
    void SetFileTypes(uint cFileTypes, [In] COMDLG_FILTERSPEC[] rgFilterSpec);
    void SetFileTypeIndex(uint iFileType);
    void GetFileTypeIndex(out uint piFileType);
    void Advise(IFileDialogEvents pfde, out uint pdwCookie);
    void Unadvise(uint dwCookie);
    void SetOptions(uint fos);
    void GetOptions(out uint pfos);
    void SetDefaultFolder(IShellItem psi);
    void SetFolder(IShellItem psi);
    void GetFolder(out IShellItem ppsi);
    void GetCurrentSelection(out IShellItem ppsi);
    void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
    void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
    void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
    void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
    void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
    void GetResult(out IShellItem ppsi);
    void AddPlace(IShellItem psi, int fdap);
    void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
    void Close(uint hr);
    void SetClientGuid(ref Guid guid);
    void ClearClientData();
    void SetFilter(IShellItemFilter pFilter);
    void GetResults(out IShellItemArray ppenum);
    void GetSelectedItems(out IShellItemArray ppsai);
}

[ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem
{
    void BindToHandler(IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);
    void GetParent(out IShellItem ppsi);
    void GetDisplayName(int sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
    void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
    void Compare(IShellItem psi, int hint, out int piOrder);
}

// Stub types used only for vtable layout; neither is ever instantiated here.
[ComImport, Guid("973510DB-7D7F-452B-8975-74A85828D354"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileDialogEvents
{
}

[ComImport, Guid("B63EA76D-1F85-456F-A19C-48159EFA858B"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItemArray
{
}

[ComImport, Guid("2659B32C-B1C5-4099-BB8D-071387EEC7D2"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItemFilter
{
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct COMDLG_FILTERSPEC
{
    [MarshalAs(UnmanagedType.LPWStr)]
    public string pszName;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string pszSpec;
}