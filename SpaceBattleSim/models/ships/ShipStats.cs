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
        // 🚑 - 128657 - U+1F691 - Ambulance emoji, used for the Repair Rig ship.
        readonly static string _repairRigShip = char.ConvertFromUtf32(10070);  // 10070 - ❖ = \u2756 -- 🚑 - 128657 - U+1F691 - Ambulance emoji, used for the Repair Rig ship.
        readonly static string _capitalShip = char.ConvertFromUtf32(11790);    // 11790 - ⸎ = \u2e4e --  ⮗ - 11159  - \u2b97
        readonly static string _bomberShip = char.ConvertFromUtf32(11258);     // 11258 - ⯺ = \u2bfa
        readonly static string _fighterShip = char.ConvertFromUtf32(11033);    // 11033 - ⬙ = \u2b59
        readonly static string _transportShip = char.ConvertFromUtf32(11213);  // 11213 - ⯍ = \u2b5d
        readonly static string _raiderShip = char.ConvertFromUtf32(11501);     // 11501 - Ⳮ = \u2cd5
        //readonly static string _raiderShip = char.ConvertFromUtf32(10618);   // 10618 - ⥺ = \u293a
        //readonly static string _deadShip = char.ConvertFromUtf32(9760);      // 9760 - ☠️ = \u2620

        readonly static Color _unuseDefault = Color.Gray;
        readonly static Color _raiderColor = Color.FromArgb(255, 255, 0, 0);
        readonly static Color _fighterColor = Color.FromArgb(255, 0, 255, 0);
        readonly static Color _capitalShipColor = Color.FromArgb(255, 200, 200, 200);
        readonly static Color _repairRigShipColor = Color.FromArgb(255, 0, 255, 255);
        readonly static Color _bomberShipColor = _unuseDefault;
        readonly static Color _transportShipColor = _unuseDefault;
        readonly static Color _deadShipColor = _unuseDefault;

        static private readonly Dictionary<ShipType, 
                (uint Shields, uint Power, uint HitBox, float Speed, 
                RecoverOrder Recovery, string ShipInText, Color ShipColor, float Rotate)> 
          _shipsAvailable = new Dictionary<ShipType, 
                (uint Shields, uint Power, uint HitBox, float Speed, 
                RecoverOrder Recovery, string ShipInText, Color ShipColor, float Rotate)>()
        {
            // The most fragile ship, but also the fastest and with the smallest hitbox.
            // It is used to heal other ships and should be recovered first.
            { ShipType.RepairRig, (400, 1, 20, 2.0f, RecoverOrder.Critical, _repairRigShip, _repairRigShipColor, 0.0f) },
            // The most durable and powerful ship as a whole, but also the slowest.
            // It is the main target for the enemy team and should be recovered only
            // after healer and protected at all costs.
            { ShipType.Capital, (800, 8, 75, 0.3f, RecoverOrder.High, _capitalShip, _capitalShipColor, 0.0f) },
            // Curent not used.
            { ShipType.Bomber, (400, 6, 60, 0.5f, RecoverOrder.Medium, _bomberShip, _bomberShipColor, 0.0f) },
            // Small random ship to protect the home base.
            { ShipType.Fighter, (200, 4, 50, 1.0f, RecoverOrder.Low, _fighterShip, _fighterColor, 0.0f) },
            // Current not used.
            { ShipType.Transport, (2000, 0, 40, 2.0f, RecoverOrder.Low, _transportShip, _transportShipColor, 0.0f) },
            // Half the shield of a Captial ship and twice as much power.
            // The same hitbox and speed as a Fighter, but no recovery since
            // they are not on the home team.  Rotation needs work, leave 0.0f for now.
            { ShipType.Raider, (400, 16, 50, 1.0f, RecoverOrder.None, _raiderShip, _raiderColor, 0.0f) }, // rotate 90.0f - not working as intended.
        };

        private uint _shields = 0;
        private uint _power = 0;
        private float _speed = 0.0f;
        private uint _hitbox = 0;
        private string _shipView = string.Empty;
        private Color _shipColor = Color.Empty;
        private float _rotate = 0;
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
            if (_shipsAvailable.TryGetValue(type, out (uint shields, uint power, uint hitBox, float speed, RecoverOrder recovery, string shipView, Color shipColor, float rotate) value))
            {
                _shields = value.shields;
                _power = value.power;
                _speed = value.speed;
                _hitbox = value.hitBox;
                _recovery = value.recovery;
                _shipView = value.shipView;
                _shipColor = value.shipColor;
                //make sure, we didn't mess up the rotation value in the dictionary,
                //which should be 0 to 359, where 0 means no rotation and 90 means a 90
                //degree rotation, etc.
                _rotate = Math.Clamp(value.rotate, 0, 359);
            }
        }
        /// <summary>
        /// Gets the type of the ship.
        /// </summary>
        public ShipType Type { get; }
        public Color ShipColor { get { return _shipColor; } }
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
        /// <summary>
        /// Gets the current ship in text representation.
        /// </summary>
        public string ShipView { get { return _shipView; } }
        /// <summary>
        /// Float returns current rotation value for the ship.<br/>
        /// 0 indicates that the ship doesn't rotate, to skip. 
        /// 0 to 359 are the valid options for rotation.
        /// </summary>
        public float Rotate { get { return _rotate; } }
    }
}
