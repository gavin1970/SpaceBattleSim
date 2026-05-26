namespace SpaceBattleSim.Models.Events
{
    public delegate void StatusChangeHandler(object sender, StatusChangeArgs e);

    public class StatusChangeArgs : EventArgs
    {
        internal StatusChangeArgs(ShipType shipType, string shipName, ShipStatus shipStatus)
        {
            this.EventDateUtc = DateTime.UtcNow;
            this.EventDate = this.EventDateUtc.ToLocalTime();
            this.ShipName = shipName;
            this.ShipType = shipType;
            this.ShipStatus = shipStatus;
        }

        public DateTime EventDate { get; } = DateTime.Now;              // default value is just to satisfy the compiler, will be set in constructor
        public DateTime EventDateUtc { get; } = DateTime.UtcNow;        // default value is just to satisfy the compiler, will be set in constructor
        public string ShipName { get; } = string.Empty;                 // default value is just to satisfy the compiler, will be set in constructor
        public ShipType ShipType { get; } = ShipType.Transport;         //unused default value, will be set in constructor
        public ShipStatus ShipStatus { get; } = ShipStatus.Operational; // default value is just to satisfy the compiler, will be set in constructor
    }
}
