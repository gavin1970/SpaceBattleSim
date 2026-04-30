using System.Text;

namespace DynamicTimeDraw
{
    /// <summary>
    /// Represents the readonly statistics and configuration for a specific type of ship, including shields, power, speed, hitbox
    /// size, and recovery priority.
    /// </summary>
    /// <remarks>Use this class to access the predefined attributes for each ship type in the game. The values
    /// are determined by the ship's type and are read-only after initialization. This class is typically used to
    /// retrieve ship characteristics for gameplay logic, such as determining survivability, movement speed, or recovery
    /// order.</remarks>
    public class ShipStats
    {
        static private readonly Dictionary<ShipType, (uint Shields, uint Power, uint HitBox, float Speed, RecoverOrder Recovery)> _shipsAvailable = 
                            new Dictionary<ShipType, (uint Shields, uint Power, uint HitBox, float Speed, RecoverOrder Recovery)>()
        {
            // The most fragile ship, but also the fastest and with the smallest hitbox.
            // It is used to heal other ships and should be recovered first.
            { ShipType.TowRig, (400, 1, 20, 2.0f, RecoverOrder.Critical) },
            // The most durable and powerful ship as a whole, but also the slowest.
            // It is the main target for the enemy team and should be recovered only
            // after healer and protected at all costs.
            { ShipType.Capital, (800, 8, 75, 0.3f, RecoverOrder.High) },
            // Curent not used.
            { ShipType.Bomber, (400, 6, 60, 0.5f, RecoverOrder.Medium) },
            // Small random ship to protect the home base.
            { ShipType.Fighter, (200, 4, 50, 1.0f, RecoverOrder.Low) },
            // Current not used.
            { ShipType.Transport, (2000, 0, 40, 2.0f, RecoverOrder.Low) },
            // Half the shield of a Captial ship and twice as much power.
            // The same hitbox and speed as a Fighter, but no recovery since
            // they are not on the home team.
            { ShipType.Raider, (400, 16, 50, 1.0f, RecoverOrder.None) },
        };

        private uint _shields = 0;
        private uint _power = 0;
        private float _speed = 0.0f;
        private uint _hitbox = 0;
        private RecoverOrder _recovery = RecoverOrder.None;

        /// <summary>
        /// Initializes a new instance of the ShipStats class with the specified ship type.
        /// </summary>
        /// <remarks>If the specified ship type is recognized, the corresponding statistics such as
        /// shields, power, speed, hit box, and recovery order are initialized based on predefined values. Otherwise,
        /// these statistics remain unset or at their default values.</remarks>
        /// <param name="type">The type of ship for which to initialize statistics.</param>
        public ShipStats(ShipType type)
        {
            Type = type;
            if (_shipsAvailable.TryGetValue(type, out (uint shields, uint power, uint hitBox, float speed, RecoverOrder recovery) value))
            {
                _shields = value.shields;
                _power = value.power;
                _speed = value.speed;
                _hitbox = value.hitBox;
                _recovery = value.recovery;
            }
        }
        /// <summary>
        /// Gets the type of the ship.
        /// </summary>
        public ShipType Type { get; }
        /// <summary>
        /// Gets the current shield value for the ship.
        /// </summary>
        public uint Shields { get { return _shields; } }
        /// <summary>
        /// Gets the current power value for the ship.
        /// </summary>
        public uint Power { get { return _power; } }
        /// <summary>
        /// Gets the current recovery order for the ship.
        /// </summary>
        public RecoverOrder Recovery { get { return _recovery; } }
        /// <summary>
        /// Gets the current speed value for the ship.
        /// </summary>
        public float Speed { get { return _speed; } }
        /// <summary>
        /// Gets the current hitbox size for the ship.
        /// </summary>
        public uint Hitbox { get { return _hitbox; } }
    }
}
