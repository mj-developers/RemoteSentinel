using System.Drawing;
using System.Drawing.Drawing2D;

namespace RemoteSentinel.Core.Utils;

/// <summary>
/// Utilidad para generar iconos personalizados, por ejemplo, para la bandeja del sistema.
/// </summary>
internal static class IconFactory
{
    /// Crea un icono circular (tipo "punto") del color especificado, con borde negro.
    internal static Icon BuildDotIcon(Color color)
    {
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        using var br = new SolidBrush(color);
        using var pen = new Pen(Color.Black);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.FillEllipse(br, 2, 2, 12, 12);
        g.DrawEllipse(pen, 2, 2, 12, 12);
        return Icon.FromHandle(bmp.GetHicon());
    }
}
