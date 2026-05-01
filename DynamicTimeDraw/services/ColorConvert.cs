namespace Chizl.ColorExtension
{
    public static class ColorConvert
    {
        /// <summary>
        /// Calculates the resulting color when a foreground color is overlaid on a background color, taking alpha transparency into account.
        /// </summary>
        /// <remarks>If the background color is fully transparent, it is treated as fully opaque for
        /// blending purposes. The method performs alpha blending to compute the resulting color.</remarks>
        /// <param name="fgColor">The foreground color to overlay. The alpha component determines the blending with the background color.</param>
        /// <param name="bgColor">The background color over which the foreground color is applied. The alpha component is used for blending
        /// calculations.</param>
        /// <returns>A Color structure representing the blended result of the foreground and background colors. Returns
        /// Color.Empty if either input color is empty.</returns>
        public static Color GetOverlayColor(Color fgColor, Color bgColor)
        {
            // Validate that both colors are not empty before performing blending calculations.
            if (!fgColor.IsEmpty && !bgColor.IsEmpty)
            {
                byte r;
                byte g;
                byte b;

                if (fgColor.A == 0)
                    return bgColor;

                // if alpha is 0 or 1, treat as fully opaque for blending purposes.
                if (bgColor.A <= 1)
                    bgColor = Color.FromArgb(255, bgColor);

                // normalize alpha
                double alpha = fgColor.A / 255.0;

                // get overlay foreground with Alpha on top of background to create new R, G, and B values
                var Rr = (alpha * fgColor.R) + ((1 - alpha) * bgColor.R);
                var Gr = (alpha * fgColor.G) + ((1 - alpha) * bgColor.G);
                var Br = (alpha * fgColor.B) + ((1 - alpha) * bgColor.B);

                // round or force results to 0-255 range.
                r = (byte)Math.Clamp(Rr, 0, 255);
                g = (byte)Math.Clamp(Gr, 0, 255);
                b = (byte)Math.Clamp(Br, 0, 255);

                // build color from new RGB
                return Color.FromArgb(r, g, b);
            }

            // If either color is empty, return Color.Empty to indicate an invalid or uninitialized state.
            return Color.Empty;
        }
    }
}