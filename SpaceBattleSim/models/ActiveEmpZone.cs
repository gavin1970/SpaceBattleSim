namespace SpaceBattleSim
{
    public class ActiveEmpZone
    {
        public PointF Center { get; }
        public float MaxRadius { get; }
        public DateTime ExpiresAt { get; }

        public ActiveEmpZone(PointF center, float diameter, double durationSeconds)
        {
            Center = center;
            MaxRadius = diameter / 2f;
            ExpiresAt = DateTime.UtcNow.AddSeconds(durationSeconds);
        }

        /// <summary>
        /// Thread-safe check using standard Pythagorean math to see if a ship is caught inside.
        /// </summary>
        public bool Contains(PointF shipPosition)
        {
            float dx = shipPosition.X - Center.X;
            float dy = shipPosition.Y - Center.Y;
            // Fast squared distance check avoids a costly Math.Sqrt call on every thread tick
            return (dx * dx + dy * dy) <= (MaxRadius * MaxRadius);
        }
    }
}
