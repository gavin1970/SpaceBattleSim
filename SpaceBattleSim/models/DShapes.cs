using System.Collections.Concurrent;

namespace SpaceBattleSim
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
        /// Stores brushes associated with line segments defined by their start and end points.
        /// </summary>
        /// <remarks>This dictionary enables efficient retrieval and management of brushes for specific
        /// line segments, supporting concurrent access in multithreaded scenarios.</remarks>
        private readonly ConcurrentDictionary<(PointF Start, PointF End), Brush> _extraFillList = new ConcurrentDictionary<(PointF Start, PointF End), Brush>();

        /// <summary>
        /// Gets a list of line segments defined by start and end points.
        /// </summary>
        public List<(PointF Start, PointF End, Pen Pen)> DrawList => _extraDrawList.Select(s => (s.Key.Start, s.Key.End, s.Value)).ToList();
        /// <summary>
        /// Gets a list of fill segments, each defined by a start point, end point, and associated brush.
        /// </summary>
        /// <remarks>Each tuple in the list represents a graphical segment to be filled, where the brush
        /// specifies the fill style for that segment. The returned list is a snapshot and modifications to it do not
        /// affect the underlying collection.</remarks>
        public List<(PointF Start, PointF End, Brush Brush)> FillList => _extraFillList.Select(s => (s.Key.Start, s.Key.End, s.Value)).ToList();

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
        /// Adds a fill brush associated with the specified start and end points.
        /// </summary>
        /// <remarks>If a brush is already associated with the specified segment, the method does not add
        /// the new brush and returns false.</remarks>
        /// <param name="start">The starting point of the segment to associate with the brush.</param>
        /// <param name="end">The ending point of the segment to associate with the brush.</param>
        /// <param name="brush">The brush to associate with the specified segment. Cannot be null.</param>
        /// <returns>true if the brush was successfully added for the specified segment; otherwise, false.</returns>
        public bool Add(PointF start, PointF end, Brush brush)
        {
            if (!_extraFillList.TryAdd((start, end), brush))
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
        /// Add multiple lines to the collection using the specified brush.
        /// </summary>
        /// <remarks>If any line fails to be added, the method stops processing further lines and returns
        /// false.</remarks>
        /// <param name="lines">An array of tuples, each containing the start and end points of a line to add.</param>
        /// <param name="brush">The brush used to draw each line. Cannot be null.</param>
        /// <returns>true if all lines are successfully added; otherwise, false.</returns>
        public bool Add((PointF start, PointF end)[] lines, Brush brush)
        {
            foreach (var (start, end) in lines)
            {
                if (!Add(start, end, brush))
                    return false;
            }

            return true;
        }

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
        /// Fills one or more polygonal shapes defined by coordinate arrays using the specified brush.
        /// </summary>
        /// <param name="coordsList">A list of float arrays, where each array contains the coordinates of a polygon to fill. Each array should
        /// represent the vertices of a polygon in sequential order.</param>
        /// <param name="brush">The brush used to fill the interior of the polygons.</param>
        /// <returns>A task that represents the asynchronous fill operation.</returns>
        public Task FillPolygonalShapes(List<float[]> coordsList, Brush brush) =>
            FillPolygonalShapes(coordsList, brush, false, new Pen(brush), 0f, 0f);
        /// <summary>
        /// Fills multiple polygonal shapes defined by coordinate arrays using the specified brush and translation
        /// offsets.
        /// </summary>
        /// <param name="coordsList">A list of float arrays, each representing the vertices of a polygon to fill. Each array should contain pairs
        /// of X and Y coordinates.</param>
        /// <param name="brush">The brush used to fill the interior of each polygon.</param>
        /// <param name="moveX">The horizontal offset, in pixels, to apply to all polygon coordinates before filling.</param>
        /// <param name="moveY">The vertical offset, in pixels, to apply to all polygon coordinates before filling.</param>
        /// <returns>A task that represents the asynchronous fill operation.</returns>
        public Task FillPolygonalShapes(List<float[]> coordsList, Brush brush, float moveX, float moveY) =>
            FillPolygonalShapes(coordsList, brush, false, new Pen(brush), moveX, moveY);
        /// <summary>
        /// Fills one or more closed polygonal shapes defined by coordinate arrays, using the specified brush.
        /// Optionally outlines the shapes with a given pen.
        /// </summary>
        /// <remarks>Each shape in the list is closed automatically by connecting the last point to the
        /// first. The method runs the fill operation on a background thread. If an exception occurs during processing,
        /// the returned task result will be <see langword="false"/>.</remarks>
        /// <param name="coordsList">A list of float arrays, each representing the sequence of X and Y coordinates for a polygonal shape to fill.
        /// Each array must contain an even number of elements, with pairs representing the X and Y positions of each
        /// vertex.</param>
        /// <param name="brush">The brush used to fill the interior of each polygonal shape.</param>
        /// <param name="outline">A value indicating whether to outline each shape after filling. If <see langword="true"/>, the outlinePen is
        /// used to draw the border.</param>
        /// <param name="outlinePen">The pen used to draw the outline of each shape if outlining is enabled. Ignored if <paramref
        /// name="outline"/> is <see langword="false"/>.</param>
        /// <param name="moveX">The horizontal offset, in pixels, to apply to all coordinates of each shape. The default is 0.</param>
        /// <param name="moveY">The vertical offset, in pixels, to apply to all coordinates of each shape. The default is 0.</param>
        /// <returns>A task that represents the asynchronous fill operation. The task result is <see langword="true"/> if all
        /// shapes are filled successfully; otherwise, <see langword="false"/>.</returns>
        public Task FillPolygonalShapes(List<float[]> coordsList, Brush brush, bool outline, Pen outlinePen, float moveX = 0, float moveY = 0)
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
                        {
                            // Add lines between each point and the next, wrapping around to the first point at the end
                            this.Add(points[i], points[(i == points.Count - 1 ? 0 : i + 1)], brush);
                            // Add outline if requested, using the provided pen
                            if (outline)
                                this.Add(points[i], points[(i == points.Count - 1 ? 0 : i + 1)], outlinePen);
                        }
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
    }
}
// 🌌 Interstellar Clouds / Nebulae
// Rough circle scatter — each "point" is a 1px dot (line of length ~1)
// for each point in cloud:
//     Add(new PointF(x, y), new PointF(x+1, y+1), cloudPen)
