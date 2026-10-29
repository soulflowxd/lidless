using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Lidless;

// A visual privacy curtain. It is intentionally click-through so injected agent
// pointer input reaches the desktop. Windows capture exclusion lets compatible
// screenshot-based agents see the desktop under the curtain.
internal sealed class PrivacyCover : Form
{
    private readonly System.Windows.Forms.Timer clock = new() { Interval = 1000 };

    internal PrivacyCover()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.None;
        Font = new Font("Segoe UI", 11f);
        clock.Tick += (_, _) => Invalidate();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= 0x00000020 | 0x00080000 | 0x08000000; // transparent, layered, no activate
            return parameters;
        }
    }

    internal void ShowCover()
    {
        Show();
        if (!SetWindowDisplayAffinity(Handle, 0x11))
        {
            var error = Marshal.GetLastWin32Error();
            Close();
            throw new Win32Exception(error,
                "This Windows session cannot hide the privacy cover from agent screen capture.");
        }
        clock.Start();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs args)
    {
        Bounds = SystemInformation.VirtualScreen;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        var area = ClientRectangle;
        using (var baseBrush = new LinearGradientBrush(area, Color.FromArgb(3, 24, 74),
            Color.FromArgb(2, 126, 206), LinearGradientMode.ForwardDiagonal))
            g.FillRectangle(baseBrush, area);

        // An original blue light field and fine arcs evoke the Windows 7 era
        // without reusing Microsoft artwork or the supplied screenshot.
        using (var glow = new PathGradientBrush(new[] {
            new Point(area.Width / 2, area.Height / 4), new Point(area.Width, area.Height / 3),
            new Point(area.Width, area.Height), new Point(area.Width / 3, area.Height) }))
        {
            glow.CenterColor = Color.FromArgb(170, 48, 192, 234);
            glow.SurroundColors = Enumerable.Repeat(Color.FromArgb(0, 18, 79, 150), 4).ToArray();
            g.FillRectangle(glow, area);
        }
        var scale = Math.Max(1f, Math.Min(area.Width / 1280f, area.Height / 720f));
        using (var arc = new Pen(Color.FromArgb(90, 177, 232, 252), Math.Max(1f, scale)))
        {
            g.DrawBezier(arc, -60, area.Height * .07f, area.Width * .25f, area.Height * .42f,
                area.Width * .57f, area.Height * .10f, area.Width + 60, area.Height * .78f);
            g.DrawBezier(arc, -40, area.Height * .67f, area.Width * .32f, area.Height * .38f,
                area.Width * .72f, area.Height * .75f, area.Width + 40, area.Height * .82f);
        }
        using (var arc = new Pen(Color.FromArgb(85, 177, 240, 146), Math.Max(1f, scale)))
            g.DrawBezier(arc, -50, area.Height * .83f, area.Width * .29f, area.Height * .22f,
                area.Width * .76f, area.Height * .58f, area.Width + 50, area.Height * .92f);

        var primary = Bounds.Size == SystemInformation.VirtualScreen.Size
            ? Screen.PrimaryScreen?.Bounds ?? SystemInformation.VirtualScreen
            : new Rectangle(Left, Top, area.Width, area.Height);
        var centerX = primary.Left - Left + primary.Width / 2f;
        var centerY = primary.Top - Top + primary.Height * .59f;
        var tile = 112f * scale;
        var iconBox = new RectangleF(centerX - tile / 2, centerY - tile * 1.4f, tile, tile);
        using (var shadow = new SolidBrush(Color.FromArgb(55, 0, 19, 49)))
            g.FillRectangle(shadow, iconBox.X + 5 * scale, iconBox.Y + 7 * scale, tile, tile);
        using (var glass = new LinearGradientBrush(iconBox, Color.FromArgb(242, 246, 252),
            Color.FromArgb(161, 196, 224), LinearGradientMode.Vertical))
            g.FillRectangle(glass, iconBox);
        using (var border = new Pen(Color.FromArgb(223, 255, 255, 255), Math.Max(1f, scale)))
            g.DrawRectangle(border, iconBox.X, iconBox.Y, iconBox.Width, iconBox.Height);
        using (var blue = new Pen(Color.FromArgb(26, 69, 106), 3.5f * scale))
        {
            g.DrawArc(blue, centerX - 15 * scale, iconBox.Y + 26 * scale,
                30 * scale, 30 * scale, 0, 360);
            g.DrawArc(blue, centerX - 28 * scale, iconBox.Y + 49 * scale,
                56 * scale, 42 * scale, 180, 180);
        }

        using var nameFont = new Font("Segoe UI", 18 * scale, FontStyle.Regular, GraphicsUnit.Pixel);
        using var hintFont = new Font("Segoe UI", 13 * scale, FontStyle.Regular, GraphicsUnit.Pixel);
        using var footerFont = new Font("Segoe UI Semibold", 16 * scale, FontStyle.Regular, GraphicsUnit.Pixel);
        using var white = new SolidBrush(Color.White);
        using var light = new SolidBrush(Color.FromArgb(227, 244, 253));
        CenterText(g, Environment.UserName, nameFont, white, centerX, centerY - 20 * scale);
        var inputBox = new RectangleF(centerX - 155 * scale, centerY + 15 * scale, 310 * scale, 40 * scale);
        using (var boxBrush = new SolidBrush(Color.FromArgb(232, 248, 253))) g.FillRectangle(boxBrush, inputBox);
        using (var boxBorder = new Pen(Color.FromArgb(130, 222, 250), 1.2f * scale)) g.DrawRectangle(boxBorder,
            inputBox.X, inputBox.Y, inputBox.Width, inputBox.Height);
        using (var dark = new SolidBrush(Color.FromArgb(41, 74, 102)))
            g.DrawString("Press Enter to unlock", hintFont, dark, inputBox.X + 12 * scale, inputBox.Y + 11 * scale);
        CenterText(g, "Agents can keep working beneath this cover", hintFont, light,
            centerX, centerY + 86 * scale);
        CenterText(g, "lidless  ·  privacy cover", footerFont, white,
            centerX, primary.Bottom - Top - 45 * scale);
    }

    private static void CenterText(Graphics graphics, string text, Font font, Brush brush, float x, float y)
    {
        var size = graphics.MeasureString(text, font);
        graphics.DrawString(text, font, brush, x - size.Width / 2, y);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            clock.Dispose();
        }
        base.Dispose(disposing);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);
}
