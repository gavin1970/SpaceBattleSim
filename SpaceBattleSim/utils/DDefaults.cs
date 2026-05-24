internal static class DDefaults
{
    // Default fill color for the box, set to white for better visibility against
    // the default black shadow and border colors.
    public static readonly Color DEF_TEXT_CLR = Color.FromArgb(255, 0, 0, 0);
    /// <summary>
    /// Represents the default shadow color with 64 alpha transparency.
    /// </summary>
    public static readonly Color DEF_SHADOW_CLR = Color.FromArgb(64, 0, 0, 0);
    // Default fill color for the box, set to white for better visibility against
    // the default black shadow and border colors.
    public static readonly Color DEF_NO_CLR = Color.FromArgb(0, Color.Transparent);
    // 10 dec, using char to save memory, since the max value is 255 and we
    // only need positive values, this allows us to store the depth in a
    // single byte instead of an int (4 bytes).
    public static readonly char DEF_SHDW_DEPTH = (char)0;
    // 30 dec, using char to save memory, since the max value is 255 and we
    // only need positive values, this allows us to store the opacity in a
    // single byte instead of an int (4 bytes).
    public static readonly char DEF_SHDW_OPACITY = (char)0;
    // 3 dec, using char to save memory, since the max value is 255 and we
    // only need positive values, this allows us to store the border width in a
    // single byte instead of an int (4 bytes).
    public static readonly char DEF_BORDER_WIDTH = (char)0;
    /// <summary>
    /// Specifies a white pen with a width of 2 units used for line setup.
    /// </summary>
    public static readonly Pen DEF_LINE_SETUP = new Pen(Color.White, 2);
    /// <summary>
    /// Represents the default pen used for drawing line shadows.  Default a semi-transparent white color 
    /// and a width of 0 pixel, which will skip shadow effect.
    /// When above 0, this allows an enhanced visual depth without overpowering the main line, 
    /// creating a more polished and visually appealing result.  Users can customize this pen to achieve 
    /// different shadow effects based on their preferences and design requirements.
    /// </summary>
    public static readonly Pen DEF_LINE_SHADOW_SETUP = new Pen(Color.FromArgb(64, DEF_LINE_SETUP.Color), 0);
    /// <summary>
    /// Represents the default pen used for drawing red laser lines.  Default a solid red color 
    /// and a width of 2 pixels for raiders.
    /// </summary>
    public static readonly Pen DEF_RAIDER_LASER = new Pen(Color.FromArgb(255, Color.Red), 2);
    /// <summary>
    /// Represents the default pen used for drawing green laser lines.  Default a solid green color 
    /// and a width of 2 pixels for allies.
    /// </summary>
    public static readonly Pen DEF_ALLY_LASER = new Pen(Color.FromArgb(255, Color.Green), 2);
    /// <summary>
    /// Represents the default pen used for drawing repair laser lines.  Default a solid blue color 
    /// and a width of 2 pixels.
    /// </summary>
    public static readonly Pen DEF_REPAIR_LASER_LINE = new Pen(Color.FromArgb(255, Color.Blue), 2);

    public static List<float[]> GetHomeBase()   //out PointF center 
    {
        // Size of HomeBase: 125 x 144 
        List<float[]> coordsList = new List<float[]>();
        // ---===[ Outer, Top, Left ]===---
        coordsList.Add(new float[] { 697, 506, 723, 491, 723, 520, 697, 536 });
        // ---===[ Outer, Top ]===---
        coordsList.Add(new float[] { 732, 486, 758, 471, 784, 486, 758, 501 });
        // ---===[ Outer, Top, Right ]===---
        coordsList.Add(new float[] { 793, 491, 819, 506, 819, 536, 793, 521 });
        // ---===[ Inner, Top, Left ]===---
        coordsList.Add(new float[] { 697, 541, 728, 523, 728, 489, 755, 506, 755, 539, 725.5f, 556 });
        // ---===[ Inner, Top, Right ]===---
        coordsList.Add(new float[] { 760.5f, 539, 760.5f, 505, 788.5f, 489, 788.5f, 523, 818, 541, 790.5f, 556 });
        // ---===[ Outer, Bottom, Left ]===---
        coordsList.Add(new float[] { 697, 546, 723, 561, 723, 591, 697, 576 });
        // ---===[ Outer, Bottom ]===---
        coordsList.Add(new float[] { 732, 596, 758, 581, 784, 596, 758, 611 });
        // ---===[ Outer, Bottom, Right ]===---
        coordsList.Add(new float[] { 793, 561, 819, 546, 819, 575, 793, 591 });
        // ---===[ Inner, Bottom ]===---
        coordsList.Add(new float[] { 727.5f, 560, 757.5f, 543, 788, 560, 788, 593, 757.5f, 575, 727.5f, 593 });

        return coordsList;
    }

    public static List<float[]> GetRaidersMotherShip()
    {
        List<float[]> coordsList = new List<float[]>();
        // ---===[ Ship, Top Left ]===---
        coordsList.Add(new float[] { 2, 44, 18, 37, 56, 37, 64, 32, 76, 32, 76, 41, 66, 47, 58, 47, 44, 56, 44, 65, 22, 65, 22, 62, 9, 62, 2, 55 });
        // ---===[ Ship, Top Right ]===---
        coordsList.Add(new float[] { 150, 44, 134, 37, 96, 37, 88, 32, 76, 32, 76, 41, 86, 47, 94, 47, 108, 56, 108, 65, 130, 65, 130, 62, 143, 62, 150, 55 });
        // ---===[ Ship, Bottom Left ]===---
        coordsList.Add(new float[] { 2, 86, 18, 93, 56, 93, 64, 98, 76, 98, 76, 89, 66, 83, 58, 83, 44, 74, 44, 65, 22, 65, 22, 68, 9, 68, 2, 75 });
        // ---===[ Ship, Bottom Right ]===---
        coordsList.Add(new float[] { 150, 86, 134, 93, 96, 93, 88, 98, 76, 98, 76, 89, 86, 83, 94, 83, 108, 74, 108, 65, 130, 65, 130, 68, 143, 68, 150, 75 });
        // ---===[ Ship, Center Peice ]===---
        //coordsList.Add(new float[] { ... });

        return coordsList;
    }
}
