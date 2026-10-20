using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace ChatGPTLite
{
    internal static class IconVisuals
    {
        public static readonly string[] GlyphChoices = new string[]
        {
            "✦", "●", "◆", "◈", "☀", "☁", "☕", "✎", "☑", "⚑", "⌘", "♟", "#", "!"
        };

        public static Color GetColor(WindowRecord record)
        {
            if (record == null)
                return Color.FromArgb(75, 115, 184);
            return Color.FromArgb(record.IconColorArgb);
        }

        public static string GetGlyph(WindowRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.IconGlyph))
                return "✦";
            string glyph = record.IconGlyph.Trim();
            return glyph.Length > 3 ? glyph.Substring(0, 3) : glyph;
        }

        public static string GetGlyphFromText(string glyph)
        {
            if (string.IsNullOrWhiteSpace(glyph))
                return "✦";
            string trimmed = glyph.Trim();
            return trimmed.Length > 3 ? trimmed.Substring(0, 3) : trimmed;
        }

        public static string GetImagePath(WindowRecord record)
        {
            if (record == null)
                return string.Empty;
            if (record.UseFavicon == true && !string.IsNullOrWhiteSpace(record.FaviconPath) && File.Exists(record.FaviconPath))
                return record.FaviconPath;
            if (record.UseFavicon != true && !string.IsNullOrWhiteSpace(record.CustomIconPath) && File.Exists(record.CustomIconPath))
                return record.CustomIconPath;
            return string.Empty;
        }

        public static bool HasImage(WindowRecord record)
        {
            return !string.IsNullOrWhiteSpace(GetImagePath(record));
        }

        public static Bitmap CreateImageBitmap(WindowRecord record, int width, int height)
        {
            string path = GetImagePath(record);
            if (string.IsNullOrWhiteSpace(path))
                return null;
            try
            {
                using (Image source = Image.FromFile(path))
                {
                    Bitmap image = new Bitmap(Math.Max(1, width), Math.Max(1, height), PixelFormat.Format32bppArgb);
                    using (Graphics graphics = Graphics.FromImage(image))
                    {
                        graphics.Clear(Color.Transparent);
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        Rectangle destination = FitRectangle(source.Size, new Rectangle(0, 0, image.Width, image.Height));
                        graphics.DrawImage(source, destination);
                    }
                    return image;
                }
            }
            catch
            {
                return null;
            }
        }

        public static Color PaleColor(Color color)
        {
            return Color.FromArgb(
                (color.R * 18 + 255 * 82) / 100,
                (color.G * 18 + 255 * 82) / 100,
                (color.B * 18 + 255 * 82) / 100);
        }

        public static Color WaterTint(Color color)
        {
            Color water = Color.FromArgb(189, 236, 232);
            return Color.FromArgb(
                (color.R * 28 + water.R * 42 + 255 * 30) / 100,
                (color.G * 28 + water.G * 42 + 255 * 30) / 100,
                (color.B * 28 + water.B * 42 + 255 * 30) / 100);
        }

        public static Color DarkTint(Color color)
        {
            Color night = Color.FromArgb(22, 29, 36);
            return Color.FromArgb(
                (color.R * 22 + night.R * 78) / 100,
                (color.G * 22 + night.G * 78) / 100,
                (color.B * 22 + night.B * 78) / 100);
        }

        public static Color ContrastColor(Color color)
        {
            int brightness = (color.R * 299 + color.G * 587 + color.B * 114) / 1000;
            return brightness >= 158 ? Color.FromArgb(35, 35, 35) : Color.White;
        }

        public static Icon CreateWindowIcon(WindowRecord record)
        {
            using (Bitmap bitmap = new Bitmap(64, 64))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                if (!DrawImage(graphics, GetImagePath(record), new Rectangle(4, 4, 56, 56)))
                    DrawBadge(graphics, new Rectangle(4, 4, 56, 56), GetGlyph(record), GetColor(record));
                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (Icon source = Icon.FromHandle(handle))
                        return (Icon)source.Clone();
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
        }

        public static bool DrawImage(Graphics graphics, string path, Rectangle bounds)
        {
            if (graphics == null || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;
            try
            {
                using (Image source = Image.FromFile(path))
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    Rectangle destination = FitRectangle(source.Size, bounds);
                    graphics.DrawImage(source, destination);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private static Rectangle FitRectangle(Size source, Rectangle bounds)
        {
            if (source.Width <= 0 || source.Height <= 0)
                return bounds;
            float scale = Math.Min((float)bounds.Width / source.Width, (float)bounds.Height / source.Height);
            int width = Math.Max(1, (int)(source.Width * scale));
            int height = Math.Max(1, (int)(source.Height * scale));
            return new Rectangle(
                bounds.Left + (bounds.Width - width) / 2,
                bounds.Top + (bounds.Height - height) / 2,
                width,
                height);
        }

        public static void DrawBadge(Graphics graphics, Rectangle bounds, string glyph, Color color)
        {
            int radius = Math.Max(4, bounds.Width / 4);
            using (GraphicsPath path = RoundedRectangle(bounds, radius))
            using (SolidBrush background = new SolidBrush(color))
            using (SolidBrush foreground = new SolidBrush(Color.White))
            using (Font font = new Font("Segoe UI Symbol", Math.Max(9f, bounds.Height * 0.52f), FontStyle.Regular, GraphicsUnit.Pixel))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                graphics.FillPath(background, path);
                graphics.DrawString(string.IsNullOrWhiteSpace(glyph) ? "✦" : glyph, font, foreground, bounds, format);
            }
        }

        private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}
