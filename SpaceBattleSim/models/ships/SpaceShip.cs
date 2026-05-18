using Chizl.ColorExtension;
using Chizl.ThreadSupport;
using System.Collections.Concurrent;
// using System.Numerics; //Vector2 is not used in the current implementation, but it can be useful for future enhancements or alternative distance calculations.

namespace SpaceBattleSim
{
    /// <summary>
    /// Represents a spaceship with various attributes such as type, status, shields, power, and location.<br/>
    /// </summary>
    public class SpaceShip : DRectangleF
    {
        private static readonly Color SHIP_COLOR_DEFAULT = Color.FromArgb(255, Color.ForestGreen);
        private static readonly Color SHIP_IMG_CLR_DEFAULT = Color.FromArgb(0, Color.Black);
        private static readonly List<Pen> _hitboxLowestCircleList = new List<Pen>() {
            new Pen(Color.FromArgb(10, Color.Red), 1),
            new Pen(Color.FromArgb(100, Color.Red), 1),
            new Pen(Color.FromArgb(25, Color.Silver), 1),
            new Pen(Color.FromArgb(50, Color.Silver), 1),
            new Pen(Color.FromArgb(75, Color.Silver), 1),
            new Pen(Color.FromArgb(100, Color.Silver), 1)
        };
        private static readonly List<Pen> _hitboxLowCircleList = new List<Pen>() {
            new Pen(Color.FromArgb(10, Color.Red), 1),
            new Pen(Color.FromArgb(100, Color.Red), 1),
            new Pen(Color.FromArgb(25, Color.Orange), 1),
            new Pen(Color.FromArgb(50, Color.Orange), 1),
            new Pen(Color.FromArgb(75, Color.Orange), 1),
            new Pen(Color.FromArgb(100, Color.Orange), 1)
        };
        private static readonly List<Pen> _hitboxMidCircleList = new List<Pen>() {
            new Pen(Color.FromArgb(10, Color.Red), 1),
            new Pen(Color.FromArgb(100, Color.Red), 1),
            new Pen(Color.FromArgb(25, Color.Cyan), 1),
            new Pen(Color.FromArgb(50, Color.Cyan), 1),
            new Pen(Color.FromArgb(75, Color.Cyan), 1),
            new Pen(Color.FromArgb(100, Color.Cyan), 1)
        };
        private static readonly List<Pen> _hitboxHighCircleList = new List<Pen>() {
            new Pen(Color.FromArgb(10, Color.Red), 1),
            new Pen(Color.FromArgb(100, Color.Red), 1),
            new Pen(Color.FromArgb(25, Color.Green), 1),
            new Pen(Color.FromArgb(50, Color.Green), 1),
            new Pen(Color.FromArgb(75, Color.Green), 1),
            new Pen(Color.FromArgb(100, Color.Green), 1)
        };

        private enum SHIP_BRUSH_TYPE
        {
            TEXT,
            IMAGE
        }

        // The hitbox is represented as a char, which can be used to store a single
        // character representation of the ship's radar distance to pick up ships,
        // based on ShipType. This drops as they take damage.
        private char _hitBox = (char)0;
        private int _orgShields = 0;
        private int _shields = 0;
        private int _orgPower = 0;
        private int _power = 0;
        private float _speed = 0.5f;
        private int _recovery = 0;
        private ADateTime _nextCriticalTransfer = ADateTime.UtcNow;
        private bool _criticalTransfer = false;
        private string _shipsView = string.Empty;
        private string _shipsViewOrig = string.Empty;
        private float _rotate = 0.0f;
        private Pen _hitboxCircle = _hitboxHighCircleList[5];   //default, alpha will be changed based on damage level.
        // The last attack time is stored as an Atomic DateTime, which can be used
        // to track if currently in battle. This allows for cooldown management,
        // attack rate limiting, and other time-based mechanics in the game or
        // simulation.
        private ADateTime _lastAttack = ADateTime.MinValue;
        // The reset flag is used to prevent multiple threads from trying to reset
        // the ship's stats simultaneously, ensuring that the reset operation is
        // thread-safe and does not cause race conditions or data corruption.
        private ABool _reset = ABool.False;
        // The in-use color brush flag is used to prevent multiple threads from
        // trying to update the ship's color brush simultaneously, ensuring that
        // the color update operation is thread-safe and does not cause race
        // conditions or data corruption when multiple threads attempt to update
        // the ship's visual representation based on damage levels or status changes.
        private ABool _inUseColorBrush = ABool.False;
        private ShipType _shipType = ShipType.Transport;
        private ShipStatus _shipStatus = ShipStatus.Operational;
        private Color _damageColor = Color.Empty;
        private Color _orgShipColor = SHIP_COLOR_DEFAULT;
        private Color _shipsColor = SHIP_COLOR_DEFAULT;
        private Color _shipsImgColor = SHIP_IMG_CLR_DEFAULT;
        private SolidBrush _shipsColorBrush = new SolidBrush(SHIP_COLOR_DEFAULT);       // default
        private SolidBrush _shipsImageBrush = new SolidBrush(SHIP_IMG_CLR_DEFAULT);     // default
        private ShipMission _shipsMission = ShipMission.Idle;

        private string _shipName = string.Empty;
        private bool _isEnabled = false;
        private bool _isEmpty = true;
        private bool _isRepairRig = false;
        private bool _isRaider = false;

        private ConcurrentDictionary<SHIP_BRUSH_TYPE, SolidBrush> 
            _customData = new ConcurrentDictionary<SHIP_BRUSH_TYPE, SolidBrush>();

        #region Public Properties
        /// <summary>
        /// Initializes a new instance of the <see cref="SpaceShip"/> class with the specified type.
        /// </summary>
        /// <param name="type">The type of the spaceship.</param>
        public SpaceShip(string name, ShipType type, bool criticalTransfer)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                // for audit purposes, we want to add the ship to the audit
                // log as soon as it's created, so we can track its stats
                // throughout the match.
                BattleStats.AddShip(name, type);

                _isEmpty = false;
                _shipName = name;
                _shipType = type;

                var shipStats = new ShipStats(_shipType);
                _orgPower = shipStats.Power;
                _orgShields = shipStats.Shields;
                _speed = shipStats.Speed;
                _hitBox = (char)shipStats.Hitbox;
                _criticalTransfer = criticalTransfer || shipStats.HasCritalTransfer;

                if (criticalTransfer && !shipStats.HasCritalTransfer)
                    ShipStats.SetCriticalTransfer.Add(_shipType);

                _isRepairRig = type == ShipType.RepairRig;
                _isRaider = type == ShipType.Raider;
                _recovery = (int)shipStats.Recovery;
                _shipsView = shipStats.ShipView;
                _shipsViewOrig = _shipsView;
                _rotate = shipStats.Rotate;

                _orgShipColor = shipStats.ShipColor;
                _shipsColor = shipStats.ShipColor;
                _shipsImgColor = Color.FromArgb(64, shipStats.ShipColor);
                // Provides a pre-built dynamic visual representations of the ship's shield status.
                _shipsColorBrush.Dispose();
                _shipsColorBrush = new SolidBrush(_shipsColor);
                _shipsImageBrush.Dispose();
                _shipsImageBrush = new SolidBrush(_shipsImgColor);
                // Store the ship's color brush in the custom data dictionary. Used later for
                // an async thread-safe color overlay merge processing which tends to be
                // slower that required for real-time updates, so we want to avoid doing it on
                // the main thread and just swap the brush when ready.
                _customData.TryAdd(SHIP_BRUSH_TYPE.TEXT, _shipsColorBrush);
                _customData.TryAdd(SHIP_BRUSH_TYPE.IMAGE, _shipsImageBrush);

                // Initialize the ship's stats based on its type, setting
                // the shields, hitbox, power, and status accordingly.
                ResetStats(string.Empty);
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
        public bool Enabled 
        { 
            get { return !IsEmpty && _isEnabled; } 
            set { _isEnabled = value; } 
        }
        /// <summary>
        /// Gets the current view representation of the ship or ships.
        /// </summary>
        public string ShipView => _shipsView;
        /// <summary>
        /// Gets the current rotation value of the ship.   If the ship by default is facing up, then a rotation 
        /// of 90 means the ship is rotated 90 degrees to the right, 180 means it is facing down, and 270 means 
        /// it is facing left. The rotation value can be used to determine the ship's orientation in various 
        /// contexts, such as visual representations in a game or simulation, or for calculating movement and 
        /// interactions based on the ship's facing direction.<br/>
        /// </summary>
        public float RotateShip 
        { 
            get { return _rotate; } 
            set { _rotate = Math.Clamp(value, 0, 359); } 
        }

        /// <summary>
        /// Gets the hit box value. The hit box represents the ship's radar distance to pick up other ships, and<br/>
        /// it drops as the ship takes damage. The hit box is determined based on the ship's current shield<br/>
        /// level, with higher shield levels resulting in a larger hit box. This allows for a dynamic<br/>
        /// representation of the ship's vulnerability and detection range, which can be used in various<br/>
        /// contexts such as gameplay mechanics or visual representations in a game or simulation.<br/>
        /// </summary>
        public int HitBox => _hitBox;
        /// <summary>
        /// The order in which a ship is recovered after being destroyed. The recovery order 
        /// can be used to determine the priority of repairs or salvage operations, with 
        /// higher recovery orders indicating a higher priority for recovery. This allows for 
        /// strategic decision-making in various contexts, such as demand, mechanics or 
        /// resource management in simulation.<br/>
        /// </summary>
        public int Recovery => _recovery;
        /// <summary>
        /// Gets the bounding rectangle that defines the hit box area around the outside of the ship.
        /// </summary>
        public RectangleF HitBoxRect
        {
            get { return new RectangleF(this.Center.X - _hitBox, this.Center.Y - _hitBox, _hitBox * 2, _hitBox * 2); }
        }

        /// <summary>
        /// Gets the pen used to draw the hitbox circle.
        /// </summary>
        public Pen HitboxCircle 
            { get { return _hitboxCircle; } }
        /// <summary>
        /// Gets the color of the ship. The ship's color is used to visually represent the ship in various contexts,
        /// such as user interfaces or visual effects in a game or simulation. The color can be used to differentiate
        /// between different types of ships or to indicate the ship's status.
        /// </summary>
        public Color ShipsColor => _shipsColor;
        /// <summary>
        /// Gets the color used to overlay onto ship images.
        /// </summary>
        public Color ShipsImgColor => _shipsImgColor;
        /// <summary>
        /// Gets the type of the ship. The ship type is used to categorize and differentiate between different
        /// types of ships, which can affect their behavior, capabilities, and interactions in various contexts,
        /// such as gameplay mechanics or visual representations in a game or simulation.
        /// </summary>
        public ShipType ShipType => _shipType;

        /// <summary>
        /// Gets the image representing the ship.
        /// </summary>
        public Image ShipImage { get { return ShipSkins.SkinImage(_shipType); } }
        /// <summary>
        /// RepairRig is a special case. 
        /// Gets a value indicating whether the vehicle is configured as a RepairRig.
        /// </summary>
        public bool IsRepairRig => _isRepairRig;
        /// <summary>
        /// Like RepairRig, Raiders are a special case. Raiders are aggressive ships that can attack other 
        /// ships and cause damage.  Gets a value indicating whether the entity is classified as a raider.
        /// </summary>
        public bool IsRaider => _isRaider;
        /// <summary>
        /// Allows a raider to transfer half their power to their shields, when they drop below 25% shields.
        /// </summary>
        public bool CriticalTransfer => _criticalTransfer;
        /// <summary>
        /// Gets or sets a value indicating whether a RepairRig is required.
        /// </summary>
        public ABool NeedRepairRig { get; } = ABool.False;
        /// <summary>
        /// Gets or sets the name of the RepairRig.
        /// </summary>
        public string RepairRigName { get; private set; } = string.Empty;
        /// <summary>
        /// Gets or sets the distance to the RepairRig, measured in units relevant to the application's context.
        /// </summary>
        public float RepairRigDistance { get; set; } = 0.0f;
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
        public int Shields => Volatile.Read(ref _shields);
        /// <summary>
        /// Gets the current shield integrity as a percentage of the original shield value.
        /// </summary>
        /// <remarks>Returns 0 if the original shield value is zero. The value represents the proportion
        /// of remaining shields relative to the initial amount, expressed as a percentage.</remarks>
        public float ShieldIntegrity => _orgShields == 0 ? 0 : (this.Shields / (float)_orgShields) * 100.0f;
        /// <summary>
        /// Gets the current power value. The power represents the ship's operational capabilities, and as the ship<br/>
        /// takes damage, the power value decreases. When the power reaches zero, the ship is considered dead.<br/>
        /// The power value is used to determine the ship's status and can affect its performance in various<br/>
        /// ways, such as reducing speed or changing its visual representation based on the damage level.<br/>
        /// </summary>
        public int Power => Volatile.Read(ref _power);
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
        public double DamageLevel => _orgShields == 0 ? 0 : 100.0 - ((this.Shields / (double)_orgShields) * 100.0);
        /// <summary>
        /// Gets the brush used to represent the ship's color. The color can be customized through the 
        /// ship's custom data, allowing for dynamic visual representations based on the ship's status 
        /// or other factors. If a custom brush is not set, it defaults to the ship's current color, 
        /// which may change based on damage levels and status. This allows for flexible and dynamic 
        /// visual representations of the ship in various contexts, such as user interfaces or visual 
        /// effects in a game or simulation.<br/>
        /// </summary>
        public SolidBrush ShipsColorBrush
        {
            get
            {
                if (_customData.TryGetValue(SHIP_BRUSH_TYPE.TEXT, out var brush))
                    return brush;

                return _shipsColorBrush;
            }
        }
        /// <summary>
        /// Gets the brush used to overlay the ship image with a color representing the ship's current damage state. 
        /// The overlay color can be customized through the ship's custom data, allowing for dynamic visual representations 
        /// based on the ship's status or other factors.
        /// </summary>
        /// <remarks>The returned brush may be customized based on internal data. If no custom brush is
        /// set, a default brush is provided. The caller is responsible for not disposing the returned brush.</remarks>
        public SolidBrush ShipsImgOverlayBrush
        {
            get
            {
                if (_customData.TryGetValue(SHIP_BRUSH_TYPE.IMAGE, out var brush))
                    return brush;

                return _shipsImageBrush;
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Attempts to set the RepairRig name and distance if the current instance is not empty and the RepairRig is not
        /// needed.
        /// </summary>
        /// <param name="repairRigName">The name of the RepairRig to assign. This value will be set if the operation succeeds.</param>
        /// <param name="repairRigDistance">The distance to the RepairRig, in units relevant to the context. This value will be set if the operation
        /// succeeds.</param>
        /// <returns>true if the RepairRig name and distance were successfully set; otherwise, false.</returns>
        public bool SetRepairRig(string repairRigName, float repairRigDistance)
        {
            if (this.IsEmpty)
                return false;

            if (NeedRepairRig.TrySetFalse())
            {
                RepairRigName = repairRigName;
                RepairRigDistance = repairRigDistance;
                return true;
            }

            return false;
        }
        /// <summary>
        /// Calculates the squared Euclidean distance from the current location to the specified point.
        /// </summary>
        /// <remarks>This method returns the squared distance rather than the actual distance. Use this
        /// value for comparisons or performance-sensitive scenarios where the exact distance is not required.</remarks>
        /// <param name="point">The target point to which the squared distance is calculated.</param>
        /// <returns>The squared distance between the current location and the specified point.</returns>
        public float DistanceFrom(PointF point)
        {
            /// Also works, but creates extra Vector2 objects and is less efficient than just doing the math directly.
            //var shipLoc = new Vector2(this.Location.X, this.Location.Y);
            //var targetLoc = new Vector2(point.X, point.Y);
            //return Vector2.DistanceSquared(shipLoc, targetLoc);

            var dx = point.X - this.Location.X;
            var dy = point.Y - this.Location.Y;
            return dx * dx + dy * dy;
        }
        /// <summary>
        /// Applies damage to the ship, reducing its shields and power accordingly. The ship's status is updated<br/>
        /// based on the new shield level, determining whether it is still operational, scratched, damaged,<br/>
        /// critical, or dead.<br/>
        /// </summary>
        /// <param name="damage">The amount of damage to apply to the ship.</param>
        public void TakeDamage(int damage, string byWho)
        {
            if (this.IsEmpty || _shipStatus == ShipStatus.Dead)
                return;

            // Because more than one ship can attack at the same time, we need to make sure that the damage is
            // applied in a thread-safe way, and that the ship's status is updated correctly based on the new
            // shield level. Because of this I moved away from:
            /// Interlocked.Exchange(ref _shields, this.Shields - damage);
            // We also want to log the incoming data to try to figure out why no damage is being taken, but ships
            // are still dying. This allows us to track the ship's condition and performance in various contexts,
            // such as gameplay mechanics or visual representations in a simulation.
            for (int i = 0; i < damage; i++)
                Interlocked.Decrement(ref _shields);

            // this.Shields is a Volatile read, so we get the current value after we potentially
            // update it with damage. We then clamp the value to ensure it does not go below zero
            // or above the original shield value.
            Interlocked.Exchange(ref _shields, Math.Clamp(this.Shields, 0, _orgShields));

            // log incoming data, trying to figure out why no damage is being taken, but ships are still dying.
            _lastAttack.AdjustTime(DateTime.UtcNow);

            // Reduce the ship's shields by the damage taken, and update the power based on the new shield level.
            // The power is calculated as a percentage of the maximum power based on the current shield level,
            // allowing for a dynamic relationship between shields and power.
            if (this.Shields == 0) // Volatile read
            {
                Interlocked.Exchange(ref _power, 0);
                _shipStatus = ShipStatus.Dead;
                BattleStats.Audit(this.Name, ActionType.Death, $"Killed by: {byWho}"); //I died
                BattleStats.Audit(byWho, ActionType.Kill, $"Killed: {this.Name}");      //this ship killed me
            }
            else
            {
                // So we get the current value before we potentially update it with Critical Transfer.
                if (ShieldIntegrity <= 25.0f && this.Power > 2 && _criticalTransfer && _nextCriticalTransfer <= ADateTime.UtcNow)
                {
                    //Volatile read, then divided by 2 for the critical transfer mechanic.
                    var prevPower = Interlocked.Exchange(ref _power, Math.Clamp(this.Power / 2, 2, _orgPower));

                    // using Critical Transfer
                    BattleStats.Audit(this.Name, ActionType.CriticalTransfer, $"Power was: {prevPower}, Power now: {_power}");
                    _nextCriticalTransfer.AdjustTime(DateTime.UtcNow.AddSeconds(2));

                    //resets health, shields
                    Interlocked.Exchange(ref _shields, _orgShields);
                    _shipsMission = ShipMission.Idle;

                    // Reset stats without resetting power, since we just updated it with the critical transfer mechanic.
                    // This allows the ship to have a chance to recover and continue fighting, while still maintaining the
                    // strategic element of managing power and shields in battle.
                    ResetStats("CritTransfer", false);
                }
                else if (ShieldIntegrity <= 25.0f)
                {
                    BattleStats.Audit(this.Name, ActionType.AlmostDead, $"Last moments are from: {byWho} ({damage} dmg). Shields at: {this.Shields} ({this.ShieldIntegrity:00}%)");
                }
                else
                {
                    BattleStats.Audit(this.Name, ActionType.UnderAttack, $"By: {byWho} ({damage} dmg). Shields at: {this.Shields} ({this.ShieldIntegrity:00}%)");
                }

            }

            // Update the ship's status based on the new shield level, determining whether it is still operational,
            // scratched, damaged, critical, or dead.
            UpdateStatus();
        }
        /// <summary>
        /// Resets the ship's stats to their initial values, including shields, power, status, and damage color.<br/>
        /// </summary>
        public void ResetStats(string byWho = "System", bool includePower = true)
        {
            if (this.IsEmpty || !_reset.TrySetTrue())
                return;
            try
            {
                if (includePower)
                    Interlocked.Exchange(ref _power, _orgPower);
                Interlocked.Exchange(ref _shields, _orgShields);
                _shipsMission = ShipMission.Idle;

                if (!string.IsNullOrWhiteSpace(byWho))
                    BattleStats.Audit(this.Name, ActionType.Heal, $"Healed: {byWho}");

                UpdateStatus();
            }
            finally
            {
                _reset.SetFalse();
            }
        }
        #endregion

        #region Support Methods
        /// <summary>
        /// Updates the ship's status based on its current shield level. The status can be operational,<br/>
        /// scratched, damaged, critical, or dead. The damage color is also updated accordingly.<br/>
        /// </summary>
        private void UpdateStatus()
        {
            var alpha = 128;
            var dmgLevel = DamageLevel;     
            var prevDamageColor = _damageColor;
            var hitboxList = _hitboxHighCircleList;
            var currentPower = this.Power;      // One time Volatile read

            if (currentPower > 0)
            {
                if (currentPower < _orgPower / 4)
                    hitboxList = _hitboxLowestCircleList;
                else if (currentPower < _orgPower / 2)
                    hitboxList = _hitboxLowCircleList;
                else if (currentPower < _orgPower)
                    hitboxList = _hitboxMidCircleList;
            }

            if (this.Shields == 0)
            {
                _hitboxCircle = hitboxList[0];
                _shipStatus = ShipStatus.Dead;
                if (!ItemReq.UnicodeShips)
                    alpha = 192;
                _damageColor = Color.FromArgb(alpha, Color.Black);
            }
            else if (dmgLevel >= 90.0)
            {
                _hitboxCircle = hitboxList[1];
                if (!ItemReq.UnicodeShips)
                    alpha = 175;
                _damageColor = Color.FromArgb(alpha, Color.BlueViolet);
            }
            else if (dmgLevel >= 75.0)
            {
                _hitboxCircle = hitboxList[2];
                _shipStatus = ShipStatus.Critical;
                if (!ItemReq.UnicodeShips)
                    alpha = 150;
                _damageColor = Color.FromArgb(alpha, Color.OrangeRed);
            }
            else if (dmgLevel >= 50.0)
            {
                _hitboxCircle = hitboxList[3];
                _shipStatus = ShipStatus.Damaged;
                if (!ItemReq.UnicodeShips)
                    alpha = 140;
                _damageColor = Color.FromArgb(alpha, Color.Orange);
            }
            else if (dmgLevel >= 25.0)
            {
                _hitboxCircle = hitboxList[4];
                _shipStatus = ShipStatus.Scratched;
                if (!ItemReq.UnicodeShips)
                    alpha = 128;
                _damageColor = Color.FromArgb(alpha, Color.Yellow);
            }
            else 
            {
                _hitboxCircle = hitboxList[5];
                _shipStatus = ShipStatus.Operational;
                if (!ItemReq.UnicodeShips)
                    _damageColor = Color.FromArgb(1, Color.Transparent);
                else
                    _damageColor = Color.Empty;

                _shipsView = _shipsViewOrig;
            }

            if (!_damageColor.IsEmpty && _damageColor != prevDamageColor)
            {
                GetUpdatedShipsColorBrushAsync(_damageColor).ContinueWith(task =>
                {
                    if (task.IsCompletedSuccessfully)
                    {
                        (var textBrush, var imageBrush) = task.Result;

                        _customData.TryUpdate(SHIP_BRUSH_TYPE.TEXT, textBrush, _shipsColorBrush);
                        _customData.TryUpdate(SHIP_BRUSH_TYPE.IMAGE, imageBrush, _shipsImageBrush);

                        _shipsColorBrush = textBrush;
                        _shipsImageBrush = imageBrush;
                    }
                });
            }
        }
        /// <summary>
        /// Asynchronously updates and retrieves the ship's color brush based on the specified damage color.
        /// </summary>
        /// <remarks>Thread safety is ensured by preventing concurrent updates to the ship's color brush.
        /// If an update is already in progress, the current brush is returned without modification.</remarks>
        /// <param name="damageColor">The color representing the current damage state to be applied as an overlay to the ship's original color.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a SolidBrush reflecting the
        /// updated ship color.</returns>
        private Task<(SolidBrush textBrush, SolidBrush imageBrush)> GetUpdatedShipsColorBrushAsync(Color damageColor)
        {
            return Task.Run(() =>
            {
                // To prevent multiple threads from trying to update the ship's color brush simultaneously,
                // we use an ABool as a atomic lock mechanism. If another thread is already updating the brush,
                // we return the current brush without making changes. This ensures thread safety while
                // allowing for asynchronous updates to the ship's color based on damage levels.
                if (!_inUseColorBrush.TrySetTrue())
                    return (_shipsColorBrush, _shipsImageBrush);
                try
                {
                    _shipsColor = ColorConvert.GetOverlayColor(Color.FromArgb(128,damageColor), _orgShipColor);
                    var newBrush = new SolidBrush(_shipsColor);
                    var color = damageColor == Color.Transparent ? Color.Transparent : Color.FromArgb(damageColor.A, Color.Black);
                    var newImgBrush = new SolidBrush(color);

                    //_customData.TryUpdate(SHIP_BRUSH_TYPE.TEXT, newBrush, _shipsColorBrush);
                    //_customData.TryUpdate(SHIP_BRUSH_TYPE.IMAGE, newImgBrush, _shipsImageBrush);

                    //_shipsColorBrush = newBrush;
                    //_shipsImageBrush = newImgBrush;

                    return (newBrush, newImgBrush);
                } 
                finally
                {
                    _inUseColorBrush.SetFalse();
                }
            });
        }
        #endregion
    }
}
