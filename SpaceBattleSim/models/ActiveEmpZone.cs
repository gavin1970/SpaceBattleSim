namespace SpaceBattleSim
{
    /// <summary>
    /// Represents an active EMP zone in the game, defined by its center position, 
    /// maximum radius, and expiration time. This class provides methods to check 
    /// if the EMP zone has expired and to determine if a ship is currently within 
    /// the zone based on its position.
    /// </summary>
    public class ActiveEmpZone
    {
        /// <summary>
        /// The center point of the EMP zone, represented as a PointF structure 
        /// containing X and Y coordinates.
        /// </summary>
        public PointF Center { get; }
        /// <summary>
        /// The maximum radius of the EMP zone, calculated as half of the provided diameter.
        /// </summary>
        public float MaxRadius { get; }
        /// <summary>
        /// The timestamp indicating when the EMP zone will expire, calculated as the current UTC time
        /// plus the specified duration in seconds.
        /// </summary>
        public DateTime ExpiresAt { get; }

        /// <summary>
        /// Initializes a new instance of the ActiveEmpZone class with the specified center position, diameter, and duration.
        /// </summary>
        /// <param name="center">The center point of the EMP zone.</param>
        /// <param name="diameter">The diameter of the EMP zone.</param>
        /// <param name="durationSeconds">The duration in seconds for which the EMP zone will be active.</param>
        public ActiveEmpZone(PointF center, float diameter, double durationSeconds)
        {
            Center = center;
            MaxRadius = diameter / 2f;
            ExpiresAt = DateTime.UtcNow.AddSeconds(durationSeconds);
        }

        /// <summary>
        /// Indicates whether the EMP zone has expired based on the 
        /// current UTC time compared to the ExpiresAt timestamp.
        /// </summary>
        public bool HasEnded => DateTime.UtcNow >= ExpiresAt;

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
