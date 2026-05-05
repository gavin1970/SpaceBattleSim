using System.Collections.Concurrent;
using static DDefaults;

namespace DynamicTimeDraw
{
    /// <summary>
    /// Represents a collection of drawable polygonal shapes.
    /// </summary>
    public class DShapes
    {
        /// <summary>
        /// Stores additional line segments to be drawn, each defined by a start and end point.
        /// </summary>
        private readonly ConcurrentDictionary<(PointF Start, PointF End), Pen> _extraDrawList = new ConcurrentDictionary<(PointF Start, PointF End), Pen>();

        /// <summary>
        /// Gets a list of line segments defined by start and end points.
        /// </summary>
        public List<(PointF Start, PointF End, Pen Pen)> DrawList => _extraDrawList.Select(s => (s.Key.Start, s.Key.End, s.Value)).ToList();
        /// <summary>
        /// Adds a line defined by the specified start and end points to the collection if it does not already exist.
        /// </summary>
        /// <param name="start">The starting point of the line.</param>
        /// <param name="end">The ending point of the line.</param>
        /// <returns>true if the line was added; false if it already exists in the collection.</returns>
        public bool Add(PointF start, PointF end, Pen pen)
        {
            if (!_extraDrawList.TryAdd((start, end), pen))
                return false;

            return true;
        }
        /// <summary>
        /// Adds multiple lines defined by the specified start and end points to the collection if they do not already exist.
        /// </summary>
        /// <param name="lines">An array of tuples representing the start and end points of the lines.</param>
        /// <param name="itemType">The type of the item to set if the lines are added successfully.</param>
        /// <returns>true if all lines were added; false if any line already exists in the collection.</returns>
        public bool Add((PointF start, PointF end)[] lines, Pen pen)
        {
            foreach (var (start, end) in lines)
            {
                if (!Add(start, end, pen))
                    return false;
            }

            return true;
        }
        /// <summary>
        /// Gets or sets the pen used to draw the shadow of a line.
        /// </summary>
        public Pen ShadowPen { get; set; } = DEF_LINE_SHADOW_SETUP;

        /// <summary>
        /// Adds closed polygonal shapes for each set of coordinates in the 
        /// specified list, using the provided pen and optional movement offsets. 
        /// The last point in each set of coordinates is connected back to the 
        /// first point to create a closed shape.
        /// <code>
        /// Usage:
        /// List<float[]> coordsList = new List<float[]>();
        /// ---===[ Diamonds Shapes ]===---  
        /// 2 sets equal 1 PointF corner - (697, 506), (723, 491), (723, 520), (697, 536)
        ///    coordsList.Add(new float[] { 697, 506,   723, 491,   723, 520,   697, 536 });
        /// coordsList.Add(new float[] { 732, 486, 758, 471, 784, 486, 758, 501 });
        /// coordsList.Add(new float[] { 793, 491, 819, 506, 819, 536, 793, 521 });
        /// coordsList.Add(new float[] { 697, 546, 723, 561, 723, 591, 697, 576 });
        /// coordsList.Add(new float[] { 732, 596, 758, 581, 784, 596, 758, 611 });
        /// coordsList.Add(new float[] { 793, 561, 819, 546, 819, 575, 793, 591 });
        /// ---===[ Inner 6 Corner Shapes ]===---
        /// coordsList.Add(new float[] { 697, 541, 728, 523, 728, 489, 755, 506, 755, 539, 725.5f, 556 });
        /// coordsList.Add(new float[] { 760.5f, 539, 760.5f, 505, 788.5f, 489, 788.5f, 523, 818, 541, 790.5f, 556 });
        /// coordsList.Add(new float[] { 727.5f, 560, 757.5f, 543, 788, 560, 788, 593, 757.5f, 575, 727.5f, 593 });         
        /// var tr = AddPolygonalShapes(coordsList, DEF_LINE_SETUP).GetAwaiter().GetResult();
        /// </code>
        /// </summary>
        /// <remarks>Each polygon is drawn by connecting its vertices in order and closing the shape by
        /// connecting the last vertex to the first. The movement offsets allow repositioning of the polygons without
        /// modifying the original coordinates.</remarks>
        /// <param name="coordsList">A list of coordinate arrays, where each array defines the vertices of a polygon to be drawn.</param>
        /// <param name="pen">The pen used to draw the outlines of the polygons.</param>
        /// <param name="moveX">An optional horizontal offset applied to each coordinate. Good for shadows. The default is 0.</param>
        /// <param name="moveY">An optional vertical offset applied to each coordinate. Good for shadows. The default is 0.</param>
        public Task AddPolygonalShapes(List<float[]> coordsList, Pen pen, float moveX = 0, float moveY = 0)
        {
            return Task.Run(() =>
            {
                try
                {
                    // lines from center point for all corners of the inner HomeBase shape
                    foreach (var cords in coordsList)
                    {
                        // Create the points for the lines based on the
                        // coordinates and optional movement offsets
                        var points = BuildClosedShape(cords, moveX, moveY).ToList();

                        // Add lines between each point and the next,
                        // wrapping around to the first point at the end to
                        // create a closed shape
                        for (int i = 0; i < points.Count; i++)
                            // Add lines between each point and the next, wrapping around to the first point at the end
                            this.Add(points[i], points[(i == points.Count - 1 ? 0 : i + 1)], pen);
                    }
                }
                catch (Exception)
                {
                    return false;
                }
                return true;
            });
        }
        /// <summary>
        /// Creates an array of Point structures representing the corners of a closed shape, optionally offset by specified values.
        /// </summary>
        /// <param name="coordinates">An array of integers representing the x and y coordinates of the corners of the shape.</param>
        /// <param name="moveX">The horizontal offset to apply to each x-coordinate.</param>
        /// <param name="moveY">The vertical offset to apply to each y-coordinate.</param>
        /// <returns>An array of Point structures representing the adjusted coordinates.</returns>
        /// <exception cref="ArgumentException">Thrown when the coordinates array does not contain a valid number of elements.</exception>
        public static PointF[] BuildClosedShape(float[] coordinates, float moveX = 0, float moveY = 0)
        {
            if (coordinates.Length > 3 && (coordinates.Length % 4) != 0)
                throw new ArgumentException("Coordinates array must contain a valid number of elements representing the corners of the shape.");

            var retVal = new List<PointF>();
            for (int i = 0; i < coordinates.Length; i += 2)
                retVal.Add(new PointF(coordinates[i] + moveX, coordinates[i + 1] + moveY));

            return retVal.ToArray();
        }

        /// <summary>
        /// Creates an array of points representing the vertices of a star shape based on the specified location and
        /// size.
        /// </summary>
        /// <param name="location">The reference point for the star. Interpreted as the top-left corner of the bounding rectangle unless
        /// <paramref name="loctionIsCenter"/> is set to <see langword="true"/>.</param>
        /// <param name="size">The overall width and height of the star.</param>
        /// <param name="loctionIsCenter">If set to <see langword="true"/>, the <paramref name="location"/> parameter is treated as the center of the
        /// star; otherwise, it is treated as the top-left corner.</param>
        /// <returns>An array of <see cref="PointF"/> objects representing the vertices of the star.</returns>
        public static PointF[] CreateStar(PointF location, SizeF size, bool loctionIsCenter = false)
        {
            List<PointF> retVal = new();

            var midWidth = size.Width / 2;
            var midHeight= size.Height / 2;
            var cx = location.X + midWidth;
            var cy = location.Y + midHeight;

            // horizonal bar, left side
            retVal.Add(new PointF(location.X, cy));
            // horizonal bar, right side
            retVal.Add(new PointF(location.X + size.Width, cy));
            // veritcal bar, top side
            retVal.Add(new PointF(cx, location.Y));
            // veritcal bar, bottom side
            retVal.Add(new PointF(cx, location.Y + size.Height));

            return retVal.ToArray();
        }
    }
}
// 🌌 Interstellar Clouds / Nebulae
// Rough circle scatter — each "point" is a 1px dot (line of length ~1)
// for each point in cloud:
//     Add(new PointF(x, y), new PointF(x+1, y+1), cloudPen)
