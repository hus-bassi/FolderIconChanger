using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FolderIconChanger.Helpers;
using FolderIconChanger.Localization;
using FolderIconChanger.Services;

namespace FolderIconChanger;

public partial class MainWindow : Window
{
    private static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".webp" };

    private BitmapSource? _composedSquare;
    private string? _selectedImagePath;
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        EnableDarkTitleBar();
        ApplyUiText();

        var settings = SettingsService.Instance.Current;
        if (!string.IsNullOrWhiteSpace(settings.LastFolder))
        {
            FolderPathBox.Text = settings.LastFolder;
        }
    }

    // ------------------------------------------------------------ UI helpers

    private void ApplyUiText()
    {
        // Nothing dynamic for now; strings are resolved through x:Static in XAML.
    }

    private enum StatusKind
    {
        Ready,
        Info,
        Success,
        Error
    }

    private void SetStatus(string text, StatusKind kind = StatusKind.Info)
    {
        StatusText.Text = text;
        StatusText.Foreground = kind switch
        {
            StatusKind.Error => (Brush)FindResource("ErrorBrush"),
            StatusKind.Success => (Brush)FindResource("SuccessBrush"),
            _ => (Brush)FindResource("TextSecondaryBrush")
        };

        StatusDot.Fill = kind switch
        {
            StatusKind.Error => (Brush)FindResource("ErrorBrush"),
            StatusKind.Success => (Brush)FindResource("SuccessBrush"),
            StatusKind.Ready => (Brush)FindResource("TextMutedBrush"),
            _ => (Brush)FindResource("AccentBrush")
        };
    }

    private void SetBusy(bool busy, string statusText)
    {
        _busy = busy;
        ApplyButton.IsEnabled = !busy;
        RestoreButton.IsEnabled = !busy;
        Spinner.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        StatusDot.Visibility = busy ? Visibility.Collapsed : Visibility.Visible;
        if (busy)
        {
            SetStatus(statusText, StatusKind.Info);
            StatusText.Foreground = (Brush)FindResource("AccentBrush");
        }
    }

    public void ShowUnexpectedError()
    {
        if (StatusText is null || StatusDot is null)
        {
            // XAML never finished loading; nothing to paint.
            return;
        }

        if (_busy)
        {
            SetBusy(false, Strings.StatusReady);
        }

        SetStatus(Strings.ErrorUnexpected, StatusKind.Error);
    }

    // ------------------------------------------------------------ image load

    private void ChooseImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select an image",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.webp|" +
                     "PNG (*.png)|*.png|" +
                     "JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|" +
                     "Bitmap (*.bmp)|*.bmp|" +
                     "WebP (*.webp)|*.webp|" +
                     "All files (*.*)|*.*"
        };

        string last = SettingsService.Instance.Current.LastImageDirectory ?? "";
        if (Directory.Exists(last))
        {
            dialog.InitialDirectory = last;
        }
        else
        {
            string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            dialog.InitialDirectory = Directory.Exists(pictures) ? pictures : "";
        }

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        TrySetImage(dialog.FileName);
    }

    private void DropBorder_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) &&
                    FirstDroppedImageFile(e) != null
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void DropBorder_Drop(object sender, DragEventArgs e)
    {
        string? file = FirstDroppedImageFile(e);
        if (file is null)
        {
            SetStatus(Strings.UnsupportedDrop, StatusKind.Error);
            return;
        }

        TrySetImage(file);
        e.Handled = true;
    }

    private static string? FirstDroppedImageFile(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return null;
        }

        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (files is null || files.Length == 0)
        {
            return null;
        }

        foreach (string file in files)
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (SupportedExtensions.Contains(ext))
            {
                return file;
            }
        }

        return null;
    }

    private void TrySetImage(string path)
    {
        if (_busy) return;

        try
        {
            var source = ImageHelper.LoadImage(path);
            var square = IconGenerator.ComposeSquare(source);
            var folderPreview = IconGenerator.RenderToSquare(square, 128);

            _composedSquare = square;
            _selectedImagePath = path;

            PreviewImage.Source = square;
            PreviewImage.Visibility = Visibility.Visible;
            DropHintPanel.Visibility = Visibility.Collapsed;

            SelectedThumb.Source = source;
            FolderThumb.Source = folderPreview;

            SettingsService.Instance.RememberImageDirectory(Path.GetDirectoryName(path));
            AppLog.Info($"Image loaded: '{path}'");
            SetStatus("Image loaded.", StatusKind.Info);
        }
        catch (Exception)
        {
            PreviewImage.Visibility = Visibility.Collapsed;
            DropHintPanel.Visibility = Visibility.Visible;
            SetStatus(Strings.ImageLoadFailed, StatusKind.Error);
        }
    }

    // ------------------------------------------------------------ folder pick

    private void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        string current = FolderPathBox.Text.Trim();
        string initial = !string.IsNullOrWhiteSpace(current) && Directory.Exists(current)
            ? current
            : SettingsService.Instance.Current.LastFolder ?? "";

        string? picked = FolderPickerService.PickFolder(this, initial);
        if (picked is null)
        {
            return;
        }

        FolderPathBox.Text = picked;
        SettingsService.Instance.RememberFolder(picked);
        SetStatus(Strings.StatusReady, StatusKind.Ready);
    }

    // ------------------------------------------------------------ apply icons

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;

        if (_composedSquare is null || _selectedImagePath is null)
        {
            SetStatus(Strings.NoImageSelected, StatusKind.Error);
            return;
        }

        string folder = FolderPathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(folder))
        {
            SetStatus(Strings.NoFolderSelected, StatusKind.Error);
            return;
        }

        if (!Directory.Exists(folder))
        {
            SetStatus(Strings.FolderNotFound, StatusKind.Error);
            return;
        }

        SetBusy(true, Strings.Applying);
        try
        {
            byte[] icoBytes = IconGenerator.BuildIco(_composedSquare);
            var result = await Task.Run(() => FolderIconService.Apply(folder, icoBytes));
            if (result.Success)
            {
                SettingsService.Instance.RememberFolder(folder);
            }

            SetStatus(result.Message, result.Success ? StatusKind.Success : StatusKind.Error);
        }
        catch (UnauthorizedAccessException)
        {
            SetStatus(Strings.ErrorAccessDenied, StatusKind.Error);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Apply threw.");
            SetStatus(Strings.ErrorUnexpected, StatusKind.Error);
        }
        finally
        {
            SetBusy(false, Strings.StatusReady);
        }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;

        string folder = FolderPathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(folder))
        {
            SetStatus(Strings.NoFolderSelected, StatusKind.Error);
            return;
        }

        if (!Directory.Exists(folder))
        {
            SetStatus(Strings.FolderNotFound, StatusKind.Error);
            return;
        }

        SetBusy(true, Strings.Restoring);
        try
        {
            var result = await Task.Run(() => FolderIconService.Restore(folder));
            SetStatus(result.Message, result.Success ? StatusKind.Success : StatusKind.Error);
        }
        catch (UnauthorizedAccessException)
        {
            SetStatus(Strings.ErrorAccessDenied, StatusKind.Error);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Restore threw.");
            SetStatus(Strings.ErrorUnexpected, StatusKind.Error);
        }
        finally
        {
            SetBusy(false, Strings.StatusReady);
        }
    }

    // ------------------------------------------------------------ window chrome

    private void EnableDarkTitleBar()
    {
        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE = 19;

        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        int enabled = 1;
        int result = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int));
        if (result != 0)
        {
            _ = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE, ref enabled, sizeof(int));
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int cbSize);
}