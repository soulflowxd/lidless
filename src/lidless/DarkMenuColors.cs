using System.Drawing;

namespace Lidless;

internal sealed class DarkMenuColors : ProfessionalColorTable
{
    internal static readonly Color Background = Color.FromArgb(23, 23, 23);
    internal static readonly Color Foreground = Color.FromArgb(245, 245, 245);
    internal static readonly Color Muted = Color.FromArgb(190, 190, 190);
    private static readonly Color Selected = Color.FromArgb(48, 48, 48);
    private static readonly Color Border = Color.FromArgb(58, 58, 58);
    private static readonly Color Separator = Color.FromArgb(67, 67, 67);

    public override Color ToolStripDropDownBackground => Background;
    public override Color MenuBorder => Border;
    public override Color MenuItemBorder => Selected;
    public override Color MenuItemSelected => Selected;
    public override Color MenuItemSelectedGradientBegin => Selected;
    public override Color MenuItemSelectedGradientEnd => Selected;
    public override Color MenuItemPressedGradientBegin => Selected;
    public override Color MenuItemPressedGradientEnd => Selected;
    public override Color ImageMarginGradientBegin => Background;
    public override Color ImageMarginGradientMiddle => Background;
    public override Color ImageMarginGradientEnd => Background;
    public override Color SeparatorDark => Separator;
    public override Color SeparatorLight => Separator;
}

internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    internal DarkMenuRenderer() : base(new DarkMenuColors()) { }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? DarkMenuColors.Foreground : DarkMenuColors.Muted;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = DarkMenuColors.Foreground;
        base.OnRenderArrow(e);
    }
}
