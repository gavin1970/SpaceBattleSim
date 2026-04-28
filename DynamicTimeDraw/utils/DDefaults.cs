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
    /// Represents the default pen used for drawing laser lines.  Default a solid red color 
    /// and a width of 2 pixels.
    /// </summary>
    public static readonly Pen DEF_LASER_LINE = new Pen(Color.FromArgb(255, Color.Red), 2);
}
