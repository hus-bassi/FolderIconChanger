namespace FolderIconChanger.Localization;

/// <summary>
/// Central place for all user-facing strings.
/// Additional languages (Arabic, Russian, ...) can be added later by
/// swapping the backing implementation of this class, the UI stays
/// unchanged because it binds through this type via {x:Static}.
/// </summary>
public static class Strings
{
    public const string AppTitle = "Folder Icon Changer";
    public const string AppSubtitle = "Give any folder a custom icon";

    public const string ImageSectionTitle = "CUSTOM IMAGE";
    public const string ImageDropHint = "Drop an image here";
    public const string ImageEmptyHint = "Drop an image here or click Choose Image";
    public const string ChooseImageButton = "Choose Image";

    public const string FolderSectionTitle = "TARGET FOLDER";
    public const string FolderEmptyHint = "No folder selected";
    public const string ChooseFolderButton = "Choose Folder";

    public const string ApplyButton = "APPLY ICON";
    public const string RestoreButton = "Restore Default Icon";

    public const string SelectedImageLabel = "Selected Image";
    public const string FolderIconPreviewLabel = "Folder Icon Preview";

    public const string StatusReady = "Ready";
    public const string StatusWorking = "Working…";

    public const string NoImageSelected = "No image selected. Choose an image first.";
    public const string NoFolderSelected = "Please select a target folder.";
    public const string FolderNotFound = "The selected folder does not exist.";
    public const string ImageLoadFailed = "Could not load this image. Use a PNG, JPG, BMP or WEBP file.";
    public const string UnsupportedDrop = "Dropped file is not a supported image.";
    public const string Applying = "Applying icon to folder…";
    public const string Restoring = "Restoring default icon…";
    public const string IconAppliedSuccess = "Icon applied successfully.";
    public const string IconRestoredSuccess = "Folder restored to its default icon.";
    public const string NoCustomIconFound = "This folder does not have a custom icon from Folder Icon Changer.";
    public const string RestoredPrevious = "Custom icon removed. The folder's original icon was restored.";

    public const string RestartExplorerButton = "Restart Explorer";
    public const string RestartExplorerConfirm = "Restart Explorer now?\n\nAny open File Explorer windows will close and reopen automatically. This is the surest way to force Windows to show the new folder icon.";
    public const string RestartingExplorer = "Restarting Explorer…";
    public const string ExplorerRestarted = "Explorer restarted. Check the folder icon now.";

    public const string ErrorAccessDenied = "Windows denied permission to modify this folder. You may need to move the folder to an accessible location, or run the app as administrator for protected system folders.";
    public const string ErrorFolderInaccessible = "Unable to access this folder.";
    public const string ErrorIconWrite = "Unable to create the icon file.";
    public const string ErrorDesktopIni = "Unable to update desktop.ini.";
    public const string ErrorIconBuild = "Unable to generate the icon.";
    public const string ErrorUnexpected = "Something went wrong. Please try again.";

    public const string CliArgError = "Invalid arguments.";
}