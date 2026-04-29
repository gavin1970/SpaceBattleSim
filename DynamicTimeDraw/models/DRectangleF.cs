namespace DynamicTimeDraw
{
    public class DRectangleF
    {
        private RectangleF _rect;
        private readonly SizeF _boundary;

        /// <summary>
        /// Initializes a new instance of the DRectangleF class with a default rectangle and an unbounded maximum size.
        /// </summary>
        /// <remarks>This constructor creates a rectangle at position (0, 0) with a width and height of
        /// 100, and sets the maximum size to the largest possible floating-point values. This is useful for scenarios
        /// where an initial, unconstrained rectangle is needed.</remarks>
        public DRectangleF() : this(new RectangleF(0, 0, 100, 100), new SizeF(float.MaxValue, float.MaxValue)) { }
        /// <summary>
        /// Initializes a new instance of the DRectangleF structure using the specified rectangle and a default maximum
        /// size.
        /// </summary>
        /// <remarks>This constructor sets the maximum size to the largest possible floating-point values
        /// by default. Use this overload when you want to specify only the rectangle and do not need to limit its
        /// size.</remarks>
        /// <param name="rectVal">The RectangleF value that defines the position and size of the rectangle.</param>
        public DRectangleF(RectangleF rectVal) : this(rectVal, new SizeF(float.MaxValue, float.MaxValue)) { }
        /// <summary>
        /// Initializes a new instance of the DRectangleF structure with the specified rectangle and boundary size.
        /// </summary>
        /// <remarks>If the specified boundary size has a width or height less than or equal to zero, the
        /// corresponding dimension is considered unbounded.</remarks>
        /// <param name="rectVal">The rectangle to initialize the DRectangleF instance with.</param>
        /// <param name="sizeBoundary">The maximum allowed size for the rectangle. If the width or height is less than or equal to zero, no
        /// boundary is enforced for that dimension.</param>
        public DRectangleF(RectangleF rectVal, SizeF sizeBoundary)
        {
            // Use float.MaxValue if no boundary is provided
            _boundary = new SizeF(
                sizeBoundary.Width <= 0 ? float.MaxValue : sizeBoundary.Width,
                sizeBoundary.Height <= 0 ? float.MaxValue : sizeBoundary.Height
            );

            // Clamp the initial rectangle to ensure it fits within the specified boundary
            _rect = new RectangleF(
                Math.Clamp(rectVal.X, 0, _boundary.Width - rectVal.Width),
                Math.Clamp(rectVal.Y, 0, _boundary.Height - rectVal.Height),
                Math.Clamp(rectVal.Width, 0, _boundary.Width - rectVal.X),
                Math.Clamp(rectVal.Height, 0, _boundary.Height - rectVal.Y)
            );
        }

        /// <summary>
        /// Gets an instance of <see cref="DRectangleF"/> that represents a default DRectangleF.
        /// </summary>
        /// <remarks>Use this property to obtain a rectangle with all values set to zero. This can be
        /// useful for comparisons or as a default value.</remarks>
        public static DRectangleF Default => new DRectangleF();
        /// <summary>
        /// The "Source of Truth"<br/>
        /// Gets the rectangle representing the area of the object. 
        /// This property is read-only and will always reflect the 
        /// current state of the Location and Size properties.<br/>
        /// Any changes to Location or Size will automatically update 
        /// this rectangle to ensure it remains consistent with the 
        /// object's position and dimensions.<br/>
        /// The rectangle's coordinates and size are constrained within 
        /// the specified boundary to prevent invalid drawing parameters.
        /// </summary>
        public RectangleF Rectangle => _rect;
        /// <summary>
        /// Gets or sets the location of the rectangle as a point in client coordinates.
        /// </summary>
        /// <remarks>When setting this property, the location is automatically clamped to ensure the
        /// rectangle remains within the defined boundary if it exists. The X and Y coordinates are 
        /// adjusted if necessary so that the rectangle does not extend beyond the boundary's 
        /// width and height minus the rectangle's width and height.</remarks>
        public PointF Location
        {
            get => _rect.Location;
            set
            {
                // Clamp the X and Y coordinates to ensure the rectangle remains within the boundary
                var x = Math.Clamp(value.X, 0, _boundary.Width - _rect.Width);
                var y = Math.Clamp(value.Y, 0, _boundary.Height - _rect.Height);
                // Update the rectangle's location with the clamped values
                _rect.Location = new PointF(x, y);
            }
        }
        /// <summary>
        /// Gets or sets the size of the rectangle represented by this instance.
        /// </summary>
        /// <remarks>When setting the size, the width and height are clamped to ensure the rectangle
        /// remains within the defined boundary if it exists. The width cannot exceed the boundary's width minus the rectangle's X
        /// coordinate, and the height cannot exceed the boundary's height minus the rectangle's Y coordinate.</remarks>
        public SizeF Size
        {
            get => _rect.Size;
            set
            {
                // Clamp the width and height to ensure the rectangle remains within the boundary
                var w = Math.Clamp(value.Width, 0, _boundary.Width - _rect.X);
                var h = Math.Clamp(value.Height, 0, _boundary.Height - _rect.Y);
                // Update the rectangle's size with the clamped values
                _rect.Size = new SizeF(w, h);
            }
        }
        /// <summary>
        /// Gets or sets the horizontal position of the left edge of the rectangle, in pixels.
        /// </summary>
        /// <remarks>When setting this property, the value is clamped to ensure the rectangle remains
        /// within the horizontal bounds defined by the boundary width minus the rectangle's width.</remarks>
        public float Left
        {
            get => _rect.X;
            set
            {
                _rect.X = Math.Clamp(value, 0, _boundary.Width - _rect.Width);
            }
        }
        /// <summary>
        /// Gets or sets the Y-coordinate of the top edge of the rectangle, constrained within the vertical bounds of
        /// the containing area.
        /// </summary>
        /// <remarks>When setting this property, the value is automatically clamped to ensure the
        /// rectangle remains within the vertical limits defined by the boundary. If the specified value is less than
        /// zero, it is set to zero. If it exceeds the maximum allowed value (boundary height minus rectangle height),
        /// it is set to that maximum.</remarks>
        public float Top
        {
            get => _rect.Y;
            set
            {
                _rect.Y = Math.Clamp(value, 0, _boundary.Height - _rect.Height);
            }
        }
        /// <summary>
        /// Gets or sets the width of the rectangle, constrained by the boundary limits.
        /// </summary>
        /// <remarks>When setting this property, the value is automatically clamped to ensure it is not
        /// negative and does not extend beyond the right edge of the boundary minus the rectangle's X coordinate. 
        /// This prevents the rectangle from exceeding its allowed area.</remarks>
        public float Width
        {
            get => _rect.Width;
            set
            {
                _rect.Width = Math.Clamp(value, 0, _boundary.Width - _rect.X);
            }
        }
        /// <summary>
        /// Gets or sets the height of the rectangle, constrained within the allowed boundary.
        /// </summary>
        /// <remarks>When setting this property, the value is automatically clamped to ensure it is not
        /// negative and does not extend beyond the lower edge of the boundary minus the rectangle's Y coordinate. 
        /// This prevents the rectangle from exceeding its allowed area.</remarks>
        public float Height
        {
            get => _rect.Height;
            set
            {
                _rect.Height = Math.Clamp(value, 0, _boundary.Height - _rect.Y);
            }
        }
        /// <summary>
        /// Gets or sets the center point of the rectangle within its boundary.
        /// </summary>
        /// <remarks>When setting this property, the rectangle is repositioned so that its center aligns
        /// with the specified value. The rectangle's position is automatically clamped to ensure it remains fully
        /// within the defined boundary.</remarks>
        public PointF Center
        {
            get => new(_rect.X + (_rect.Width / 2), _rect.Y + (_rect.Height / 2));
            set
            {
                // Calculate the new top-left corner based on the desired center position
                var newX = value.X - (_rect.Width / 2);
                var newY = value.Y - (_rect.Height / 2);
                // Clamp the new location to ensure the rectangle stays within the boundary
                var x = Math.Clamp(newX, 0, _boundary.Width - _rect.Width);
                var y = Math.Clamp(newY, 0, _boundary.Height - _rect.Height);
                // Update the rectangle's location to the new clamped position
                _rect.Location = new PointF(x, y);
            }
        }
        /// <summary>
        /// Gets or sets the x-coordinate of the right edge of the rectangle.
        /// </summary>
        /// <remarks>Setting this property adjusts the rectangle's width or position to ensure the right
        /// edge is at the specified value and remains within the allowed boundary. If the specified value is less than
        /// the current left edge, the rectangle's left edge is moved to maintain the width.</remarks>
        public float Right {
            get => _rect.Right;
            set
            {
                // If the new right edge is less than the current left edge,
                // adjust the left edge to maintain the width
                if (value < _rect.X)
                {
                    // Calculate the new left edge based on the desired right edge and current width
                    var left = value - _rect.Width;
                    // Clamp the new left edge to ensure the rectangle stays within the boundary
                    _rect.X = Math.Clamp(left, 0, _boundary.Width - _rect.Width);
                }
                // Adjust the width to ensure the right edge is at the specified value and within the boundary
                _rect.Width = Math.Clamp(value - _rect.X, 0, _boundary.Width - _rect.X);
            }
        }
        /// <summary>
        /// Gets or sets the y-coordinate of the bottom edge of the rectangle, in local coordinates.
        /// </summary>
        /// <remarks>When setting this property, the rectangle's height is adjusted to ensure the bottom
        /// edge is at the specified value. If the new value is less than the current top edge, the rectangle is
        /// repositioned so that its height remains non-negative. The value is clamped to remain within the allowed
        /// boundary.</remarks>
        public float Bottom
        {
            get => _rect.Bottom;
            set
            {
                // If the new bottom edge is less than the current top edge, adjust the top edge to maintain the height
                if (value < _rect.Y)
                {
                    // Calculate the new top edge based on the desired bottom edge and current height
                    var top = value - _rect.Height;
                    // Clamp the new top edge to ensure the rectangle stays within the boundary
                    _rect.Y = Math.Clamp(top, 0, _boundary.Height - _rect.Y);
                }
                // Adjust the height to ensure the bottom edge is at the specified value and within the boundary
                _rect.Height = Math.Clamp(value - _rect.Y, 0, _boundary.Height - _rect.Y);
            }
        }
    }
}
