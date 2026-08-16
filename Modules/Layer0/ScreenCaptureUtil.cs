using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace JarvisLauncher
{
    public static class ScreenCaptureUtil
    {
        public static byte[]? CapturePrimaryScreen(bool saveToDisk = false)
        {
            try
            {
                var screen = Screen.PrimaryScreen;
                if (screen == null) return null;
                Rectangle bounds = screen.Bounds;
                using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                    }

                    if (saveToDisk)
                    {
                        try {
                            string dir = Path.Combine(PathHandler.GetDataDirectory(), "Screenshots");
                            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                            string filename = $"AI_Vision_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                            bitmap.Save(Path.Combine(dir, filename), ImageFormat.Jpeg);
                        } catch { }
                    }

                    using (MemoryStream ms = new MemoryStream())
                    {
                        // Save as JPEG to reduce payload size for API
                        bitmap.Save(ms, ImageFormat.Jpeg);
                        return ms.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error capturing screen: {ex.Message}");
                return null;
            }
        }

        public static string? CapturePrimaryScreenToBase64(bool saveToDisk = false)
        {
            var bytes = CapturePrimaryScreen(saveToDisk);
            return bytes != null ? Convert.ToBase64String(bytes) : null;
        }
    }
}
