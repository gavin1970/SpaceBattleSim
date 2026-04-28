using Chizl.ColorExtension;
using Chizl.ThreadSupport;
using System.Collections.Concurrent;

namespace DynamicTimeDraw
{
    /// <summary>
    /// Represents a spaceship with various attributes such as type, status, shields, power, and location.<br/>
    /// </summary>
    public class SpaceShip : DRectangleF
    {
        const string SHIP_COLR_BRUSH_KEY = "shipsColorBrush";
        //private TextLogger _logger = TextLogger.Empty;
        // Using a ConcurrentQueue to store the current location allows for
        // thread-safe updates and retrievals of the ship's position, ensuring that
        // multiple threads can interact with the ship's location without causing race
        // conditions or data corruption.
        private ConcurrentQueue<PointF> _currentLoc = new ConcurrentQueue<PointF>();
        // The hitbox is represented as a char, which can be used to store a single
        // character representation of the ship's radar distance to pick up ships,
        // based on ShipType. This drops as they take damage.
        private char _hitBox = (char)0;
        private RectangleF _hitBoxRect = RectangleF.Empty;
        // Shields represent the ship's defensive capabilities, while power represents
        // the ship's energy reserves for various systems and operations.
        private uint _orgShields = 0;
        private uint _shields = 0;
        private uint _orgPower = 0;
        private uint _power = 0;
        private float _speed = 0.5f;
        // The last attack time is stored as an Autonomous DateTime, which can be used
        // to track if currently in battle. This allows for cooldown management,
        // attack rate limiting, and other time-based mechanics in the game or
        // simulation.
        private ADateTime _lastAttack = ADateTime.MinValue;
        // The ship's type and status are stored as enums, allowing for easy categorization
        // and management of the ship's characteristics and condition.
        private ShipType _shipType = ShipType.Transport;
        // The ship's status is determined by the amount of damage it has taken, with
        // different statuses representing different levels of damage and operational
        // capability.
        private ShipStatus _shipStatus = ShipStatus.Operational;
        // The damage color is used to visually represent the ship's current status,
        // changing based on the level of damage it has sustained. This allows for
        // quick identification of the ship's condition in a visual context, such as a
        // user interface or game environment.
        private Color _damageColor = Color.Transparent;
        private Color _orgShipColor = Color.Transparent;
        private ShipMission _shipsMission = ShipMission.Idle;
        private SolidBrush _shipsColorBrush = new SolidBrush(Color.FromArgb(255, 127, 127, 127));   // default
        private Color _shipsColor = Color.FromArgb(255, 127, 127, 127); //Pure Green aka, Lime 
        private string _shipName = string.Empty;
        private bool _isEnabled = false;
        private bool _isEmpty = true;
        // special case
        private bool _isTowRig = false;
        private bool _isRaider = false;

        private ConcurrentDictionary<string, object> _customData = new ConcurrentDictionary<string, object>();

        /// <summary>
        /// Initializes a new instance of the <see cref="SpaceShip"/> class with the specified type.
        /// </summary>
        /// <param name="type">The type of the spaceship.</param>
        public SpaceShip(string name, ShipType type, Color shipColor)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                _isEmpty = false;
                _shipName = name;
                _shipType = type;

                var shipStats = new ShipStats(_shipType);
                _orgPower = shipStats.Power;
                _orgShields = shipStats.Shields;
                _speed = shipStats.Speed;
                _hitBox = (char)shipStats.Hitbox;
                _hitBoxRect = new RectangleF(this.ShipsCenter.X - _hitBox,
                                             this.ShipsCenter.Y - _hitBox,
                                             _hitBox * 2,
                                             _hitBox * 2);
                
                _isTowRig = type == ShipType.TowRig;
                _isRaider = type == ShipType.Raider;

                _orgShipColor = shipColor;
                _shipsColor = shipColor;
                // Provides a pre-built dynamic visual representations of the ship's shield status.
                _shipsColorBrush = new SolidBrush(_shipsColor);
                // Store the ship's color brush in the custom data dictionary. Used later for
                // an async thread-safe color overlay merge processing which tends to be
                // slower that required for real-time updates, so we want to avoid doing it on
                // the main thread and just swap the brush when ready.
                _customData.TryAdd(SHIP_COLR_BRUSH_KEY, _shipsColorBrush);

                // Initialize the ship's stats based on its type, setting
                // the shields, hitbox, power, and status accordingly.
                ResetStats();
            }
        }
        /// <summary>
        /// Ships are identified by their name, which is a unique string that distinguishes them from other 
        /// ships. The name can be used for various purposes, such as displaying the ship's identity in a 
        /// user interface, logging events related to the ship, or referencing the ship in game mechanics 
        /// and interactions. The name is set during the initialization of the ship and can be used throughout 
        /// its lifecycle to identify it in various contexts.
        /// </summary>
        public string Name => _shipName;
        /// <summary>
        /// Dummy constructor for testing purposes. Initializes a new instance of the <see cref="SpaceShip"/> 
        /// class with default values.
        /// </summary>
        public bool IsEmpty => _isEmpty;
        /// <summary>
        /// Gets or sets a value indicating whether the ship is enabled. An enabled ship is active and can<br/>
        /// perform operations.<br/>
        /// </summary>
        public bool Enabled { get { return !IsEmpty && _isEnabled; } set { _isEnabled = value; } }
        /// <summary>
        /// Gets the hit box value. The hit box represents the ship's radar distance to pick up other ships, and<br/>
        /// it drops as the ship takes damage. The hit box is determined based on the ship's current shield<br/>
        /// level, with higher shield levels resulting in a larger hit box. This allows for a dynamic<br/>
        /// representation of the ship's vulnerability and detection range, which can be used in various<br/>
        /// contexts such as gameplay mechanics or visual representations in a game or simulation.<br/>
        /// </summary>
        public uint HitBox => _hitBox;
        /// <summary>
        /// Gets or sets the current location of the ship.
        /// </summary>
        /// <remarks>Thread-safe updates to the ship's position are supported. Only the most recent
        /// location is retained.</remarks>
        public PointF ShipsCenter
        {
            get
            {
                if (this.IsEmpty || _currentLoc.Count == 0)
                    return new Point(0, 0);

                // Ensure that only the most recent location is kept in the queue by dequeuing older locations until
                // only one remains.
                // ## FUTURE ##
                // This can be used for ghosting effects, where the ship's previous positions are
                // briefly visible before disappearing.
                while (_currentLoc.Count() > 1)
                    _currentLoc.TryDequeue(out _);

                // TryPeek is used to retrieve the current location without removing it from the queue, ensuring that
                // the ship's position can be accessed without affecting its state. If the queue is empty, it returns a
                // default PointF(0, 0) to indicate that the ship has no defined location.
                return _currentLoc.TryPeek(out PointF retVal) ?
                                retVal : new PointF(0, 0);
            }
            set
            {
                if (this.IsEmpty)
                    return;

                // Enqueue the new location to the ConcurrentQueue, allowing for thread-safe updates to the ship's
                // position. The while loop ensures that only the most recent location is kept in the queue,
                // effectively maintaining a single current location for the ship.
                _currentLoc.Enqueue(value);
            }
        }

        /// <summary>
        /// Gets the color of the ship. The ship's color is used to visually represent the ship in various contexts,
        /// such as user interfaces or visual effects in a game or simulation. The color can be used to differentiate
        /// between different types of ships or to indicate the ship's status.
        /// </summary>
        public Color ShipsColor => _shipsColor;
        /// <summary>
        /// Gets the type of the ship. The ship type is used to categorize and differentiate between different
        /// types of ships, which can affect their behavior, capabilities, and interactions in various contexts,
        /// such as gameplay mechanics or visual representations in a game or simulation.
        /// </summary>
        public ShipType ShipType => _shipType;
        /// <summary>
        /// TowRig is a special case. 
        /// Gets a value indicating whether the vehicle is configured as a tow rig.
        /// </summary>
        public bool IsTowRig => _isTowRig;
        /// <summary>
        /// Like TowRig, Raiders are a special case. Raiders are aggressive ships that can attack other 
        /// ships and cause damage.  Gets a value indicating whether the entity is classified as a raider.
        /// </summary>
        public bool IsRaider => _isRaider;
        public bool SetTower(string towerName, float towerDistance)
        {
            if (this.IsEmpty)
                return false;

            if (NeedTowRig.TrySetFalse())
            {
                TowerName = towerName;
                TowerDistance = towerDistance;
                return true;
            }

            return false;
        }
        /// <summary>
        /// Gets or sets a value indicating whether a tow rig is required.
        /// </summary>
        public ABool NeedTowRig { get; } = ABool.False;
        /// <summary>
        /// Gets or sets the name of the tower.
        /// </summary>
        public string TowerName { get; private set; } = string.Empty;
        /// <summary>
        /// Gets or sets the distance to the tower, measured in units relevant to the application's context.
        /// </summary>
        public float TowerDistance { get; set; } = 0.0f;

        /// <summary>
        /// Gets the current mission assigned to the ship.
        /// </summary>
        public ShipMission CurrentMission
        {
            get { return _shipsMission; }
            set { _shipsMission = value; }
        }
        /// <summary>
        /// Gets the current status of the ship. The status is determined based on the ship's health and other<br/>
        /// factors, such as damage taken and shield levels. This allows for easy categorization and management<br/>
        /// of the ship's condition in various contexts, such as gameplay mechanics or visual representations.<br/>
        /// </summary>
        public ShipStatus Status => _shipStatus;
        /// <summary>
        /// Gets the color that represents damage. The damage color is determined based on the ship's current<br/>
        /// status, with different colors representing different levels of damage. For example, a ship that is<br/>
        /// operational may have a transparent damage color, while a ship that is critical may have a red damage<br/>
        /// color. This visual representation allows for quick identification of the ship's condition and can be<br/>
        /// used in various contexts, such as user interfaces or visual effects in a game or simulation.<br/>
        /// </summary>
        public Color DamageColor => _damageColor;

        /// <summary>
        /// Gets the current shield value. The shields represent the ship's defensive capabilities, and as they<br/>
        /// take damage, the shield value decreases. When the shields reach zero, the ship is considered dead.<br/>
        /// The shield value is used to determine the ship's status and can affect its performance in various<br/>
        /// ways, such as reducing power or changing its visual representation based on the damage level.<br/>
        /// </summary>
        public uint Shields => _shields; 
        /// <summary>
        /// Gets the current power value. The power represents the ship's operational capabilities, and as the ship<br/>
        /// takes damage, the power value decreases. When the power reaches zero, the ship is considered dead.<br/>
        /// The power value is used to determine the ship's status and can affect its performance in various<br/>
        /// ways, such as reducing speed or changing its visual representation based on the damage level.<br/>
        /// </summary>
        public uint Power => _power;
        /// <summary>
        /// Gets the current speed value. The speed represents the ship's movement capabilities, and as the ship<br/>
        /// takes damage, the speed value may decrease. When the speed reaches zero, the ship is considered dead.<br/>
        /// The speed value is used to determine the ship's status and can affect its performance in various<br/>
        /// ways, such as reducing maneuverability or changing its visual representation based on the damage level.<br/>
        /// </summary>
        public float Speed => _speed;

        /// <summary>
        /// Gets a value indicating whether the ship is currently in battle. A ship is considered 
        /// in battle if it is dead or if it has been attacked within the last 5 seconds.<br/>
        /// </summary>
        public bool InBattle => IsDead || _lastAttack.Value > DateTime.UtcNow.AddSeconds(-5);
        /// <summary>
        /// Gets a value indicating whether the ship is dead. A dead ship has no shields or power and is<br/>
        /// considered non-operational.<br/>
        /// </summary>
        public bool IsDead => _shipStatus == ShipStatus.Dead;
        /// <summary>
        /// Gets a value indicating whether the ship is in a critical condition. A critical ship has very low<br/>
        /// shields and power, and is at risk of being destroyed.<br/>
        /// </summary>
        public bool IsCritical => _shipStatus == ShipStatus.Critical;
        /// <summary>
        /// Gets a value indicating whether the ship is damaged. A damaged ship has sustained significant damage<br/>
        /// but is not yet critical.<br/>
        /// </summary>
        public bool IsDamaged => _shipStatus == ShipStatus.Damaged;
        /// <summary>
        /// Gets a value indicating whether the ship is scratched. A scratched ship has minor damage and is<br/>
        /// still largely operational.<br/>
        /// </summary>
        public bool IsScratched => _shipStatus == ShipStatus.Scratched; 
        /// <summary>
        /// Gets a value indicating whether the ship is operational. An operational ship has no significant damage<br/>
        /// and is fully functional.<br/>
        /// </summary>
        public bool IsOperational => _shipStatus == ShipStatus.Operational;
        /// <summary>
        /// Gets the current damage level as a value between 0 and 1.
        /// </summary>
        /// <remarks>A value of 0 indicates no damage, while a value of 1 indicates maximum damage. The
        /// damage level is calculated based on the ratio of current shields to maximum shields.</remarks>
        /// public double DamageLevel => 1 - ((double)(_shields / _orgShields));
        public double DamageLevel => _orgShields == 0 ? 0 : 100.0 - ((_shields / (double)_orgShields) * 100.0);

        /// <summary>
        /// Gets the brush used to represent the ship's color. The color can be customized through the 
        /// ship's custom data, allowing for dynamic visual representations based on the ship's status 
        /// or other factors. If a custom brush is not set, it defaults to the ship's current color, 
        /// which may change based on damage levels and status. This allows for flexible and dynamic 
        /// visual representations of the ship in various contexts, such as user interfaces or visual 
        /// effects in a game or simulation.<br/>
        /// </summary>
        public Brush ShipsColorBrush
        {
            get 
            {
                if (_customData.TryGetValue(SHIP_COLR_BRUSH_KEY, out var brush))
                    return (Brush)brush;

                return _shipsColorBrush;
            }
        }
        /// <summary>
        /// Applies damage to the ship, reducing its shields and power accordingly. The ship's status is updated<br/>
        /// based on the new shield level, determining whether it is still operational, scratched, damaged,<br/>
        /// critical, or dead.<br/>
        /// </summary>
        /// <param name="damage">The amount of damage to apply to the ship.</param>
        public void TakeDamage(uint damage, string byWho)
        {
            if (this.IsEmpty || _shipStatus == ShipStatus.Dead)
                return;
            
            var afterDmg = _shields - damage;
            // log incoming data, trying to figure out why no damage is being taken, but ships are still dying.
            // _logger.WriteLine(LogLevel.Debug, $"Ship '{Name}' @ '{Location}' is taking damage '{damage}' from '{byWho}'.  Shields are now '{afterDmg}'");
            _lastAttack.AdjustTime(DateTime.UtcNow);

            // Reduce the ship's shields by the damage taken, and update the power based on the new shield level.
            // The power is calculated as a percentage of the maximum power based on the current shield level,
            // allowing for a dynamic relationship between shields and power.
            if (afterDmg <= 0 || afterDmg > int.MaxValue)
            {
                _shields = 0;
                _power = 0;
                _shipStatus = ShipStatus.Dead;
            }
            else
                _shields -= damage;

            // Update the ship's status based on the new shield level, determining whether it is still operational,
            // scratched, damaged, critical, or dead.
            UpdateStatus();
        }
        /// <summary>
        /// Updates the ship's status based on its current shield level. The status can be operational,<br/>
        /// scratched, damaged, critical, or dead. The damage color is also updated accordingly.<br/>
        /// </summary>
        private void UpdateStatus()
        {
            var alpha = 128;
            var dmgLevel = DamageLevel;     
            var prevDamageColor = _damageColor;

            if (_shields == 0)
            {
                _shipStatus = ShipStatus.Dead;
                _damageColor = Color.FromArgb(alpha, Color.Black);
            }
            else if (dmgLevel >= 90.0)
            {
                _damageColor = Color.FromArgb(alpha, Color.Red);
            }
            else if (dmgLevel >= 75.0)
            {
                _shipStatus = ShipStatus.Critical;
                _damageColor = Color.FromArgb(alpha, Color.OrangeRed);
            }
            else if (dmgLevel >= 50.0)
            {
                _shipStatus = ShipStatus.Damaged;
                _damageColor = Color.FromArgb(alpha, Color.Orange);
            }
            else if (dmgLevel >= 25.0)
            {
                _shipStatus = ShipStatus.Scratched;
                _damageColor = Color.FromArgb(alpha, Color.Yellow);
            }
            else 
            {
                _shipStatus = ShipStatus.Operational;
                _damageColor = Color.Transparent;
            }

            if (_damageColor != prevDamageColor)
            {
                GetUpdatedShipsColorBrushAsync(_damageColor).ContinueWith(task =>
                {
                    if (task.IsCompletedSuccessfully)
                    {
                        var newBrush = task.Result;
                        _customData.TryUpdate(SHIP_COLR_BRUSH_KEY, newBrush, _shipsColorBrush);
                        _shipsColorBrush = newBrush;
                    }
                });
            }
        }
        private ABool _inUseColorBrush = ABool.False;
        private Task<SolidBrush> GetUpdatedShipsColorBrushAsync(Color damageColor)
        {
            return Task.Run(() =>
            {
                if (!_inUseColorBrush.TrySetTrue())
                    return _shipsColorBrush;
                try
                {
                    _shipsColor = ColorConvert.GetOverlayColor(damageColor, _orgShipColor);
                    var newBrush = new SolidBrush(_shipsColor);
                    _customData.TryUpdate(SHIP_COLR_BRUSH_KEY, newBrush, _shipsColorBrush);

                    return newBrush;
                } 
                finally
                {
                    _inUseColorBrush.SetFalse();
                }
            });
        }
        /// <summary>
        /// Resets the ship's stats to their initial values, including shields, power, status, and damage color.<br/>
        /// </summary>
        public void ResetStats()
        {
            if (this.IsEmpty)
                return;
            
            _power = _orgPower;
            _shields = _orgShields;
            _shipsMission = ShipMission.Idle;

            UpdateStatus();
        }
    }
}
