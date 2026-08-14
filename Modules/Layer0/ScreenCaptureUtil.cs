using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace JarvisLauncher
{
    public static class ScreenCaptureUtil
    {
        public static byte[]? CapturePrimaryScreen()
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

        public static string? CapturePrimaryScreenToBase64()
        {
            var bytes = CapturePrimaryScreen();
            return bytes != null ? Convert.ToBase64String(bytes) : null;
        }
    }
}
