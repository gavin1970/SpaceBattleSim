using Chizl.StandAloneLogging;
using Chizl.ThreadSupport;
using System.Collections.Concurrent;
using System.Diagnostics;
using static DDefaults;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SpaceBattleSim
{
    /// <summary>
    /// Can be used as an abstract or standalone class request for boxing 
    /// operations, encapsulating properties for identification, z-order,
    /// creation time, and rectangular bounds.
    /// </summary>
    internal class ItemReq : IDisposable
    {
        const int _alternateShadowDepth = 7;
        internal static readonly object _logLocker = new();
        internal static Logger _logger = Logger.Empty;

        // battle time tracking
        private static TimeSpan _battleTime = TimeSpan.Zero;

        // Start time of the current battle, used to track the duration of battles and reset times
        // when all ships are dead.
        private static ADateTime _startBattle = ADateTime.MinValue;
        // Reference to the parent form, used for drawing and subscribing to mouse
        // events for interaction with the item.
        private Form _parentForm = new() { Name = DateTime.Now.ToString($"DummyForm_HHmmssffff"), Visible = false };

        /// <summary>
        /// Provides a StringFormat configured to center text both horizontally and vertically.
        /// </summary>
        internal static readonly StringFormat _centerText = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        // ConcurrentDictionary is used for thread-safe operations on the collection of spaceships,
        // allowing for concurrent reads and writes without the need for external locking. This is
        // crucial in a simulation where multiple threads may be updating ship statuses, locations,
        // and handling combat interactions simultaneously.
        internal static readonly ConcurrentDictionary<string, SpaceShip> _allSpaceShips = new ConcurrentDictionary<string, SpaceShip>();
        /// <summary>
        /// So we don't have than 1 ship on Repair for the same fighter.
        /// </summary>
        internal static readonly ConcurrentDictionary<string, SpaceShip> _spaceShipsInRepair = new ConcurrentDictionary<string, SpaceShip>();
        // By making FormStatus static, we can have a shared event status across all instances of ItemReq,
        // which can be useful for tracking global refresh and states that affect all items.
        internal static readonly EventStatus _formStatus = new();
        // This flag is used to ensure that only one combat scan runs at a time across all ships, preventing
        // race conditions and ensuring thread safety when multiple ships are engaged in battle simultaneously.
        // It is set to true when a scan is in progress, and other scans will check this flag before starting
        // to avoid overlapping operations that could lead to inconsistent state or performance issues.
        private ABool _isInBattleCheck = ABool.False;
        // This flag is set to true when any ship has its type set, which triggers the battle logic in the
        // drawing code. It is used to avoid unnecessary checks and logic for space battles when no ships
        // are active, improving performance by only engaging the battle code when needed.
        private ABool _isSpaceBattle = ABool.False;
        // This flag is used to trigger the drawing of laser flashes on the UI thread during the
        // Paint event.
        private ABool _showFire = ABool.False;
        // Tracks the time of the last combat action to manage the duration of visual effects like
        // laser flashes.
        private DateTime _lastCombatTime = DateTime.MinValue;
        // Stores the last engaged target's location and the time of the last attack so a
        // laser flash line can be drawn on the UI thread during the next Paint pass.
        private PointF _lastTargetLocation = PointF.Empty;
        // Tracks how many consecutive scans the ship has been in the same location, which can be
        // used to determine if a RepairRig has reached a dead ship and should stop trying to pull
        // it home after a certain threshold, preventing it from getting stuck indefinitely if
        // something goes wrong with the pathfinding or if the target is unreachable for some reason.
        private int _lastTargetLocationCount = 0;
        // Cached next destination set by the background battle task so the synchronous
        // movement code can act on it in the same frame instead of always wandering.
        private PointF _pendingDestination = PointF.Empty;
        // Tracks the name of the currently locked target so the ship keeps chasing
        // across frames without waiting for a new async scan each time.
        private string _activeTargetName = string.Empty;
        // Throttle the async combat scan so it doesn't spawn a Task every 20ms.
        // Steering toward a locked target still happens every frame (sync, cheap).
        private DateTime _lastScanTime = DateTime.MinValue;
        // 150ms is a good balance for responsiveness without overloading the thread pool with
        // combat scans when many ships are active. At 1px/frame, it allows for up to ~7px of
        // steering lag, which is acceptable for this type of simulation.
        private static readonly TimeSpan _scanInterval = TimeSpan.FromMilliseconds(150);
        /// <summary>
        /// Flag to determine whether to use Unicode characters for ship representation.
        /// </summary>
        private static bool _unicodeShips = true;
        // This SpaceShip instance represents the current ship associated with this ItemReq.
        // It is initialized with default values (empty name, RepairRig type, and white color)
        // and will be updated when the ship type is set using the SetShiptType method. This allows
        // each ItemReq to have its own ship information, which can be used for tracking status,
        // location, and other properties relevant to the space battle simulation.
        private SpaceShip _spaceShip = new SpaceShip(string.Empty, ShipType.RepairRig, false);
        // EventStatus is used to track the state of various events related to the ItemReq, such
        // as mouse interactions and refresh states. By using an EventStatus instance, we can
        // manage these states in a centralized way, allowing for easy checking and updating of
        // event-related flags without needing multiple separate variables for each event type.
        private readonly EventStatus _eventStatus = new();

        // default vars
        private char _shadowOpacity = DEF_SHDW_OPACITY;
        private char _borderWidth = DEF_BORDER_WIDTH;
        private char _shadowDepth = DEF_SHDW_DEPTH;
        private Color _shadowColor = DEF_NO_CLR;
        private DRectangleF  _rectangleF = DRectangleF.Default;
        private DLine _dLine = new();
        private DText _dText = new();
        private bool disposedValue;

        #region Constructors
        ~ItemReq()=> Dispose(disposing: false);
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (_parentForm != null)
                    {
                        _parentForm.MouseMove -= _parentForm_MouseMove;
                        _parentForm.MouseUp -= _parentForm_MouseUp;
                        _parentForm.MouseDown -= _parentForm_MouseDown;
                        // Since this is multithreaded and all instances of this class have reference to the
                        // parent, we don't want to close the parent and dispose it for each open instance.
                        ///_parentForm.Close();    // Close the dummy form to ensure it is not visible or interactable.
                        ///_parentForm.Dispose();  // Ensure the dummy form does not consume resources.
                    }
                }

                disposedValue = true;
            }
        }
        /// <summary>
        /// Dispose class instance and free resources. Since the ItemReq class is designed
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        /// <summary>
        /// Initializes a new empty instance of the ItemReq class.
        /// </summary>
        /// <remarks>Creates a dummy invisible parent form to satisfy drawing requirements without impacting the application.</remarks>
        private ItemReq() 
        {
            // Use the dummy form for the empty instance to satisfy the requirement of
            // having a parent form for drawing, without affecting the actual application.
            this.IsEmpty = true;

            // initialize the event to prevent validate subscribers .
            this.MouseDown += (e, args) => { };      
            this.MouseUp += (e, args) => { };
            this.MouseMove += (e, args) => { };

            // Since the Form was required to have a value, used a dummy form for the empty instance to satisfy the
            // requirement of having a parent form for drawing, without affecting the actual application. Close and
            // Dispose out this dummy form immediately since it is not needed and should not be visible or
            // interactable in any way.
            _parentForm?.Close();    // Close the dummy form to ensure it is not visible or interactable.
            _parentForm?.Dispose();  // Ensure the dummy form does not consume resources.
        }
        /// <summary>
        /// Initializes a new instance of the BoxingReq class with the specified name, rectangle, and optional z-order.<br/>
        /// </summary>
        /// <param name="name">The name associated with the boxing request.</param>
        /// <param name="itemType">The type of the item, such as Square, Diamond, Text, etc.</param>
        /// <param name="rectangle">The rectangle defining the area for the boxing request.</param>
        /// <param name="zOrder">The z-order value for the boxing request. Defaults to -1, meaning construction create time will be the order.</param>
        public ItemReq(Form parentForm, string name)
        {
            if(parentForm == null)
                throw new ArgumentNullException(nameof(parentForm), "Parent form cannot be null.");

            lock (_logLocker)
            {
                // static object, set once for all creations, so we don't have to
                // worry about threading issues or multiple loggers being created.
                // Logger is ThreadSafe and designed to be asyncronous, so we can
                // set it once here without worrying about conflicts or performance
                // issues.
                if (_logger.IsEmpty)
                {
                    if(Logger.Static.IsEmpty)
                        Logger.Static = Logger.Default;

                    _logger = Logger.Static;
                    _logger.WriteLine(LogLevel.Application, $"Logger initialized for ItemReq instances. ItemReq.Name: {name}");
                    _startBattle = ADateTime.UtcNow;
                    _battleTime = TimeSpan.Zero;
                }
            }

            // passing reference from the form that creates the ItemReq instance, so we can subscribe to
            // its mouse events for interaction with the item, such as clicking animations, and to have a
            // valid reference for drawing operations. This allows the ItemReq to respond to user
            // interactions and update its state accordingly based on mouse events.
            _parentForm = parentForm;
            // Mouse events are used for interaction with the item, such
            // as clicking animations, so we subscribe to them here.
            _parentForm.MouseMove += _parentForm_MouseMove;
            _parentForm.MouseUp += _parentForm_MouseUp;
            _parentForm.MouseDown += _parentForm_MouseDown;

            _rectangleF = new DRectangleF(RectangleF.Empty, _parentForm.Size);

            this.MouseDown += (e, args) => { };      // initialize the event to prevent validate subscribers .
            this.MouseUp += (e, args) => { };        // initialize the event to prevent validate subscribers .
            this.MouseMove += (e, args) => { };      // initialize the event to prevent validate subscribers .
            this.Name = name;

            if (!_eventStatus.Set($"{Name}_MouseOver", false))
                throw new ArgumentNullException(nameof(parentForm), $"Parent form Name {Name}.");

            if (!_eventStatus.Set($"{Name}_MouseDown", false))
                throw new ArgumentNullException(nameof(parentForm), $"Parent form Name {Name}.");

            if (!_eventStatus.Set($"{Name}_MouseInRectangle", false))
                throw new ArgumentNullException(nameof(parentForm), $"Parent form Name {Name}.");

            // Set the creation time in UTC to ensure consistent
            // sorting and comparison regardless of the local
            // time zone of the system where the code is running.
            this.CreatedUtc = DateTime.UtcNow;
            // Converted here, for less overhead.
            this.Created = this.CreatedUtc.ToLocalTime();
            // Since the Rectangle property is designed to keep the Location
            // and Size properties in sync, we can set it directly here
            // to initialize those properties as well.
            //~this.Refresh();
        }
        #endregion

        /// <summary>
        /// Occurs when the mouse pointer is over the this ItemReq and a mouse button is pressed.
        /// </summary>
        public event MouseEventHandler MouseDown;
        /// <summary>
        /// Occurs when the mouse pointer is over the this ItemReq and a mouse button is released.
        /// </summary>
        public event MouseEventHandler MouseUp;
        /// <summary>
        /// Occurs when the mouse pointer is over the this ItemReq.
        /// </summary>
        public event MouseEventHandler MouseMove;

        #region Updatable Action Properties
        /// <summary>
        /// Gets or sets a value indicating whether the element is visible.
        /// </summary>
        public bool Visible { get; set; } = false;
        /// <summary>
        /// Center point of homebase.
        /// </summary>
        public PointF HomeBaseLocation { get; set; } = PointF.Empty;
        /// <summary>
        /// Gets or sets a value indicating whether a click animation is enabled.<br/>
        /// When clicked and if shadow depth is greater than 0, the item will move to the 
        /// shadow position for a click effect.  When released, it will return to its original 
        /// position.
        /// </summary>
        public bool AnimateClick { get; set; } = false;
        /// <summary>
        /// Gets or sets a value indicating whether the item should hide when mouse isn't in its vicinity.
        /// </summary>
        public bool InActiveHide { get; set; } = false;
        /// <summary>
        /// Gets or sets the drawing order of the element.
        /// </summary>
        public int ZOrder { get; set; } = -1;
        #endregion

        #region Animation Properties
        /// <summary>
        /// Gets or sets a value indicating whether animation is enabled.  
        /// This means the item will move around on the screen.
        /// </summary>
        public bool Animation { get; set; } = false;
        /// <summary>
        /// Gets or sets the distances pixels from starting location to next random destination location.<br/>
        /// Used for auto setting of next destination point when the item reaches its current destination, 
        /// creating a continuous movement effect.<br/>
        /// </summary>
        public uint DestinationRange { get; set; } = 4096;
        /// <summary>
        /// Gets or sets the destination point for the animation.
        /// </summary>
        public PointF NextDestination { get; set; } = PointF.Empty;
        /// <summary>
        /// Gets or sets the destination point for the animation.
        /// </summary>
        public PointF LastDestination { get; set; } = PointF.Empty;
        /// <summary>
        /// Gets or sets a value indicating whether ghost effects are enabled. Creates a ghost affect as the<br/>
        /// item moves, leaving behind a fading trail that visually represents the path of the animation.<br/>
        /// </summary>
        public bool GhostEffect { get; set; } = false;
        /// <summary>
        /// Gets or sets a value indicating whether drunk effects are enabled.<br/>
        /// Creates a drunk effect as the item moves, causing it to sway or wobble unpredictably.
        /// </summary>
        public bool DrunkEffect { get; set; } = false;
        /// <summary>
        /// Gets or sets a value indicating whether the item has a directional sprite for the 
        /// ship.  Only use if the ship has a directional view, otherwise it will just be a 
        /// square.  This is used to determine whether to rotate the sprite based on the movement 
        /// direction, or to keep it static.  If true, the item will rotate to face the direction 
        /// of movement, creating a more dynamic and visually engaging animation effect.  If false, 
        /// the item will maintain its original orientation regardless of movement direction, 
        /// which can be suitable for items that do not have a specific facing direction or when 
        /// a simpler visual style is desired.
        /// </summary>
        public bool HasDirectionalSprite { get; set; } = false;
        #endregion

        #region Readonly Identification Properties
        /// <summary>
        /// Gets an empty instance of the ItemReq class.
        /// </summary>
        public static ItemReq Empty { get; } = new ItemReq();
        /// <summary>
        /// Gets a value indicating whether the object contains no data.
        /// </summary>
        public bool IsEmpty { get; } = false;
        /// <summary>
        /// Gets or sets the name of this specific Box.
        /// </summary>
        public string Name { get; } = DateTime.Now.ToString($"Square_HHmmssffff");
        /// <summary>
        /// Only available for Raiders.
        /// Allows a raider to transfer half their power to their shields, when they drop below 25% shields.
        /// </summary>
        public bool CriticalTransfer { get; set; } = false;
        /// <summary>
        /// Gets the date and time when the instance was created.
        /// </summary>
        public DateTime Created { get; } = DateTime.Now;
        /// <summary>
        /// Gets the date and time when the entity was created, in Coordinated Universal Time (UTC).<br/>
        /// Backup for sorting by creation time if ZOrder is not set or the same as another.
        /// </summary>
        public DateTime CreatedUtc { get; } = DateTime.UtcNow;
        #endregion

        #region Text and Font Properties
        public DText DText { get { return _dText; } }
        #endregion

        #region Box Fill/Border Color and Thickness 
        /// <summary>
        /// Gets a brush that is used to fill the background with the specified color.
        /// </summary>
        public Brush BackColor { get { return new SolidBrush(BGColor); } }
        /// <summary>
        /// Gets or sets the fill color.<br/>
        /// Default: Red
        /// </summary>
        public Color BGColor { get; set; } = DEF_NO_CLR;
        /// <summary>
        /// Gets or sets the color of the border.<br/>
        /// Default: Black
        /// </summary>
        public Color BorderColor { get; set; } = DEF_NO_CLR;
        /// <summary>
        /// Gets or sets the color used when the mouse pointer is directly over the<br/>
        /// control. Set as Color.Empty for no effect during mouse over, which will<br/>
        /// cause the control to use the regular BackColor instead.<br/>
        /// </summary>
        public Color MouseOverColor { get; set; } = DEF_NO_CLR;
        /// <summary>
        /// Gets a brush used to paint the background when the mouse pointer is over the control.
        /// </summary>
        public Brush MouseOverBackColor { get { return MouseOverColor.IsEmpty ? BackColor : new SolidBrush(MouseOverColor); } }
        ///// <summary>
        ///// Gets or sets the width of the border, in pixels.  Zero '0' will represent no border.<br/>
        ///// Default: 3px, Max: 255px
        ///// </summary>
        public uint BorderWidth { get { return _borderWidth; } set { _borderWidth = (char)value; } }
        /// <summary>
        /// Gets a new Pen with the specified border color and width.
        /// </summary>
        public Pen GetBorder { get { return new Pen(BorderColor, BorderWidth); } }
        /// <summary>
        /// Gets or sets the mouse HitBox outside the ItemReq value in pixels.
        /// </summary>
        public uint HitBox { get; set; } = 0;
        #endregion

        #region Shadowing properties
        /// <summary>
        /// Gets a brush representing the shadow color and opacity.
        /// </summary>
        public Brush Shadowing { get { return new SolidBrush(Color.FromArgb(_shadowOpacity, _shadowColor)); } }
        /// <summary>
        /// Gets or sets the color used for rendering shadows.<br/>
        /// If the alpha component of the color is greater than 0, it will 
        /// also update the ShadowOpacity to match the alpha value, ensuring 
        /// consistency between the shadow color and its opacity level.
        /// </summary>
        public Color ShadowColor 
        { 
            get { return _shadowColor; } 
            set 
            {
                _shadowOpacity = (char)value.A;
                _shadowColor = value;
            }
        }
        /// <summary>
        /// Gets or sets the opacity level of the shadow.<br/>
        /// The value should be between 0 (completely transparent) and 255 (completely opaque).<br/>
        /// When set, it will also update the alpha component and the ShadowColor to ensure that the 
        /// shadow's color and opacity are consistent.
        /// </summary>
        public uint ShadowOpacity { 
            get { return _shadowOpacity; } 
            set 
            { 
                _shadowOpacity = (char)value;
                _shadowColor = Color.FromArgb(_shadowOpacity, _shadowColor.R, _shadowColor.G, _shadowColor.B);
            }
        }
        /// <summary>
        /// Gets or sets the shadow depth in pixels.  Zero '0' will represent no shadowing.<br/>
        /// Default: 10px, Max: 255px
        /// </summary>        
        public uint ShadowDepth { get { return _shadowDepth; } set { _shadowDepth = (char)value; } }
        /// <summary>
        /// Gets or sets a value indicating whether box shadowing is enabled.
        /// </summary>
        public bool BoxShadowing { get; set; } = false;
        #endregion

        #region User Controler Methods, e.g. Mouse Hit Testing
        /// <summary>
        /// Gets the current spaceship information.
        /// </summary>
        public SpaceShip ShipInfo => _spaceShip;
        /// <summary>
        /// Gets or sets a value indicating whether Unicode characters are used instead of image representations.<br/>
        /// If set to false, Ship images are loaded from .\skins\ folder only once and shared for all other ships of the same type. If image doesn't exist, a question mark will be displayed.<br/>
        /// Default: true
        /// </summary>
        /// <remarks>Set this property to <see langword="true"/> to display Unicode symbols in place of
        /// images. This may improve compatibility with text-based environments or accessibility tools.</remarks>
        public static bool UnicodeShips { get { return _unicodeShips; } set { _unicodeShips = value; } }
        /// <summary>
        /// Determines whether all raider ships or all repair rigs are in the dead state, indicating that a dead reset
        /// is required.
        /// </summary>
        /// <remarks>Use this method to check if a reset condition is met based on the status of all
        /// raider ships or all repair rigs. This can be useful for triggering game state changes or recovery logic when
        /// all critical ship types are incapacitated.</remarks>
        /// <returns>true if all raider ships or all repair rigs are dead; otherwise, false.</returns>
        public static bool AnyRaiderAlive => _allSpaceShips.Where(w => w.Value.IsRaider && w.Value.Status != ShipStatus.Dead).Any();
        public static bool AnyAllyAlive => _allSpaceShips.Where(w => !w.Value.IsRaider && w.Value.Status != ShipStatus.Dead).Any();
        public static bool NeedsDeadReset()
        {
            if (!AnyRaiderAlive || !AnyAllyAlive)
                return true;

            return false;
        }
        /// <summary>
        /// Resets only the ships that are currently marked as Dead, 
        /// allowing them to be reused without affecting the status of 
        /// operational ships. This method iterates through all ships 
        /// and calls ResetStats() on those that are dead, restoring 
        /// them to their initial state while leaving alive ships 
        /// unchanged and vunerable.
        /// </summary>
        public static void ResetDeadShips()
        {
            var utc = ADateTime.UtcNow;
            _battleTime = utc - _startBattle.Value;

            var anyRaiderAlive = AnyRaiderAlive;
            var anyAllyAlive = AnyAllyAlive;
            var winMessage = anyRaiderAlive && anyAllyAlive ? "Manual Reset" : anyRaiderAlive ? "Raiders win" : "Ally win";

            BattleStats.SaveAudit(_startBattle.Value, utc, winMessage);

            // reset everyone, so we can start fresh without worrying about the state of any ship. This is simpler
            // and more reliable than trying to selectively reset only dead ships, which could lead to edge cases
            // where some ships are not properly reset or where the logic becomes too complex to maintain. By
            // resetting all ships, we ensure a consistent starting point for each battle and avoid potential
            // bugs related to ship status.
            foreach (var ship in _allSpaceShips)
                _allSpaceShips[ship.Key].ResetStats();

            _spaceShipsInRepair.Clear();
            _startBattle = ADateTime.UtcNow;
        }
        /// <summary>
        /// Retrieves the status information for all spaceships, either as detailed records or as grouped summaries.
        /// </summary>
        /// <remarks>When details is set to true, each element in the returned array contains the name,
        /// type, status, location, power, hit box, and, if applicable, the current mission of a spaceship. When details
        /// is false, each element summarizes the count of ships by type and status.</remarks>
        /// <param name="details">true to return detailed information for each spaceship; false to return grouped summaries by ship type and
        /// status. The default is true.</param>
        /// <returns>An array of strings containing either detailed status information for each spaceship or grouped summaries,
        /// depending on the value of the details parameter.</returns>
        public static string[] GetShipStatus(bool stats = true)
        {
            string[] status = { };
            List<string> retVal = new List<string>();

            if (stats)
            {
                var header = $"| {CreatePaddedString("Type", 9)} | {CreatePaddedString("Shields", 7)} | " +
                             $"{CreatePaddedString("Power", 10)} | {CreatePaddedString("HitBox", 6)} | " +
                             $"{CreatePaddedString("Speed", 5)} | {CreatePaddedString("Recovery", 8)} | " +
                             $"{CreatePaddedString("Crit", 4)} | {CreatePaddedString("Image", 5)} ";

                retVal.Add(new string('-', header.Length));
                retVal.Add(header);
                retVal.Add(new string('-', header.Length));

                foreach (ShipType sType in Enum.GetValues(typeof(ShipType)))
                {
                    // unused, skip.
                    if (sType == ShipType.Transport || sType == ShipType.Bomber)
                        continue;
                    
                    var shipStats = new ShipStats(sType);
                    // RepairRig doesn't do damage, so show 0 dps for clarity instead of "Power * 33.33333333333333" which would be misleading.
                    // Power * 3 is the formula for calculating dps for all other ship types and comes from the refresh timer
                    // that occurs .30 of a second muliplied by ShipTypes->Power, rounded.
                    var dps = sType == ShipType.RepairRig ? 0 : shipStats.Power * 3;
                    retVal.Add($"| {CreatePaddedString($"{sType}", 9)} | {CreatePaddedString($"{shipStats.Shields}", 7)} | " +
                                 $"{CreatePaddedString($"{shipStats.Power:00} - {(dps)}dps", 10)} | {CreatePaddedString($"{shipStats.Hitbox}", 6)} | " +
                                 $"{CreatePaddedString($"{shipStats.Speed}", 5)} | {CreatePaddedString($"{shipStats.Recovery}", 8)} | " +
                                 $"{CreatePaddedString($"{shipStats.HasCritalTransfer}", 4)} |{CreatePaddedString($" {shipStats.ShipView}", 4)} ");
                }
            }
            else
            {
                var header = $"| {CreatePaddedString("Type", 9)} | {CreatePaddedString("Total", 5)} | " +
                             $"{CreatePaddedString("Alive", 6)} | {CreatePaddedString("Dead", 4)} ";

                retVal.Add($"This Battle Time: {(ADateTime.UtcNow.Value - _startBattle.Value).ToString(@"hh\:mm\:ss")}");
                retVal.Add($"Last Total Battle Time: {_battleTime}");

                retVal.Add(new string('-', header.Length));

                retVal.Add(new string('-', header.Length));
                retVal.Add(header);
                retVal.Add(new string('-', header.Length));

                foreach (ShipType sType in Enum.GetValues(typeof(ShipType)))
                {
                    // unused, skip.
                    if (sType == ShipType.Transport || sType == ShipType.Bomber)
                        continue;

                    var totalByType = _allSpaceShips.Where(w => w.Value.ShipType == sType).Count();
                    var deadByType = _allSpaceShips.Where(w => w.Value.ShipType == sType && w.Value.Status == ShipStatus.Dead).Count();
                    retVal.Add($"| {CreatePaddedString($"{sType}", 9)} | {CreatePaddedString($"{totalByType}", 5)} | " +
                               $"{CreatePaddedString($"{totalByType-deadByType}", 6)} | {CreatePaddedString($"{deadByType}", 4)} ");
                }
            }

            return retVal.ToArray();
        }
        /// <summary>
        /// Sets the type of the spaceship.
        /// </summary>
        /// <param name="stype">The type of the spaceship.</param>
        public void SetShiptType(ShipType sType)
        {
            if (sType == ShipType.Raider)
                Debug.WriteLine("here");

            // When setting the ship type, we create a new SpaceShip
            // instance with the specified type and color,
            _spaceShip = new SpaceShip(Name, sType, CriticalTransfer);
            _allSpaceShips.TryAdd(this.Name, _spaceShip);
            _isSpaceBattle.TrySetTrue();
        }
        /// <summary>
        /// Draws a Item with a shadow, background, border, and an 'X' symbol onto the specified graphics
        /// surface.
        /// </summary>
        /// <param name="g">The graphics surface on which to draw the item.</param>
        /// <returns>true if the item was drawn successfully; otherwise, false.</returns>
        public bool DrawItem(Graphics g)
        {
            try
            {
                int boxShadowDepth = this.BoxShadowing ? (this.ShadowDepth == 0 ? _alternateShadowDepth : (int)this.ShadowDepth) : 0;
                if (DLine.DrawList.Count > 0 && DLine.HasAnchor && this.HomeBaseLocation.IsEmpty)
                    this.HomeBaseLocation = DLine.DrawList.First().Start;
                
                if(_allSpaceShips.TryGetValue(this.Name, out _))
                {
                    // if animation was off, that means it was dead, its not dead anymore,
                    // meaning this ship was repaired, but the _spaceShip doesn't have
                    // access to this class, so lets reset it's state.  RepairRig are an
                    // exception, they can be repaired but not fight, so they don't get
                    // the animation treatment.
                    if (!this.Animation && !_spaceShip.IsDead && !_spaceShip.IsRepairRig)
                        this.Animation = true;
               }

                // Calculate the rectangle for the close button based on form size and padding
                //var frmW = ParentSize.Width - _parentForm.Right;
                RectangleF clsBtnRect = this.Rectangle;
                if (this.Animation || (_spaceShip.IsRepairRig && !_spaceShip.IsDead))
                {
                    if (this._dText.Text != this._dText.OrgText)
                        this._dText.Text = this._dText.OrgText;

                    if (this.NextDestination.IsEmpty)
                        this.NextDestination = this.Location;

                    // --- Per-frame target tracking ---
                    // The combat scan handles both steering (via _pendingDestination) and damage.
                    // It is throttled to _scanInterval to avoid flooding the thread pool when many
                    // ships fight simultaneously. At 1px/frame the steering lag is at most ~7px
                    // per interval, which is imperceptible.
                    if (_isSpaceBattle)
                    {
                        // Throttled async scan: steering + damage + new-target search at most once per _scanInterval.
                        var now = DateTime.UtcNow;
                        if (!_isInBattleCheck.Value && (now - _lastScanTime) >= _scanInterval)
                        {
                            _lastScanTime = now;

                            Task.Run(() =>
                            {
                                // TrySetTrue() is atomic — if it returns false, another task already
                                // owns the lock, so bail out immediately without touching SetFalse().
                                if (!_isInBattleCheck.TrySetTrue())
                                    return;

                                try
                                {
                                    var hitBox = (float)_spaceShip.HitBox;
                                    var hitBoxSq = hitBox * hitBox;
                                    var myLoc = _spaceShip.Location;        // _spaceShip.Location is the same as this.Center

                                    if (!string.IsNullOrEmpty(_activeTargetName))
                                    {
                                        // Verify locked target is still alive; steer toward it and deal damage if in range.
                                        if (_allSpaceShips.TryGetValue(_activeTargetName, out var locked) && (_spaceShip.IsRepairRig ? locked.IsDead : !locked.IsDead))
                                        {
                                            // Using distance squared (distSq) for comparison to avoid the overhead of
                                            // calculating the square root when determining proximity to targets. Better
                                            // use of memory and CPU than Math.sqrt() when we only need relative distances
                                            // for comparison against hitBoxSq and closestDist.
                                            float distSq = locked.DistanceFrom(myLoc);
                                            if (distSq <= hitBoxSq)
                                            {
                                                _lastTargetLocation = locked.Location;
                                                _lastCombatTime = DateTime.UtcNow;
                                                if (!_spaceShip.IsRepairRig)
                                                {
                                                    locked.TakeDamage(_spaceShip.Power, this.Name);
                                                    _allSpaceShips[_activeTargetName] = locked;
                                                }
                                                else
                                                {
                                                    _spaceShip.CurrentMission = ShipMission.HeadingHome;
                                                    //BattleStats.Audit(this.Name, ActionType.Heal, $"Healed: {_activeTargetName}");
                                                    _allSpaceShips[_activeTargetName].ResetStats(this.Name, true);
                                                    _spaceShipsInRepair.TryRemove(_activeTargetName, out _);
                                                    _spaceShipsInRepair.Where(w => w.Value.Name == this.Name).ToList().ForEach(s => _spaceShipsInRepair.TryRemove(s.Key, out _));

                                                    _activeTargetName = string.Empty;
                                                    this.NextDestination = this.HomeBaseLocation;
                                                }
                                            }
                                            else if (_spaceShip.IsRepairRig)
                                            {
                                                // RepairRig can still pull from range, so update
                                                // destination even if not in hit box.
                                                this.NextDestination = locked.Location;
                                            }
                                            else
                                            {
                                                // Break lock if target is out of range, so the ship can
                                                // search for a new one instead of chasing a lost cause.
                                                _activeTargetName = string.Empty;
                                            }
                                        }
                                        else
                                        {
                                            if (_spaceShip.IsRepairRig)
                                            {
                                                _spaceShipsInRepair.TryRemove(_activeTargetName, out _);
                                                _spaceShipsInRepair.Where(w => w.Value.Name == this.Name).ToList().ForEach(s => _spaceShipsInRepair.TryRemove(s.Key, out _));
                                                _spaceShip.CurrentMission = ShipMission.Idle;
                                            }

                                            // Target is dead or gone — clear lock.
                                            _activeTargetName = string.Empty;
                                        }
                                    }
                                    else if (_spaceShip.IsRepairRig && _spaceShip.CurrentMission == ShipMission.HeadingHome)
                                    {
                                        if (!_lastTargetLocation.Equals(myLoc))
                                        {
                                            _lastTargetLocation = myLoc;
                                            _lastTargetLocationCount = 1;
                                        }
                                        else
                                            _lastTargetLocationCount++;

                                        float distSq = _spaceShip.DistanceFrom(HomeBaseLocation);
                                        if (distSq <= hitBoxSq || _lastTargetLocationCount > 100)
                                        {
                                            this.Animation = false;
                                            _spaceShip.CurrentMission = ShipMission.Idle;
                                            _spaceShip.ResetStats("HomeBase_Healer", false);
                                            _activeTargetName = string.Empty;
                                        }

                                        this.NextDestination = this.HomeBaseLocation;
                                    }
                                    else if (_spaceShip.IsRepairRig && _spaceShip.CurrentMission == ShipMission.Idle)
                                    {
                                        List<SpaceShip> deadShipsList = _allSpaceShips.Where(w =>
                                                                        !w.Value.IsEmpty && w.Value.Name != this.Name &&
                                                                        w.Value.IsDead && w.Value.Recovery != 0)
                                                                 .Select(s => s.Value).ToList();

                                        if (deadShipsList.Count == 0)
                                        {
                                            this.Animation = false;
                                            _activeTargetName = string.Empty;
                                            if (this.NextDestination != this.HomeBaseLocation)
                                            {
                                                _spaceShip.CurrentMission = ShipMission.Idle;
                                                _lastTargetLocation = this.NextDestination;
                                                this.NextDestination = this.HomeBaseLocation;
                                            }
                                        }
                                        else
                                        {
                                            var inOrderOfRecovery= deadShipsList.OrderByDescending(o => o.Recovery).ToArray();
                                            foreach (var kvp in inOrderOfRecovery)
                                            {
                                                if (_spaceShipsInRepair.TryAdd(kvp.Name, _spaceShip))
                                                {
                                                    float distSq = _allSpaceShips[kvp.Name].DistanceFrom(myLoc);
                                                    _allSpaceShips[kvp.Name].SetRepairRig(this.Name, distSq);
                                                    this.NextDestination = kvp.Center;

                                                    this.Animation = true;
                                                    _spaceShip.CurrentMission = ShipMission.OnRepair;
                                                    _activeTargetName = kvp.Name;

                                                    _lastTargetLocation = _pendingDestination;
                                                    _pendingDestination = kvp.Center;

                                                    _lastCombatTime = DateTime.UtcNow;
                                                    break;
                                                }
                                            }

                                            if(_spaceShip.CurrentMission != ShipMission.OnRepair && _spaceShip.CurrentMission != ShipMission.HeadingHome)
                                            {
                                                _spaceShip.CurrentMission = ShipMission.Idle;
                                                _lastTargetLocation = _pendingDestination;
                                                _pendingDestination = this.HomeBaseLocation;
                                                this.NextDestination = this.HomeBaseLocation;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // No locked target — scan for any enemy in detection range.
                                        SpaceShip? closest = null;
                                        List<SpaceShip> allShips = new List<SpaceShip>();
                                        double closestDist = double.MaxValue;

                                        allShips = _allSpaceShips.Where(w =>
                                                                        !w.Value.IsEmpty && w.Value.Name != this.Name &&
                                                                        !w.Value.IsDead && w.Value.Location.X != 0 &&
                                                                        w.Value.Location.Y != 0).Select(s => s.Value).ToList();


                                        if (allShips.Count > 0)
                                        {
                                            if (_spaceShip.IsRaider)
                                                allShips = allShips.Where(w => w.ShipType != ShipType.Raider).ToList();
                                            else
                                                allShips = allShips.Where(w => w.ShipType == ShipType.Raider).ToList();


                                            if (allShips.Count > 0)
                                            {
                                                foreach (var kvp in allShips) 
                                                {
                                                    // Using distance squared (distSq) for comparison to avoid the overhead of
                                                    // calculating the square root when determining proximity to targets. Better
                                                    // use of memory and CPU than Math.sqrt() when we only need relative distances
                                                    // for comparison against hitBoxSq and closestDist.
                                                    float distSq = kvp.DistanceFrom(myLoc);
                                                    var inHitBox = distSq <= hitBoxSq;

                                                    // if this is a damaged raider and the target is a raider, no matter what, go to them.
                                                    // or if in the hitbox already, keep them as a target, even if they are not a raider, because
                                                    // we want to stay and fight until we die or they die, we don't want to run away from a
                                                    // fight if we are already in it.

                                                    if (inHitBox && distSq < closestDist)
                                                    {
                                                        closestDist = distSq;
                                                        closest = kvp;
                                                    }
                                                }
                                            }
                                        }

                                        if (closest != null)
                                        {
                                            _pendingDestination = closest.Location;
                                            _lastTargetLocation = closest.Location;
                                            _activeTargetName = closest.Name;
                                            _lastCombatTime = DateTime.UtcNow;
                                            _allSpaceShips[closest.Name].TakeDamage(_spaceShip.Power, Name);
                                        }
                                    }

                                    // keep it up to date for other threads, even if it didn't change here,
                                    // so the latest status is always visible to the UI and other tasks.
                                    _allSpaceShips[_spaceShip.Name] = _spaceShip;

                                    if (_spaceShip.IsDead)
                                    {
                                        this.Animation = false;
                                        this._dText.Text = $"{this._dText.DeadDisplay}";
                                    }
                                }
                                catch(Exception ex)
                                {
                                    _logger.WriteLine(LogLevel.Error, $"Error in combat scan for '{Name}': {ex.Message}");
                                    if (_spaceShip.IsRepairRig)
                                        this.NextDestination = this.HomeBaseLocation;
                                }
                                finally
                                {
                                    // Only reached if TrySetTrue() succeeded above, so SetFalse() is always safe here.
                                    _isInBattleCheck.SetFalse();
                                }
                            });
                        }
                    }

                    // by using X or Y instead of both, we can create a more dynamic movement
                    // pattern where the item can move in straight lines along the axes,
                    // creating a more varied and less predictable animation effect.
                    var x = this.Location.X;
                    var y = this.Location.Y;

                    // Consume any pending destination from the combat scan above every frame,
                    // so steering toward an enemy overrides the current path immediately.
                    if (!_pendingDestination.IsEmpty && !_spaceShip.IsRepairRig)
                    {
                        // we don't want _spaceShip.IsRepairRig random walking around, they
                        // should only move toward targets and home base.
                        var lX = Math.Min(_pendingDestination.X, x);
                        var hX = Math.Min(_pendingDestination.X, x);
                        var lY = Math.Min(_pendingDestination.Y, y);
                        var hY = Math.Min(_pendingDestination.Y, y);

                        // this way theyare not on top of each other constantly.
                        var rX = Random.Shared.Next((int)lX, (int)hX) + (lX - (int)lX);
                        var rY = Random.Shared.Next((int)lY, (int)hY) + (lY - (int)lY);

                        this.NextDestination = new PointF(
                            Math.Clamp(rX, 0, ParentSize.Width - this.ShipInfo.Width),
                            Math.Clamp(rY, 0, ParentSize.Height - this.Height));

                        _pendingDestination = PointF.Empty;
                    }
                    else if ((this.Location.X == this.NextDestination.X ||
                             this.Location.Y == this.NextDestination.Y) && !_spaceShip.IsRepairRig) 
                    {
                        x += Random.Shared.Next(-(int)this.DestinationRange, (int)this.DestinationRange + 1);
                        y += Random.Shared.Next(-(int)this.DestinationRange, (int)this.DestinationRange + 1);

                        this.NextDestination = new PointF(
                            Math.Clamp(x, 0, ParentSize.Width - _spaceShip.HitBoxRect.Width),
                            Math.Clamp(y, 0, ParentSize.Height - _spaceShip.HitBoxRect.Height)
                        );

                        this.LastDestination = this.Location;
                    }

                    if (_isSpaceBattle)
                    {
                        if (this.Location.X < this.NextDestination.X)
                            x = Math.Min(this.Location.X + _spaceShip.Speed, this.NextDestination.X);
                        else
                            x = Math.Max(this.Location.X - _spaceShip.Speed, this.NextDestination.X);
                        if (this.Location.Y < this.NextDestination.Y)
                            y = Math.Min(this.Location.Y + _spaceShip.Speed, this.NextDestination.Y);
                        else
                            y = Math.Max(this.Location.Y - _spaceShip.Speed, this.NextDestination.Y);
                    }

                    this.Location = new PointF(x, y);

                    if (_isSpaceBattle)
                    {
                        // Location based on the center of the ship instead of the top-left
                        // corner of the rectangle, so movement and hit detection are more
                        // intuitive and visually aligned with the ship's position.
                        _spaceShip.Location = this.Center;
                        // update the shared ship info with the latest position and
                        // status so other ships can see it during their async scans.
                        _allSpaceShips[Name] = _spaceShip;
                    }

                    clsBtnRect = this.Rectangle;
                }

                // Shadow rectangle for the close button, offset by shadowing depth
                RectangleF clsBtnShdwRect = new RectangleF(this.Left + boxShadowDepth, this.Top + boxShadowDepth, this.Width, this.Height);

                // Calculate the horizontal center of the close button for
                // potential use in drawing the "X" symbol or other centered elements.
                var lineMoves = (clsBtnRect.X + (clsBtnRect.Width / 2));

                // If the close button is active (mouse over + left click), use
                // the shadow rectangle for drawing to create a "pressed" effect
                if (_eventStatus.Get($"{Name}_MouseDown") && this.AnimateClick && this.BoxShadowing)
                {
                    // If the button is in the pressed state, we can use the
                    // shadow rectangle for drawing to create a "pressed" effect.
                    clsBtnRect = new RectangleF(clsBtnShdwRect.Location, clsBtnShdwRect.Size);
                    // Adjust line to be based on the shadow rectangle's position
                    // to keep the lines centered within the pressed Item.
                    lineMoves = (clsBtnRect.X + (clsBtnRect.Width / 2)) - lineMoves;
                }
                else
                    lineMoves = 0;

                if ((!_eventStatus.Get($"{Name}_MouseDown") || !this.AnimateClick) && this.BoxShadowing)
                    g.FillRectangle(this.Shadowing, clsBtnShdwRect);     // Shadow of button

                // Background of button
                if (!_eventStatus.Get($"{Name}_MouseDown") && _eventStatus.Get($"{Name}_MouseInRectangle"))
                    g.FillRectangle(this.MouseOverBackColor, clsBtnRect);
                else
                    g.FillRectangle(this.BackColor, clsBtnRect);

                // Extra lines for testing, can be used for debugging or additional visual elements.
                // They will be drawn on top of the button background and shadow, but below the border
                // to ensure they are visible without obscuring the Items edges.
                // UNUSED AT THE MOMENT.
                switch(this.DLine.ItemType)
                {
                    case ItemType.Text:
                    case ItemType.Square:
                    case ItemType.Diamond:
                    case ItemType.Ellipse:
                    case ItemType.Lines:
                    case ItemType.Custom:
                    default:
                        LineDraw(g, clsBtnRect, lineMoves);
                        break;
                }

                // Text of ItemReq, only drawn when TextEnabled is true.  This allows us to have text
                // content that can be toggled on or off without affecting the underlying properties.
                if (this._dText.IsEnabled)
                {
                    if ((_unicodeShips || !_isSpaceBattle) && this._dText.HasShadowing)
                    {
                        clsBtnShdwRect = new RectangleF(clsBtnRect.X + (int)this.ShadowDepth, clsBtnRect.Y + (int)this.ShadowDepth, clsBtnRect.Width, clsBtnRect.Height);
                        g.DrawString(this._dText.Text, this._dText.DFont, this._dText.ForeColorShadow.Brush, clsBtnShdwRect, _centerText);
                    }

                    if (_isSpaceBattle)
                    {
                        if (_unicodeShips)
                            g.DrawString(this._dText.Text, this._dText.DFont, _spaceShip.ShipsColorBrush, clsBtnRect, _centerText);
                        else
                        {
                            g.DrawImage(_spaceShip.ShipImage, clsBtnRect);
                            g.FillEllipse(_spaceShip.ShipsImgOverlayBrush, clsBtnRect);
                        }
                    }
                    else
                        g.DrawString(this._dText.Text, this._dText.DFont, this._dText.ForeColor.Brush, clsBtnRect, _centerText);

                    if (_isSpaceBattle && _spaceShip.Status != ShipStatus.Dead)
                    {
                        // Draw the detection radius as a circle correctly centered on the ship
                        // and sized to match ShipInfo.HitBox (the actual combat range).
                        var hbR = (float)ShipInfo.HitBox;
                        //var dmg = (float)ShipInfo.DamageLevel;
                        var shipCx = clsBtnRect.X + clsBtnRect.Width / 2;
                        var shipCy = clsBtnRect.Y + clsBtnRect.Height / 2;

                        var hbRect = new RectangleF(shipCx - hbR, shipCy - hbR, hbR * 2, hbR * 2);
                        g.DrawEllipse(_spaceShip.HitboxCircle, hbRect);

                        // If the ship has recently fired (within the last 300ms), draw a laser line toward the last target
                        // location to visually indicate an attack, creating a dynamic combat effect that shows the direction
                        // of fire and adds visual feedback to the battle interactions. _showFire is true every other frame,
                        // causing a blink.
                        if (_showFire.TrySetTrue())
                        {
                            // Draw a brief laser flash line toward the last engaged target.
                            if (!_lastTargetLocation.IsEmpty &&
                                (DateTime.UtcNow - _lastCombatTime).TotalMilliseconds < 300)
                            {
                                var pen = _spaceShip.IsRepairRig ? DDefaults.DEF_REPAIR_LASER_LINE : DDefaults.DEF_LASER_LINE;
                                g.DrawLine(pen, new PointF(shipCx, shipCy), _lastTargetLocation);
                            }
                            else if ((DateTime.UtcNow - _lastCombatTime).TotalMilliseconds >= 300)
                            {
                                _lastTargetLocation = PointF.Empty;
                            }
                        }
                        else
                        {
                            _showFire.SetFalse();
                        }
                    }
                }

                // Border of button
                g.DrawRectangle(this.GetBorder, clsBtnRect);
            }
            catch (Exception ex)
            {
                // Handle any exceptions that may occur during drawing,
                // such as issues with graphics context or invalid parameters.
                _logger.WriteLine(LogLevel.Critical, ex.Message);
                return false;
            }

            return true;
        }
        /// <summary>
        /// Determines whether the specified mouse position is within the rectangle, optionally including the shadow
        /// area and an expanded hit area.
        /// </summary>
        /// <param name="mousePos">The mouse position to test, in parent coordinates.</param>
        /// <returns>true if the mouse position is within the rectangle or the expanded area; otherwise, false.</returns>
        public bool IsMouseInRect(PointF mousePos) 
        {
            try
            {
                if (_isSpaceBattle)
                    return false;

                // First, check if the mouse position is within the bounds of
                // the parent form to avoid unnecessary calculations.
                if (mousePos.X < 0 || mousePos.Y < 0 || mousePos.X > ParentSize.Width || mousePos.Y > ParentSize.Height)
                    return false;

                // If the rectangle is empty or not visible, we can immediately
                // return false without performing any calculations.
                if (this.IsEmpty)
                    return false;

                // If right over the rectangle, we can return true immediately
                // without needing to calculate the expanded hit area.
                if (this.Rectangle.Contains(mousePos))
                    return true;

                // Ensure HitBox is non-negative and does not exceed half of the
                // smaller dimension of the rectangle to prevent invalid expansion.
                var hitboxWidth = (float)Math.Min(HitBox, this.Width);
                var hitboxHeight = (float)Math.Min(HitBox, this.Height);

                // If shadow is included, we need to consider the shadow depth as part of
                // the hitbox to ensure the hit test accounts for the shadow area.
                if (this.AnimateClick && this.BoxShadowing)
                {
                    // If shadow is included, expand the rectangle to include the shadow area.
                    hitboxWidth += (this.ShadowDepth == 0 ? _alternateShadowDepth : (int)this.ShadowDepth);
                    hitboxHeight += (this.ShadowDepth == 0 ? _alternateShadowDepth : (int)this.ShadowDepth);
                }

                // Create an expanded rectangle that includes
                // the hitbox around the original rectangle.
                var expandedRect = new RectangleF(
                        this.Location.X - hitboxWidth,
                        this.Location.Y - hitboxHeight,
                        this.Width + (3 * hitboxWidth),
                        this.Height + (3 * hitboxHeight)
                    );

                if (expandedRect.Contains(mousePos))
                {
                    // dont want invalidate every time the mouse moves in
                    // the close button area, only when it first enters
                    if (!_eventStatus.Get($"{Name}_MouseOver"))
                        _eventStatus.Set($"{Name}_MouseOver", true);
                }
                // if mouse out was in Item area and is currently not, then remove
                // mouseover event and invalidate to redraw.
                else if (_eventStatus.Get($"{Name}_MouseOver"))
                    _eventStatus.Set($"{Name}_MouseOver", false);

                // Truth: mouse position is within the core rectangle, we can return true.
                // Might use this for something else later.
                expandedRect = new RectangleF(
                        _rectangleF.Location.X,
                        _rectangleF.Location.Y,
                        _rectangleF.Size.Width,
                        _rectangleF.Size.Height
                    );

                return expandedRect.Contains(mousePos);
            } 
            catch (Exception ex)
            {
                // Handle any exceptions that may occur during the hit test,
                // such as issues with rectangle calculations or invalid parameters.
                _logger.WriteLine(LogLevel.Critical, ex.Message);
                return false;
            }
        }
        /// <summary>
        /// Determines whether the mouse pointer is within the rectangle, 
        /// optionally including the shadow and expanding the hit area.
        /// </summary>
        /// <param name="mouseX">The X-coordinate of the mouse pointer.</param>
        /// <param name="mouseY">The Y-coordinate of the mouse pointer.</param>
        /// <returns>true if the mouse pointer is within the specified area; otherwise, false.</returns>
        public bool IsMouseInRect(uint mouseX, uint mouseY) => 
                    this.IsMouseInRect(new PointF((int)mouseX, (int)mouseY));
        #endregion

        /// <summary>
        /// Gets the DLine associated with this item.
        /// </summary>
        public DLine DLine { get { return _dLine; } }

        #region Full Region properties with synchronization to Rectangle
        public RectangleF Rectangle
        {
            get { return _rectangleF.Rectangle; }
            set { this.Location = value.Location; this.Size = value.Size; }
        }
        public PointF Location
        {
            get { return _rectangleF.Location; }
            set { _rectangleF.Location = value; }
        }
        public SizeF Size
        {
            get { return _rectangleF.Size; }
            set { _rectangleF.Size = value; }
        }
        public float Left
        {
            get { return _rectangleF.Left; }
            set { _rectangleF.Left = value; }
        }
        public float Right
        {
            get { return _rectangleF.Right; }
        }
        public float Top
        {
            get { return _rectangleF.Top; }
            set { _rectangleF.Top = value; }
        }
        public float Bottom
        {
            get { return _rectangleF.Bottom; }
        }
        public float Width
        {
            get { return _rectangleF.Width; }
            set { _rectangleF.Width = value; }
        }
        public float Height
        {
            get { return _rectangleF.Height; }
            set { _rectangleF.Height = value; }
        }
        public PointF Center
        {
            get { return _rectangleF.Center; }
        }
        #endregion

        #region Event handlers for parent form events to trigger redraws
        /// <summary>
        /// Handles mouse movement over the parent form to determine if the cursor is within the close button's
        /// interactive area.
        /// </summary>
        /// <param name="sender">The source of the event, typically the parent form.</param>
        /// <param name="e">The MouseEventArgs containing data about the mouse movement.</param>
        /// <remarks>Handles mouse movement over the parent form to determine if the cursor is within the close button's interactive area.</remarks>
        private void _parentForm_MouseMove(object? sender, MouseEventArgs e)
        {
            // Check if the mouse is within the rectangle area of the close button, including the shadow area and
            // an expanded hit area for better usability. Max hitbox is button size to prevent excessive expansion
            // that could lead to unintended interactions. If button is 50px wide, max hitbox is 50px, so the hit
            // area can have a height of 150px and 150px width (50px button + 50px hitbox on each side). Anything
            // larger than button size will be set to size * 3.
            if (this.IsMouseInRect(e.Location))
            {
                _eventStatus.Set($"{Name}_MouseInRectangle", true);
                // Only invoke MouseMove when there are subscribers to prevent unnecessary overhead,
                // since this can be triggered frequently when moving the mouse around the form.
                this.MouseMove?.Invoke(this, e); 
            }
            else if (_eventStatus.Get($"{Name}_MouseInRectangle") && 
                    e.X > this.Padding.Left &&  
                    e.X < (this.ParentSize.Width - this.Padding.Right) && 
                    e.Y > this.Padding.Top && 
                    e.Y < this.ParentSize.Height - this.Padding.Bottom)
            {
                _eventStatus.Set($"{Name}_MouseInRectangle", false);
                // Only invoke MouseMove when there are subscribers to prevent unnecessary overhead,
                // since this can be triggered frequently when moving the mouse around the form.
                this.MouseMove?.Invoke(this, e); 
            }
        }
        /// <summary>
        /// Handles mouse down events on the parent form to update mouse status and invalidate the form as needed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The MouseEventArgs containing event data.</param>
        private void _parentForm_MouseDown(object? sender, MouseEventArgs e)
        {
            if (_eventStatus.Get($"{Name}_MouseInRectangle"))
            {
                // If the left mouse button is pressed while the cursor is
                // within the rectangle area, we should update the mouse
                // down status and invalidate the form to trigger a redraw
                // with the pressed effect.
                if (e.Button == MouseButtons.Left)
                {
                    _eventStatus.Set($"{Name}_MouseDown", true);
                }
                // Invoke the MouseDown event to allow external handling
                // of the mouse down action on this item.
                this.MouseDown?.Invoke(this, e);
            }
        }
        /// <summary>
        /// Handles the MouseUp event to update the close button state and close the parent form after a delay
        /// when the left mouse button is released over the close button.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The MouseEventArgs containing event data.</param>
        /// <remarks>Closes the parent form on the UI thread after a short visual feedback delay when the close button is released.</remarks>
        private void _parentForm_MouseUp(object? sender, MouseEventArgs e)
        {
            // if mouse up happens outside the button area but the button is currently in
            // mouse down state, we should reset it back to normal state and invalidate to
            // redraw.
            if (_eventStatus.Get($"{Name}_MouseDown"))
            {
                _eventStatus.Set($"{Name}_MouseDown", false);
            }

            // If the mouse is released while still within the Item area and the left mouse button
            // was used, we should reset the button state and close the run expected function after
            // a short delay to allow the user to see the the release effect.
            if (_eventStatus.Get($"{Name}_MouseInRectangle"))
                this.MouseUp?.Invoke(this, e);
        }
        #endregion

        #region Helper methods and properties
        private static string CreatePaddedString(string label, int totalLength)
        {
            if (totalLength - label.Length <= 0)
                return label;

            return label.PadRight(totalLength, ' ');
        }
        private void LineDraw(Graphics g, RectangleF clsBtnRect, float lineMoves)
        {
            foreach (var line in this.DLine.DrawList)
            {
                // seperated to own vars to make it easier to read.
                var start = new PointF(line.Start.X + lineMoves,
                                      line.Start.Y + lineMoves);
                var startShadow = new PointF(line.Start.X + lineMoves + ((int)this.DLine.ShadowPen.Width) + 1,
                                      line.Start.Y + lineMoves + ((int)this.DLine.ShadowPen.Width) + 1);
                PointF end;
                if (this.DLine.HasAnchor)
                    end = new PointF(clsBtnRect.X + (clsBtnRect.Width / 2), clsBtnRect.Y + (clsBtnRect.Height / 2));
                else
                    end = new PointF(line.End.X + lineMoves, line.End.Y + lineMoves);

                if (this.DLine.ShadowPen.Width > 0)
                {
                    var endShadow = new PointF(end.X + ((int)this.DLine.ShadowPen.Width) + 1, end.Y + ((int)this.DLine.ShadowPen.Width) + 1);
                    g.DrawLine(this.DLine.ShadowPen, startShadow, endShadow);
                }
                g.DrawLine(this.DLine.Pen, start, end);
            }
        }
        /// <summary>
        /// Gets the client area size of the parent form.
        /// </summary>
        private SizeF ParentSize { get { return _parentForm?.ClientSize ?? SizeF.Empty; } }
        /// <summary>
        /// Gets the padding within the parent form.
        /// </summary>
        private Padding Padding { get { return _parentForm?.Padding ?? Padding.Empty; } }
        #endregion
    }
}
