using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace WinUtil.Services;

internal static class WinUtilTrayIcon
{
    private const int IconSizePixels = 32;
    private const float GlyphStrokeWidth = 3;
    private const int GlyphLeftX = 6;
    private const int GlyphInnerLeftX = 11;
    private const int GlyphCenterX = 16;
    private const int GlyphInnerRightX = 21;
    private const int GlyphRightX = 26;
    private const int GlyphTopY = 8;
    private const int GlyphCenterY = 15;
    private const int GlyphBottomY = 24;

    private static readonly Color AccentColor = Color.FromArgb(255, 143, 158, 255);
    private static readonly Color GlyphColor = Color.FromArgb(255, 24, 34, 53);

    internal static Icon Create()
    {
        using var bitmap = new Bitmap(IconSizePixels, IconSizePixels);
        using var graphics = Graphics.FromImage(bitmap);
        using var background = new SolidBrush(AccentColor);
        using var glyph = new Pen(GlyphColor, GlyphStrokeWidth)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.FillEllipse(background, 0, 0, IconSizePixels, IconSizePixels);
        graphics.DrawLines(glyph,
        [
            new Point(GlyphLeftX, GlyphTopY),
            new Point(GlyphInnerLeftX, GlyphBottomY),
            new Point(GlyphCenterX, GlyphCenterY),
            new Point(GlyphInnerRightX, GlyphBottomY),
            new Point(GlyphRightX, GlyphTopY)
        ]);

        var iconHandle = bitmap.GetHicon();
        Icon? icon = null;

        try
        {
            using var unownedIcon = Icon.FromHandle(iconHandle);
            icon = (Icon)unownedIcon.Clone();
            return icon;
        }
        finally
        {
            if (!DestroyIcon(iconHandle))
            {
                icon?.Dispose();
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint iconHandle);
}
