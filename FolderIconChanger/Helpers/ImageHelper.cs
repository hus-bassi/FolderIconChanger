using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FolderIconChanger.Helpers;

/// <summary>Loads an image file into a frozen WPF <see cref="BitmapSource"/>.</summary>
public static class ImageHelper
{
    /// <summary>Images larger than this are decoded at a reduced size to save memory.</summary>
    private const int DecodeCap = 1024;

    public static BitmapSource LoadImage(string path)
    {
        int width, height;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var decoder = BitmapDecoder.Create(
                stream, BitmapCreateOptions.None, BitmapCacheOption.OnDemand);
            var frame = decoder.Frames[0];
            width = frame.PixelWidth;
            height = frame.PixelHeight;
        }

        BitmapSource source;
        if (Math.Max(width, height) > DecodeCap)
        {
            var scaled = new BitmapImage();
            scaled.BeginInit();
            scaled.UriSource = new Uri(path, UriKind.Absolute);
            scaled.CacheOption = BitmapCacheOption.OnLoad;
            scaled.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            if (width >= height)
            {
                scaled.DecodePixelWidth = DecodeCap;
            }
            else
            {
                scaled.DecodePixelHeight = DecodeCap;
            }

            scaled.EndInit();
            source = scaled;
        }
        else
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var decoder = BitmapDecoder.Create(
                stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            source = decoder.Frames[0];
        }

        source.Freeze();
        return source;
    }
}