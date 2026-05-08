using static DDefaults;

namespace SpaceBattleSim
{
    public class DText
    {
        // internal field to store the text content, initialized to an empty string to
        // avoid null reference issues. Required because of enabling the text by default when the Text property is set,
        // and to ensure that the text is disabled when it is null or whitespace.
        // This allows for a more intuitive and user-friendly experience, as users can simply
        // set the Text property to enable or disable the text rendering without needing
        // to worry about null values or additional checks.  By initializing it to an empty
        // string and _isEnabled, we ensure that the text is in a consistent state and that
        // the IsEnabled property will correctly reflect whether there is valid text content
        // to display.
        private string _text = string.Empty;
        // On initial set of Text, if orginal is empty, set orgText to the new value, otherwise 
        // keep orgText unchanged.
        private string _orgText = string.Empty;
        // internal fields to track the enabled state and shadowing state of the text.
        // Automatically updated when the Text and ShadowDepth properties are set,
        // ensuring that the rendering logic can easily determine whether to render
        // the text and its shadow without needing to check the text content or
        // shadow depth repeatedly.
        private bool _isEnabled = false;
        private bool _hasShadowing = false;

        // minmize depth and memory usage by using char,
        // since the max value is 255 and we only need
        // positive values, this allows us to store the
        // depth in a single byte instead of an int (4 bytes).
        private char _shadowDepth = DEF_SHDW_DEPTH;

        #region User defined properties
        /// <summary>
        /// Gets or sets the text content.<br/>
        /// If Text is set to null or whitespace, the text will be considered disabled and will not be rendered.
        /// </summary>
        public string Text
        {
            get { return _text; }
            set { 
                _text = value; 
                _isEnabled = !string.IsNullOrWhiteSpace(_text);
                if (_isEnabled && string.IsNullOrWhiteSpace(_orgText))
                    _orgText = _text;
            }
        }
        /// <summary>
        /// Auto set during first Text value set.<br/>
        /// Gets the original text associated with the current instance.
        /// </summary>
        public string OrgText { get { return _orgText; } }
        /// <summary>
        /// When ship is destroyed, this character will be used to represent the dead ship 
        /// instead of the text content.<br/>
        /// </summary>
        public char DeadDisplay { get; set; } = '✞';
        /// <summary>
        /// Gets or sets the font used for rendering text.<br/>
        /// Default: Arial, 12pt, Regular
        /// </summary>
        public Font Font 
            { get; set; } = new Font("Arial", 12, FontStyle.Regular);
        /// <summary>
        /// Gets or sets the foreground color.<br/>
        /// Default: Black color with full opacity.
        /// </summary>
        public Color TextColor 
            { get; set; } = DEF_TEXT_CLR;
        /// <summary>
        /// Gets or sets the shadow depth in pixels.  Zero '0' will represent no shadowing.<br/>
        /// Default: 0px (No Shadow), Max: 255px
        /// </summary>        
        public uint ShadowDepth 
        { 
            get { return _shadowDepth; } 
            set { _shadowDepth = (char)value; _hasShadowing = _shadowDepth > 0; } 
        }
        /// <summary>
        /// Gets or sets the color used for the foreground shadow.<br/>
        /// Default is Black color with 64 alpha transparency.
        /// </summary>
        public Color ShadowColor 
            { get; set; } = DEF_SHADOW_CLR;
        #endregion

        #region Derived properties
        /// <summary>
        /// Gets or sets a value indicating whether the text is enabled.
        /// </summary>
        /// <remarks>
        /// When setting the Text property, this property will automatically enable or disable based on whether the text is null or whitespace.<br/>
        /// This ensures that text rendering is only enabled when there is valid text content to display.
        /// </remarks>
        internal bool IsEnabled { get { return _isEnabled; } }
        /// <summary>
        /// Gets or sets a value indicating whether text shadowing is enabled.
        /// </summary>
        internal bool HasShadowing { get { return _hasShadowing; } }
        /// <summary>
        /// Gets a pen configured with the foreground color and font size.
        /// </summary>
        internal Pen ForeColor { get { return new Pen(this.TextColor, this.Font.SizeInPoints); } }
        /// <summary>
        /// Gets a pen configured with the shadow foreground color and the current font size in points.
        /// </summary>
        internal Pen ForeColorShadow { get { return new Pen(this.ShadowColor, this.ShadowDepth); } }
        #endregion
    }
}
