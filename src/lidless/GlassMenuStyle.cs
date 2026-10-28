using System.Drawing;
using System.Drawing.Drawing2D;

namespace Lidless;

internal static class GlassMenuStyle
{
    internal static readonly Color Background = Color.FromArgb(16, 19, 27);
    internal static readonly Color Foreground = Color.FromArgb(245, 247, 252);
    internal static readonly Color Muted = Color.FromArgb(185, 195, 211);
    internal static readonly Color Edge = Color.FromArgb(93, 111, 139);

    internal static void Apply(ToolStripDropDown menu)
    {
        menu.Renderer = new GlassMenuRenderer();
        menu.BackColor = Background;
        menu.ForeColor = Foreground;
        menu.Font = new Font("Segoe UI", 9.5f);
        menu.Padding = new Padding(7, 8, 7, 8);
        menu.Opacity = 0.97;
        menu.SizeChanged += (_, _) => Round(menu);
        Round(menu);
    }

    private static void Round(ToolStripDropDown menu)
    {
        if (menu.Width < 24 || menu.Height < 24) return;
        using var path = Rounded(new Rectangle(0, 0, menu.Width, menu.Height), 12);
        var previous = menu.Region;
        menu.Region = new Region(path);
        previous?.Dispose();
    }

    internal static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var size = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, size, size, 180, 90);
        path.AddArc(bounds.Right - size, bounds.Top, size, size, 270, 90);
        path.AddArc(bounds.Right - size, bounds.Bottom - size, size, size, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - size, size, size, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class GlassMenuRenderer : ToolStripProfessionalRenderer
{
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        var bounds = e.ToolStrip.ClientRectangle;
        if (bounds.Width < 1 || bounds.Height < 1) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var surface = new LinearGradientBrush(bounds,
            Color.FromArgb(31, 37, 51), GlassMenuStyle.Background, LinearGradientMode.Vertical);
        e.Graphics.FillRectangle(surface, bounds);
        using var bloom = new SolidBrush(Color.FromArgb(13, 172, 191, 255));
        e.Graphics.FillEllipse(bloom, -30, -65, bounds.Width + 60, 125);
        using var glint = new LinearGradientBrush(
            new Rectangle(12, 1, Math.Max(1, bounds.Width - 24), 2),
            Color.FromArgb(110, 198, 220, 255), Color.FromArgb(0, 198, 220, 255),
            LinearGradientMode.Horizontal);
        e.Graphics.FillRectangle(glint, 12, 1, Math.Max(1, bounds.Width - 24), 1);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        var bounds = e.ToolStrip.ClientRectangle;
        if (bounds.Width < 3 || bounds.Height < 3) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = GlassMenuStyle.Rounded(
            new Rectangle(1, 1, bounds.Width - 3, bounds.Height - 3), 11);
        using var border = new Pen(GlassMenuStyle.Edge);
        e.Graphics.DrawPath(border, path);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled) return;
        var bounds = new Rectangle(Point.Empty, e.Item.Size);
        bounds.Inflate(-3, -1);
        if (bounds.Width < 16 || bounds.Height < 8) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = GlassMenuStyle.Rounded(bounds, 7);
        using var fill = new LinearGradientBrush(bounds,
            Color.FromArgb(80, 97, 120), Color.FromArgb(45, 57, 77),
            LinearGradientMode.Vertical);
        using var edge = new Pen(Color.FromArgb(116, 146, 184));
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(edge, path);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var bounds = new Rectangle(Point.Empty, e.Item.Size);
        var y = bounds.Height / 2;
        using var line = new Pen(Color.FromArgb(64, 78, 102));
        e.Graphics.DrawLine(line, bounds.Left + 12, y, bounds.Right - 12, y);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? GlassMenuStyle.Foreground : GlassMenuStyle.Muted;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = GlassMenuStyle.Foreground;
        base.OnRenderArrow(e);
    }
}
