namespace SpaceBattleSim
{
    public static class EffectsGenerator
    {
        private static Random _random = new Random();

        /// <summary>
        /// Generates a jagged, lightning-like ring array of points.
        /// </summary>
        /// <param name="center">The origin of the EMP burst.</param>
        /// <param name="diameter">The overall diameter of the ring.</param>
        /// <param name="segments">How many jagged segments (higher = more detailed/noisy).</param>
        /// <param name="jaggedness">Maximum pixel displacement inward or outward.</param>
        public static PointF[] CreateLightningRing(PointF center, float diameter, int segments = 60, float jaggedness = 15f)
        {
            PointF[] points = new PointF[segments + 1]; // +1 to close the loop perfectly
            float radius = diameter / 2f;

            // Calculate the angle step based on how many segments we want
            float angleStep = (float)(Math.PI * 2) / segments;
            float lastDisp = 0;

            for (int i = 0; i < segments; i++)
            {
                float currentAngle = i * angleStep;

                // Generate a random displacement for the lightning effect
                // This displaces the radius slightly inward or outward
                float displacement = (float)(_random.NextDouble() * 2.0 - 1.0) * jaggedness;
                lastDisp = (lastDisp * 0.25f) + (displacement * 0.75f);
                float noisyRadius = radius + lastDisp;

                //float noisyRadius = radius + displacement;

                // Convert polar coordinates (angle, radius) to Cartesian (X, Y)
                float x = center.X + (float)Math.Cos(currentAngle) * noisyRadius;
                float y = center.Y + (float)Math.Sin(currentAngle) * noisyRadius;

                points[i] = new PointF(x, y);
            }

            // To make it a perfect loop, the last point must equal the first point
            points[segments] = points[0];

            return points;
        }
    }
}
