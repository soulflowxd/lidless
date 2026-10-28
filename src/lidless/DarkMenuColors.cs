using System.Drawing;

namespace Lidless;

internal sealed class DarkMenuColors : ProfessionalColorTable
{
    private static readonly Color Background = Color.FromArgb(54, 61, 73);
    private static readonly Color Selected = Color.FromArgb(76, 89, 108);
    private static readonly Color Border = Color.FromArgb(38, 43, 52);
    private static readonly Color Separator = Color.FromArgb(99, 108, 121);

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
