using static DDefaults;

namespace DynamicTimeDraw
{
    public enum ItemType
    {
        Text = 0,
        Lines,
        Square,
        Diamond,
        Custom,
        Ellipse,
    }

    public class DLine
    {
        /// <summary>
        /// Stores additional line segments to be drawn, each defined by a start and end point.
        /// </summary>
        private readonly List<(PointF Start, PointF End)> _extraDrawList = new List<(PointF, PointF)>();
        /// <summary>
        /// Gets a list of line segments defined by start and end points.
        /// </summary>
        public List<(PointF Start, PointF End)> DrawList => _extraDrawList.Select(s => (s.Start, s.End)).ToList();
        /// <summary>
        /// Gets or sets a value indicating whether anchor the first point of the line
        /// or not. If true, the line will be anchored to the center of the rectangle,
        /// meaning the end point will move while the start point remains fixed at 1st
        /// Point(), while 2nd Point will be anchored to the center of the rectangle,
        /// even if moving.
        /// </summary>
        public bool HasAnchor { get; set; } = false;
        /// <summary>
        /// Adds a line defined by the specified start and end points to the collection if it does not already exist.
        /// </summary>
        /// <param name="start">The starting point of the line.</param>
        /// <param name="end">The ending point of the line.</param>
        /// <returns>true if the line was added; false if it already exists in the collection.</returns>
        public bool Add(PointF start, PointF end)
        {
            if (!_extraDrawList.Contains((start, end)))
                _extraDrawList.Add((start, end));
            else
                return false;

            // If the line was successfully added, set the item type to Lines.
            this.ItemType = ItemType.Lines;

            return true;
        }
        /// <summary>
        /// Adds multiple lines defined by the specified start and end points to the collection if they do not already exist.
        /// </summary>
        /// <param name="lines">An array of tuples representing the start and end points of the lines.</param>
        /// <param name="itemType">The type of the item to set if the lines are added successfully.</param>
        /// <returns>true if all lines were added; false if any line already exists in the collection.</returns>
        public bool Add((PointF start, PointF end)[] lines, ItemType itemType)
        {
            var retVal = true;

            foreach (var (start, end) in lines)
            {
                if (!_extraDrawList.Contains((start, end)))
                    _extraDrawList.Add((start, end));
                else
                    retVal = false;
            }

            if (retVal)
                this.ItemType = itemType;

            return retVal;
        }
        /// <summary>
        /// Gets or sets the type of the line item. This can be used to differentiate between different 
        /// shapes or types of items, such as squares, diamonds, text, etc.
        /// </summary>
        public ItemType ItemType { get; set; } = ItemType.Lines;
        /// <summary>
        /// Gets or sets the pen used to draw lines.
        /// </summary>
        public Pen Pen { get; set; } = DEF_LINE_SETUP;
        /// <summary>
        /// Gets or sets the pen used to draw the shadow of a line.
        /// </summary>
        public Pen ShadowPen { get; set; } = DEF_LINE_SHADOW_SETUP;
    }
}
