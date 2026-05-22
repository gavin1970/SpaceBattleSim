// Copyright (c) 2026 Gavin W. Landon (chizl.com)
// Licensed under the MIT License. See LICENSE file http://www.chizl.com/LICENSE.txt for full license information.
// SPDX-License-Identifier: MIT
namespace Chizl.Dimension
{
    /// <summary>
    /// Provides static methods for generating and analyzing points along lines and curves in two-dimensional space.
    /// Includes utilities for calculating evenly spaced points, points at fixed intervals, Bézier curve points, and
    /// angles between two points.
    /// </summary>
    /// <remarks>This class is intended for use in graphical applications, simulations, or any scenario
    /// requiring geometric computations with points in 2D space. All methods are static and thread-safe. The class
    /// cannot be instantiated.</remarks>
    public static class DPoints
    {
        /// <summary>
        /// Calculates an array of points evenly spaced between two specified points.
        /// <code>
        /// PointF startPoint = new PointF(10.0f, 20.0f);
        /// PointF endPoint = new PointF(100.0f, 200.0f);
        /// // Get an array of 5 points spanning from start to end
        /// PointF[] linePoints = GetPointsBetween(startPoint, endPoint, 5);
        /// 
        /// foreach (var p in linePoints)
        ///     Console.WriteLine($"X: {p.X:F1}, Y: {p.Y:F1}");
        /// 
        /// Output will be perfectly spaced:
        ///     X: 10.0, Y: 20.0
        ///     X: 32.5, Y: 65.0
        ///     X: 55.0, Y: 110.0
        ///     X: 77.5, Y: 155.0
        ///     X: 100.0, Y: 200.0
        /// </code>
        /// </summary>
        /// <remarks>The returned array includes both the start and end points as the first and last
        /// elements, respectively. The points are spaced evenly along the straight line connecting the start and end
        /// points.</remarks>
        /// <param name="start">The starting point of the sequence.</param>
        /// <param name="end">The ending point of the sequence.</param>
        /// <param name="totalPoints">The total number of points to generate, including the start and end points. Must be greater than or equal to
        /// 0.</param>
        /// <returns>An array of points linearly interpolated between the start and end points. If totalPoints is 0 or less,
        /// returns an empty array. If totalPoints is 1, returns an array containing only the start point.</returns>
        public static PointF[] GetPointsBetween(PointF start, PointF end, int totalPoints)
        {
            // Handle edge cases
            if (totalPoints <= 0) return new PointF[0];
            if (totalPoints == 1) return new PointF[] { start };

            PointF[] points = new PointF[totalPoints];

            // The divisor is (totalPoints - 1) because the first point is at index 0 
            // and the last point is at index (totalPoints - 1).
            float divisor = totalPoints - 1;

            for (int i = 0; i < totalPoints; i++)
            {
                // t goes from 0.0 to 1.0 as the loop progresses
                float t = i / divisor;

                // Linear interpolation formula: Start + t * (End - Start)
                float x = start.X + t * (end.X - start.X);
                float y = start.Y + t * (end.Y - start.Y);

                points[i] = new PointF(x, y);
            }

            return points;
        }

        /// <summary>
        /// Calculates an array of points spaced at regular intervals along the straight line from the specified start
        /// point to the end point.
        /// <code>
        /// PointF startPoint = new PointF(10.0f, 20.0f);
        /// PointF endPoint = new PointF(100.0f, 200.0f);
        /// 
        /// // If your moves are 5 units per frame, pass 5.0f as the interval
        /// // The resulting array gives you the exact coordinate coordinates your ship should occupy 
        /// // on frame 1, frame 2, frame 3, etc., to travel in a flawless, straight line to the target
        /// PointF[] linePoints = GetPointsByDistance(startPoint, endPoint, 5.0f);
        /// </code>
        /// </summary>
        /// <remarks>The returned array always includes the exact start and end points, regardless of the
        /// interval. The number of points is determined by dividing the total distance by the interval and rounding
        /// down, ensuring at least two points are returned unless the start and end points are nearly
        /// identical.</remarks>
        /// <param name="start">The starting point of the line segment.</param>
        /// <param name="end">The ending point of the line segment.</param>
        /// <param name="interval">The distance, in the same units as the points, between each returned point. Must be greater than zero.</param>
        /// <returns>An array of points spaced at the specified interval along the line from start to end, including both
        /// endpoints. If the start and end points are nearly identical, the array contains only the start point.</returns>
        /// <exception cref="ArgumentException">Thrown if interval is less than or equal to zero.</exception>
        public static PointF[] GetPointsByDistance(PointF start, PointF end, float interval)
        {
            if (interval <= 0) throw new ArgumentException("Interval must be greater than zero.");

            // Calculate the vector distance between points using the Pythagorean theorem
            float dx = end.X - start.X;
            float dy = end.Y - start.Y;
            float totalDistance = (float)Math.Sqrt(dx * dx + dy * dy);

            // If points are practically on top of each other, just return the start
            if (totalDistance < 0.001f) return new PointF[] { start };

            // Determine how many points fit along this distance
            int totalPoints = (int)Math.Floor(totalDistance / interval) + 1;

            // Ensure we always return at least the start and end point
            if (totalPoints < 2) totalPoints = 2;

            PointF[] points = new PointF[totalPoints];

            // Normalize the direction vector (turn it into a length of 1)
            float dirX = dx / totalDistance;
            float dirY = dy / totalDistance;

            for (int i = 0; i < totalPoints - 1; i++)
            {
                // Step forward from start along the direction vector
                points[i] = new PointF(start.X + (dirX * i * interval), start.Y + (dirY * i * interval));
            }

            // Explicitly set the last point to be exactly the end point 
            // to avoid floating-point rounding errors leaving you short.
            points[totalPoints - 1] = end;

            return points;
        }

        public static PointF GetNextPointDistance(PointF start, PointF end, float interval)
        {
            if (interval <= 0) throw new ArgumentException("Interval must be greater than zero.");
            PointF[] points = GetPointsByDistance(start, end, interval);
            return points[0];
        }

        /// <summary>
        /// Calculates a set of points along a quadratic Bézier curve defined by the specified start, control, and end
        /// points.
        /// <code>
        /// start = Starting location.
        /// end = Destination location.
        /// control = The control point that determines the single point curvature of the Bézier curve.
        /// 
        /// If you animate the control point's Y-coordinate downward over time, the generated array 
        /// of points will seamlessly deform from a flat line into a deep, smooth smile or an open 
        /// talking mouth. You can feed this point array straight into a graphics drawing loop 
        /// (like Graphics.DrawCurve or standard vertex arrays) to render the lip.         
        /// </code>
        /// </summary>
        /// <remarks>The generated points include both the start and end points of the curve. This method
        /// uses the standard quadratic Bézier formula to compute each point.</remarks>
        /// <param name="start">The starting point of the Bézier curve.</param>
        /// <param name="control">The control point that determines the curvature of the Bézier curve.</param>
        /// <param name="end">The ending point of the Bézier curve.</param>
        /// <param name="totalPoints">The total number of points to generate along the curve. Must be greater than zero.</param>
        /// <returns>An array of points representing the calculated positions along the Bézier curve. Returns an empty array if
        /// totalPoints is less than or equal to zero, or an array containing only the start point if totalPoints is
        /// one.</returns>
        public static PointF[] GetBezierPoints(PointF start, PointF control, PointF end, int totalPoints)
        {
            if (totalPoints <= 0) return new PointF[0];
            if (totalPoints == 1) return new PointF[] { start };

            PointF[] points = new PointF[totalPoints];
            float divisor = totalPoints - 1;

            // The parameter t will vary from 0 to 1 across the total number
            // of points, allowing us to calculate each point on the curve.
            for (int i = 0; i < totalPoints; i++)
            {
                float t = i / divisor;

                // Quadratic Bezier Formula: B(t) = (1-t)^2 * P0 + 2(1-t) * t * P1 + t^2 * P2
                float u = 1 - t;
                float tt = t * t;
                float uu = u * u;

                // Calculate the X and Y coordinates for the current point on the curve using the formula.
                float x = (uu * start.X) + (2 * u * t * control.X) + (tt * end.X);
                float y = (uu * start.Y) + (2 * u * t * control.Y) + (tt * end.Y);

                // Store the calculated point in the array.
                points[i] = new PointF(x, y);
            }

            return points;
        }

        /// <summary>
        /// Calculates the angle, in degrees, between two points in a 2D plane, measured from the horizontal axis
        /// through the start point to the line connecting the start and end points.
        /// <code>
        /// PointF shipPos = new PointF(100, 100);
        /// PointF targetPos = new PointF(150, 200);
        /// // Get the raw travel angle
        /// float travelAngle = GetAngleBetweenPoints(shipPos, targetPos);
        /// // Adjust by -90 because our spaceship asset points "Up" by default
        /// float spriteRotation = travelAngle - 90f;
        /// // Now pass 'spriteRotation' to render matrix or draw function
        /// </code>
        /// </summary>
        /// <remarks>The angle is measured in a counterclockwise direction from the positive X-axis. This
        /// method is useful for determining orientation or direction between two points in graphical
        /// applications.</remarks>
        /// <param name="start">The starting point from which the angle is measured.</param>
        /// <param name="end">The ending point that defines the direction of the angle relative to the start point.</param>
        /// <returns>The angle in degrees between the two points, normalized to the range 0 to 360. Returns 0 if the points are
        /// identical.</returns>
        public static float GetAngleBetweenPoints(PointF start, PointF end)
        {
            //Calculate the difference vector
            float deltaX = end.X - start.X;
            float deltaY = end.Y - start.Y;

            //Get the angle in radians (Atan2 takes Y first, then X)
            double radians = Math.Atan2(deltaY, deltaX);

            //Convert radians to degrees
            double degrees = radians * (180.0 / Math.PI);

            //(Optional) Normalize to a 0 to 360 range
            if (degrees < 0)
                degrees += 360.0;

            return (float)degrees;
        }
    }
}
