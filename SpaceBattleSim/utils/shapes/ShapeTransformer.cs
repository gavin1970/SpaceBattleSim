namespace SpaceBattleSim.shapes
{
    public class ShapeTransformer
    {
        /// <summary>
        /// Example of use:
        /// <code>
        /// Your existing coordinates
        /// 
        /// List<Point> myShape = new List<Point>
        /// {
        ///     new Point(2, 44),  new Point(18, 37), new Point(56, 37),
        ///     new Point(64, 32), new Point(76, 32), new Point(76, 41),
        ///     new Point(66, 47), new Point(58, 47), new Point(44, 56),
        ///     new Point(44, 62), new Point(22, 65), new Point(22, 62),
        ///     new Point(9, 62),  new Point(2, 55),  new Point(2, 44)
        /// };
        /// 
        /// Flip Horizontally only
        /// List<Point> flippedHoriz = ShapeTransformer.FlipShape(myShape, flipHorizontal: true, flipVertical: false);
        /// 
        /// Flip Vertically only
        /// List<Point> flippedVert = ShapeTransformer.FlipShape(myShape, flipHorizontal: false, flipVertical: true);
        /// 
        /// Print original vs flipped X for the first point to verify
        /// Console.WriteLine($"Original 1st Point: {myShape[0]}");
        /// Console.WriteLine($"Horiz Flipped 1st Point: {flippedHoriz[0]}");
        /// </code>
        /// </summary>
        /// <param name="points">The list of points representing the shape.</param>
        /// <param name="flipHorizontal">Whether to flip the shape horizontally.</param>
        /// <param name="flipVertical">Whether to flip the shape vertically.</param>
        /// <returns>A new list of points representing the flipped shape.</returns>
        public static List<PointF> FlipShape(List<PointF> points, bool flipHorizontal, bool flipVertical)
        {
            if (points == null || points.Count == 0)
                return new List<PointF>();

            // Find the boundaries of the shape
            float minX = points.Min(p => p.X);
            float maxX = points.Max(p => p.X);
            float minY = points.Min(p => p.Y);
            float maxY = points.Max(p => p.Y);

            List<PointF> flippedPoints = new List<PointF>();

            // Transform each point
            foreach (var pt in points)
            {
                float newX = pt.X;
                float newY = pt.Y;

                // Horizontal flip: Subtract current X from the
                // max boundary, add back the min boundary
                if (flipHorizontal)
                    newX = maxX - (pt.X - minX);

                // Vertical flip: Subtract current Y from the
                // max boundary, add back the min boundary
                if (flipVertical)
                    newY = maxY - (pt.Y - minY);

                flippedPoints.Add(new PointF(newX, newY));
            }

            return flippedPoints;
        }
    }
}
