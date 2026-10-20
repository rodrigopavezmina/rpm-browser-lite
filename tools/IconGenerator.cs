using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class IconGenerator
{
    private static readonly int[] IconSizes = new int[] { 16, 24, 32, 48, 64, 128, 256 };

    private static void Main(string[] args)
    {
        if (args.Length != 1)
            throw new ArgumentException("Se requiere la ruta de salida.");

        List<byte[]> pngImages = new List<byte[]>();
        foreach (int size in IconSizes)
        {
            using (Bitmap bitmap = CreateIconBitmap(size))
            using (MemoryStream png = new MemoryStream())
            {
                bitmap.Save(png, ImageFormat.Png);
                pngImages.Add(png.ToArray());
            }
        }
        WriteIcon(args[0], pngImages);
    }

    private static Bitmap CreateIconBitmap(int size)
    {
        Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.Clear(Color.Transparent);
            float scale = size / 256f;
            graphics.ScaleTransform(scale, scale);
            graphics.CompositingMode = CompositingMode.SourceOver;

            using (GraphicsPath tile = RoundedRectangle(new RectangleF(8, 8, 240, 240), 42))
            using (LinearGradientBrush background = new LinearGradientBrush(
                new PointF(8, 8), new PointF(248, 248),
                Color.FromArgb(17, 25, 42), Color.FromArgb(42, 74, 112)))
            using (Pen rim = new Pen(Color.FromArgb(103, 181, 217), 5f))
            {
                graphics.FillPath(background, tile);
                graphics.DrawPath(rim, tile);
            }

            // Small stars keep the browser/space theme visible even in the 16px icon.
            using (Brush stars = new SolidBrush(Color.FromArgb(225, 246, 255)))
            {
                graphics.FillRectangle(stars, 40, 55, 7, 7);
                graphics.FillRectangle(stars, 64, 35, 4, 4);
                graphics.FillRectangle(stars, 194, 43, 6, 6);
                graphics.FillRectangle(stars, 210, 102, 4, 4);
                graphics.FillRectangle(stars, 45, 190, 5, 5);
            }

            // Pixel-inspired globe with a clean anti-aliased silhouette.
            using (Brush ocean = new SolidBrush(Color.FromArgb(39, 139, 194)))
            using (Pen grid = new Pen(Color.FromArgb(136, 231, 239), 7f))
            using (Pen globeRim = new Pen(Color.FromArgb(220, 251, 250), 6f))
            {
                graphics.FillEllipse(ocean, 29, 31, 178, 178);
                graphics.DrawEllipse(globeRim, 29, 31, 178, 178);
                graphics.DrawLine(grid, 38, 120, 198, 120);
                graphics.DrawArc(grid, 70, 31, 96, 178, 90, 180);
                graphics.DrawArc(grid, 70, 31, 96, 178, 270, 180);
                graphics.DrawLine(grid, 63, 70, 177, 70);
                graphics.DrawLine(grid, 63, 170, 177, 170);
            }

            // Eight-bit rocket crossing the globe.
            using (Brush rocket = new SolidBrush(Color.FromArgb(250, 252, 244)))
            using (Brush window = new SolidBrush(Color.FromArgb(249, 192, 61)))
            using (Brush flame = new SolidBrush(Color.FromArgb(255, 108, 63)))
            using (Brush flameLight = new SolidBrush(Color.FromArgb(255, 207, 71)))
            {
                Point[] body = new Point[]
                {
                    new Point(123, 139), new Point(123, 119), new Point(141, 119),
                    new Point(141, 101), new Point(159, 101), new Point(159, 83),
                    new Point(177, 83), new Point(177, 65), new Point(195, 65),
                    new Point(195, 47), new Point(215, 47), new Point(215, 67),
                    new Point(197, 67), new Point(197, 85), new Point(179, 85),
                    new Point(179, 103), new Point(161, 103), new Point(161, 121),
                    new Point(143, 121), new Point(143, 139)
                };
                graphics.FillPolygon(rocket, body);
                graphics.FillRectangle(window, 181, 70, 14, 14);
                graphics.FillRectangle(flame, 108, 132, 17, 13);
                graphics.FillRectangle(flameLight, 91, 143, 17, 13);
                graphics.FillRectangle(flame, 73, 154, 17, 13);
            }

            using (Font labelFont = new Font("Segoe UI", 18f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush labelBrush = new SolidBrush(Color.FromArgb(235, 250, 255)))
                graphics.DrawString("RPM", labelFont, labelBrush, new PointF(28, 214));
        }
        return bitmap;
    }

    private static void WriteIcon(string path, IList<byte[]> images)
    {
        using (FileStream file = File.Create(path))
        using (BinaryWriter writer = new BinaryWriter(file))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)images.Count);

            int offset = 6 + images.Count * 16;
            for (int index = 0; index < images.Count; index++)
            {
                int size = IconSizes[index];
                byte dimension = (byte)(size >= 256 ? 0 : size);
                writer.Write(dimension);
                writer.Write(dimension);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write((uint)images[index].Length);
                writer.Write((uint)offset);
                offset += images[index].Length;
            }
            foreach (byte[] image in images)
                writer.Write(image);
        }
    }

    private static GraphicsPath RoundedRectangle(RectangleF rectangle, float radius)
    {
        float diameter = radius * 2f;
        GraphicsPath path = new GraphicsPath();
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180f, 90f);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270f, 90f);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0f, 90f);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90f, 90f);
        path.CloseFigure();
        return path;
    }
}
