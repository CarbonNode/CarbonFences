using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CarbonZones.Util
{
    public class ThumbnailProvider
    {
        // Supported .NET images as per https://docs.microsoft.com/en-us/dotnet/api/system.drawing.image.fromfile
        private static readonly string[] SupportedExtensions =
        {
            ".bmp",
            ".gif",
            ".jpg",
            ".jpeg",
            ".png",
            ".tiff",
            ".tif"
        };

        private class ThumbnailState
        {
            public Bitmap bmp;
        }

        // Only allow 4 concurrent images to be decoded to try and prevent OOM errors
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(4);
        private readonly IDictionary<string, ThumbnailState> iconCache = new Dictionary<string, ThumbnailState>();
        public event EventHandler IconThumbnailLoaded;

        public bool IsSupported(string path)
        {
            return SupportedExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        }

        public Bitmap GenerateThumbnail(string path)
        {
            if (!iconCache.ContainsKey(path))
            {
                return SubmitGeneratorTask(path);
            }
            else
            {
                return iconCache[path].bmp;
            }
        }

        private Bitmap SubmitGeneratorTask(string path)
        {
            var state = new ThumbnailState(); // bmp starts null until async load completes
            iconCache[path] = state;

            Task.Run(() =>
            {
                semaphore.Wait();
                try
                {
                    using (var ms = new MemoryStream(File.ReadAllBytes(path)))
                    using (var img = Image.FromStream(ms, false, false))
                    {
                        const int size = 128;
                        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
                        using (var g = Graphics.FromImage(bmp))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.SmoothingMode = SmoothingMode.HighQuality;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.Clear(Color.Transparent);

                            // Fit within size x size preserving aspect ratio
                            float scale = Math.Min((float)size / img.Width, (float)size / img.Height);
                            int w = (int)(img.Width * scale);
                            int h = (int)(img.Height * scale);
                            int ox = (size - w) / 2;
                            int oy = (size - h) / 2;
                            g.DrawImage(img, ox, oy, w, h);
                        }
                        state.bmp = bmp;
                        IconThumbnailLoaded?.Invoke(this, EventArgs.Empty);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Thumbnail generation failed for {path}: {ex.Message}");
                }
                finally
                {
                    semaphore.Release();
                }
            });

            return state.bmp; // null on first call; repaint triggered when ready
        }
    }
}
