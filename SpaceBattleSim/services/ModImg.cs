using System.Drawing.Imaging;
using System.Drawing.Text;

namespace Chizl.DrawGraphics
{
    /// <summary>
    /// Provides image generation utilities for rendering text as an image.
    /// </summary>
    /// <remarks>This class is intended for internal use and is not accessible outside its containing
    /// assembly.</remarks>
    internal static class ModImg
    {
        public static Image TxtToImg(string text, Font font, Color fgColor, Color bgColor)
        {
            // Create a dummy bitmap just to measure the string
            using Image dummy = new Bitmap(1, 1);
            using Graphics measuring = Graphics.FromImage(dummy);
            SizeF textSize = measuring.MeasureString(text, font);

            // Use Format32bppArgb for alpha/transparency support (PNG-compatible in memory)
            Bitmap img = new((int)textSize.Width, (int)textSize.Height, PixelFormat.Format32bppArgb);

            using Graphics drawing = Graphics.FromImage(img);
            // bgColor can be Color.Transparent for a fully transparent background
            drawing.Clear(bgColor);
            drawing.TextRenderingHint = TextRenderingHint.AntiAlias;

            using Brush textBrush = new SolidBrush(fgColor);
            drawing.DrawString(text, font, textBrush, 0, 0);
            drawing.Save();

            return img;
        }

        /// <summary>
        /// Returns the image encoded as a PNG byte array (e.g. for streaming or file output).
        /// </summary>
        public static byte[] TxtToImgPngBytes(string text, Font font, Color fgColor, Color bgColor)
        {
            using Image img = TxtToImg(text, font, fgColor, bgColor);
            using MemoryStream ms = new();
            img.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
    }
}

//using System.Drawing.Text;

//namespace Chizl.DrawGraphics
//{
//    /// <summary>
//    /// Provides image generation utilities for rendering text as an image.
//    /// </summary>
//    /// <remarks>This class is intended for internal use and is not accessible outside its containing
//    /// assembly.</remarks>
//    internal static class ModImg
//    {
//        public static Image TxtToImg(string text, Font font, Color fgColor, Color bgColor)
//        {
//            //first, create a dummy bitmap just to get a graphics object
//            Image img = new Bitmap(1, 1);
//            //build graphic from image
//            Graphics drawing = Graphics.FromImage(img);
//            //measure the string to see how big the image needs to be
//            SizeF textSize = drawing.MeasureString(text, font);
//            //clean up
//            img.Dispose();
//            drawing.Dispose();
//            //create a new image of the right size
//            img = new Bitmap((int)textSize.Width, (int)textSize.Height);
//            //build graphic from image
//            drawing = Graphics.FromImage(img);
//            //paint the background
//            drawing.Clear(bgColor);
//            //create a brush for the text
//            Brush textBrush = new SolidBrush(fgColor);
//            //smooth the text out.
//            drawing.TextRenderingHint = TextRenderingHint.AntiAlias;
//            //draw the image
//            drawing.DrawString(text, font, textBrush, 0, 0);
//            //save into img
//            drawing.Save();
//            //clean up
//            textBrush.Dispose();
//            drawing.Dispose();
//            //return bitmap
//            return new Bitmap(img);
//        }
//    }
//}
