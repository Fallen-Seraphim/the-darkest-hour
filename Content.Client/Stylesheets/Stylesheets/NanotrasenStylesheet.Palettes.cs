using Content.Client.Stylesheets.Palette;

namespace Content.Client.Stylesheets.Stylesheets;

public partial class NanotrasenStylesheet
{
    public override ColorPalette PrimaryPalette => Palettes.DHAmberDark; // DarkestHour: янтарная палитра (было Navy)
    public override ColorPalette SecondaryPalette => Palettes.DHUmber; // DarkestHour (было Slate)
    public override ColorPalette PositivePalette => Palettes.Green;
    public override ColorPalette NegativePalette => Palettes.Red;
    public override ColorPalette HighlightPalette => Palettes.DHAmber; // DarkestHour (было Gold)
}
