namespace DynamicTimeDraw
{
    internal static class StaticConfig
    {
        /// <summary>
        /// 255 is fully opaque, which ensures that the main elements are clearly 
        /// visible and vibrant in color while maintaining a strong contrast against 
        /// the background, enhancing the overall visual impact of the UI
        /// </summary>
        const byte _alphaColor = 255;
        /// <summary>
        /// 128 is 50% transparent, which allows the shadow to be visible without 
        /// overpowering the main elements and creates a subtle depth effect that 
        /// enhances the overall visual appeal of the UI
        /// </summary>
        const byte _alphaShdwColor = 128;
        /// <summary>
        /// Padding around the form, which creates space between the form's edges 
        /// and its content, contributing to a cleaner and more balanced layout and 
        /// preventing elements from being too close to the edges
        /// </summary>
        static readonly Padding _frmPadding = new Padding(10);
        /// <summary>
        /// Width of buttons, which provides enough space for the button's content 
        /// (like text or icons) while maintaining a compact and visually appealing 
        /// design and ensuring that the buttons are easily clickable for users
        /// </summary>
        const int _btnWidth = 50;
        /// <summary>
        /// Height of buttons, which provides enough space for the button's content 
        /// (like text or icons) while maintaining a compact and visually appealing 
        /// design and ensuring that the buttons are easily clickable for users
        /// </summary>
        const int _btHeight = 50;
        /// <summary>
        /// Depth of shadows, which determines how far the shadow extends from the 
        /// element, creating a sense of depth and separation from the background 
        /// and other elements, enhancing the overall visual appeal and readability 
        /// of the UI
        /// </summary>
        const int _shdwDepth = 7;
        /// <summary>
        /// Size of borders, which defines the thickness of the border around elements, 
        /// contributing to the overall visual structure and separation of UI components 
        /// and enhancing the clarity and aesthetics of the design.
        /// </summary>
        const int _borderSize = 3;

        /// <summary>
        /// Primary colors used in the UI, defined as Color objects with ARGB values
        /// </summary>
        private static readonly Color _red = Color.FromArgb(255, 255, 0, 0);
        private static readonly Color _white = Color.FromArgb(255, 255, 255, 255);
        private static readonly Color _black = Color.FromArgb(255, 0, 0, 0);
        private static readonly Color _consoleBlack = Color.FromArgb(255, 12, 12, 12);
        // Solid brushes for primary colors used in the UI
        private static readonly Brush _brRed = new SolidBrush(_red);
        private static readonly Brush _brWhite = new SolidBrush(_white);
        private static readonly Brush _brBlack = new SolidBrush(_black);
        // Shadow brushes with alpha transparency for shadow effects
        private static readonly Brush _brRed_Shadow = new SolidBrush(Color.FromArgb(_alphaShdwColor, _red));
        private static readonly Brush _brWhite_Shadow = new SolidBrush(Color.FromArgb(_alphaShdwColor, _white));
        private static readonly Brush _brBlack_Shadow = new SolidBrush(Color.FromArgb(_alphaShdwColor, _black));
        // Pens with alpha transparency for borders and foreground elements
        private static readonly Pen _penBlack = new Pen(Color.FromArgb(_alphaColor, _black), _borderSize);
        private static readonly Pen _penWhite = new Pen(Color.FromArgb(_alphaColor, _white), _borderSize);
        // Shadow pens with alpha transparency for shadow effects
        private static readonly Pen _penBlack_Shadow = new Pen(Color.FromArgb(_alphaShdwColor, _black), _borderSize);
        private static readonly Pen _penWhite_Shadow = new Pen(Color.FromArgb(_alphaShdwColor, _white), _borderSize);

        /// <summary>
        /// Provides static properties for configuring the visual style 
        /// of forms, including colors, borders, shadowing, and padding.
        /// </summary>
        public class FormStyle
        {
            /// <summary>
            /// Gets or sets the background color. Default is a solid color defined 
            /// by _consoleBlack constant, which is a very dark shade of black.
            /// </summary>
            public static Color BackColor { get; set; } = _consoleBlack;
            /// <summary>
            /// Gets or sets the brush used for shadow effects. Default is a 
            /// semi-transparent white shadow brush defined by _brWhite_Shadow 
            /// constant, which creates a subtle shadow effect when applied to 
            /// form elements.
            /// </summary>
            public static Brush Shdowing { get; set; } = _brWhite_Shadow;
            /// <summary>
            /// Gets or sets the thickness of the shadow in pixels. Default is defined 
            /// by the _shdwDepth constant, which determines how far the shadow extends 
            /// from the form elements, creating a sense of depth and separation from 
            /// the background.
            /// </summary>
            public static int ShadowingDepth { get; set; } = _shdwDepth;
            /// <summary>
            /// Gets or sets the pen used to draw borders. Default is a white pen with 
            /// alpha transparency defined by _alphaColor and border size defined by 
            /// _borderSize constants, which creates a clean and visible border around 
            /// the form elements.
            /// </summary>
            public static Pen Border { get; set; } = _penWhite;
            /// <summary>
            /// Gets or sets the size of the border in pixels. Default is defined by the 
            /// _borderSize constant, which determines how thick the border appears around 
            /// the form elements, contributing to the overall visual style and separation 
            /// from the background.
            /// </summary>
            public static int BorderSize { get; set; } = _borderSize;
            /// <summary>
            /// Gets or sets the pen used for drawing foreground elements. Default is a white 
            /// pen with alpha transparency defined by _alphaColor and border size defined by 
            /// _borderSize constants, which ensures that foreground elements are clearly visible 
            /// against the dark background and shadow effects.
            /// </summary>
            public static Pen ForeColor { get; set; } = _penWhite;
            /// <summary>
            /// Gets or sets the padding inside the control. Default is a Padding object with 
            /// all sides set to the value of _frmPadding constant, which creates a uniform 
            /// padding around the content of the form, ensuring that elements are not too close 
            /// to the edges and providing a balanced layout.
            /// </summary>
            public static Padding Padding { get; set; } = _frmPadding;
        }

        /*
        /// <summary>
        /// Provides static properties for configuring the appearance and 
        /// layout of a box, including colors, size, and border style.
        /// </summary>
        public class BoxStyle
        {
            /// <summary>
            /// Gets or sets the background brush. Default is a solid red brush.
            /// </summary>
            public static Brush BackColor { get; set; } = _brRed;
            /// <summary>
            /// Gets or sets the brush used for shadow effects. 
            /// Default is a semi-transparent white shadow brush.
            /// </summary>
            public static Brush Shadowing { get; set; } = _brWhite_Shadow;
            /// <summary>
            /// Gets or sets the current shadowing depth value. 
            /// Default is defined by the _shdwDepth constant, which determines how 
            /// far the shadow extends from the box, creating a sense of depth and 
            /// separation from the background.
            /// </summary>
            public static int ShadowingDepth { get; set; } = _shdwDepth;
            /// <summary>
            /// Gets or sets the size of the control. 
            /// nDefault is a Size object with width and height defined by _btnWidth 
            /// and _btHeight constants.
            /// </summary>
            public static Size Size { get; set; } = new Size(_btnWidth, _btHeight);
            /// <summary>
            /// Gets or sets the pen used to draw borders. 
            /// Default is a white pen with alpha transparency defined by _alphaColor 
            /// and border size defined by _borderSize constants.
            /// </summary>
            public static Pen Border { get; set; } = _penWhite;
            /// <summary>
            /// Gets or sets the pen used for drawing foreground elements. 
            /// Default is a white pen with alpha transparency defined by _alphaColor 
            /// and border size defined by _borderSize constants.
            /// </summary>
            public static Pen ForeColor { get; set; } = _penWhite;
        }
        /**/
    }
}
