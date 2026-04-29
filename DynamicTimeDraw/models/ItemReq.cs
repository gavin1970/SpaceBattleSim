using Chizl.IO.Logging;
using Chizl.ThreadSupport;
using System.Collections.Concurrent;
using System.Diagnostics;
using static DDefaults;

namespace DynamicTimeDraw
{
    /// <summary>
    /// Can be used as an abstract or standalone class request for boxing operations, encapsulating properties for identification, z-order,
    /// creation time, and rectangular bounds.
    /// </summary>
    internal class ItemReq
    {
        const int _alternateShadowDepth = 7;

        /// <summary>
        /// Provides a StringFormat configured to center text both horizontally and vertically.
        /// </summary>
        static readonly StringFormat _centerText = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        internal static readonly ConcurrentDictionary<string, SpaceShip> _allSpaceShips = new ConcurrentDictionary<string, SpaceShip>();
        private ABool _isInBattleCheck = ABool.False;
        private ABool _isSpaceBattle = ABool.False;
        private TextLogger _logger = TextLogger.Empty;

        private Form _parentForm = new() { Name = DateTime.Now.ToString($"DummyForm_HHmmssffff"), Visible = false };

        // default vars
        private char _shadowOpacity = DEF_SHDW_OPACITY;
        private char _borderWidth = DEF_BORDER_WIDTH;
        private char _shadowDepth = DEF_SHDW_DEPTH;
        private Color _shadowColor = DEF_NO_CLR;
        private DRectangleF  _rectangleF = DRectangleF.Default;
        private DLine _dLine = new();
        private DText _dText = new();
        private Pen _hitboxCircle = new Pen(Color.FromArgb(64, Color.Silver), 1);

        // side fun, watch ships fight each other, when enabled by SpaceBattle property.
        private SpaceShip _spaceShip = new SpaceShip(string.Empty, ShipType.TowRig, Color.White);

        private readonly EventStatus _eventStatus = new();
        // By making FormStatus static, we can have a shared event status across all instances of ItemReq,
        // which can be useful for tracking global refresh and states that affect all items.
        internal static readonly EventStatus _formStatus = new();

        #region Constructors
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
            //this.FormMouseUp += (e, args) => { };

            // Since the Form was required to have a value, used a dummy form for the empty instance to satisfy the
            // requirement of having a parent form for drawing, without affecting the actual application. Close and
            // Dispose out this dummy form immediately since it is not needed and should not be visible or
            // interactable in any way.
            _parentForm.Close();    // Close the dummy form to ensure it is not visible or interactable.
            _parentForm.Dispose();  // Ensure the dummy form does not consume resources.
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

            _parentForm = parentForm;
            _parentForm.FormClosing += _parentForm_FormClosing;
            _parentForm.Resize += _parentForm_ClientSizeChanged;
            _parentForm.ClientSizeChanged += _parentForm_ClientSizeChanged;
            _parentForm.MouseMove += _parentForm_MouseMove;
            _parentForm.MouseUp += _parentForm_MouseUp;
            _parentForm.MouseDown += _parentForm_MouseDown;
            //_parentForm.Paint += _parentForm_Paint;
            
            _rectangleF = new DRectangleF(RectangleF.Empty, _parentForm.Size);

            this.MouseDown += (e, args) => { };      // initialize the event to prevent validate subscribers .
            this.MouseUp += (e, args) => { };        // initialize the event to prevent validate subscribers .
            this.MouseMove += (e, args) => { };      // initialize the event to prevent validate subscribers .
            //this.FormMouseUp += (e, args) => { };  // initialize the event to prevent validate subscribers .

            this.Name = name;

            _logger = new TextLogger($"{name}_ItemReq", @".\logs")
            {
                EnabledLogLevels = LogLevel.Debug | LogLevel.Error,
                KeepLogDays = TimeSpan.FromDays(1)
            };

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
        /// <summary>
        /// Occurs when the mouse pointer is moved over the form, but not over this ItemReq.
        /// </summary>
        //public event MouseEventHandler FormMouseUp;

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
        public uint DestinationRange { get; set; } = 1024;
        /// <summary>
        /// Gets or sets the destination point for the animation.
        /// </summary>
        public PointF NextDestination { get; set; } = PointF.Empty;
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
        /// Resets only the ships that are currently marked as Dead, 
        /// allowing them to be reused without affecting the status of 
        /// operational ships. This method iterates through all ships 
        /// and calls ResetStats() on those that are dead, restoring 
        /// them to their initial state while leaving alive ships 
        /// unchanged and vunerable.
        /// </summary>
        public static void ResetDeadShips()
        {
            foreach (var ship in _allSpaceShips)
            {
                if (_allSpaceShips[ship.Key].Status == ShipStatus.Dead)
                {
                    _allSpaceShips[ship.Key].ResetStats();
                }
            }
            _spaceShipsInTow.Clear();
        }
        /// <summary>
        /// Sets the type of the spaceship.
        /// </summary>
        /// <param name="stype">The type of the spaceship.</param>
        public void SetShiptType(ShipType stype, Color shipsColor)
        {
            // When setting the ship type, we create a new SpaceShip
            // instance with the specified type and color,
            _spaceShip = new SpaceShip(Name, stype, shipsColor);
            _allSpaceShips.TryAdd(Name, _spaceShip);
            _isSpaceBattle.TrySetTrue();
        }
        /// <summary>
        /// Gets the current spaceship information.
        /// </summary>
        public SpaceShip ShipInfo => _spaceShip;
        /// <summary>
        /// Indicates whether a space battle between ships will occur.<br/>
        /// Default: false
        /// </summary>
        public bool SpaceBattle { get; set; } = false;
        private Color _overlayColor = Color.Transparent;
        // Stores the last engaged target's location and the time of the last attack so a
        // laser flash line can be drawn on the UI thread during the next Paint pass.
        private PointF _lastTargetLocation = PointF.Empty;
        private DateTime _lastCombatTime = DateTime.MinValue;
        // Cached next destination set by the background battle task so the synchronous
        // movement code can act on it in the same frame instead of always wandering.
        private PointF _pendingDestination = PointF.Empty;
        // Tracks the name of the currently locked target so the ship keeps chasing
        // across frames without waiting for a new async scan each time.
        private string _activeTargetName = string.Empty;
        // Throttle the async combat scan so it doesn't spawn a Task every 20ms.
        // Steering toward a locked target still happens every frame (sync, cheap).
        private DateTime _lastScanTime = DateTime.MinValue;
        private static readonly TimeSpan _scanInterval = TimeSpan.FromMilliseconds(150);
        /// <summary>
        /// So we don't have than 1 ship on tow for the same fighter.
        /// </summary>
        internal static ConcurrentDictionary<string, SpaceShip> _spaceShipsInTow = new ConcurrentDictionary<string, SpaceShip>();

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
                
                _allSpaceShips.TryGetValue(this.Name, out SpaceShip? val);

                if (val != null)
                {
                    _spaceShip = val;

                    if (!this.Animation && !val.IsDead && !val.IsTowRig)
                    {
                        this.Animation = true;
                        this.SpaceBattle = true;
                    }
                }

                // Calculate the rectangle for the close button based on form size and padding
                //var frmW = ParentSize.Width - _parentForm.Right;
                RectangleF clsBtnRect = this.Rectangle;
                if (this.Animation || (_spaceShip.IsTowRig && !_spaceShip.IsDead))
                {
                    if (this._dText.Text != this._dText.OrgText)
                        this._dText.Text = this._dText.OrgText;

                    if (this.NextDestination.IsEmpty)
                        this.NextDestination = this.Location;

                    var x = this.Location.X;
                    var y = this.Location.Y;

                    // by using X or Y instead of both, we can create a more dynamic movement
                    // pattern where the item can move in straight lines along the axes,
                    // creating a more varied and less predictable animation effect.

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
                                    var logThis = _spaceShip.Name.Equals("TowRig_000");
                                    var hitBox = (float)_spaceShip.HitBox;
                                    var hitBoxSq = hitBox * hitBox;
                                    var myLoc = this.Center;

                                    if (!string.IsNullOrEmpty(_activeTargetName))
                                    {
                                        if (logThis)
                                            _logger.WriteLine(LogLevel.Debug, $"Has Target: {_activeTargetName}");
                                        // Verify locked target is still alive; steer toward it and deal damage if in range.
                                        if (_allSpaceShips.TryGetValue(_activeTargetName, out var locked) && (_spaceShip.IsTowRig ? locked.IsDead : !locked.IsDead))
                                        {
                                            if (logThis)
                                                _logger.WriteLine(LogLevel.Debug, $"Target Found: {_activeTargetName}, Status: {locked.Status}, Location: {locked.Location}, MyLocation: {_spaceShip.Center}");

                                            // Using distance squared (distSq) for comparison to avoid the overhead of
                                            // calculating the square root when determining proximity to targets. Better
                                            // use of memory and CPU than Math.sqrt() when we only need relative distances
                                            // for comparison against hitBoxSq and closestDist.
                                            float distSq = locked.DistanceFrom(myLoc);

                                            if (distSq <= hitBoxSq)
                                            {
                                                if (logThis)
                                                    _logger.WriteLine(LogLevel.Debug, $"Close enough to response target: {_activeTargetName}, Status: {locked.Status}");

                                                _lastTargetLocation = locked.Location;
                                                _lastCombatTime = DateTime.UtcNow;
                                                if (!_spaceShip.IsTowRig)
                                                {
                                                    if (logThis)
                                                        _logger.WriteLine(LogLevel.Debug, $"#1: Should not be fighting against: {_activeTargetName}, Status: {locked.Status}");

                                                    locked.TakeDamage(_spaceShip.Power, this.Name);
                                                    _allSpaceShips[_activeTargetName] = locked;
                                                }
                                                else
                                                {
                                                    if (logThis)
                                                        _logger.WriteLine(LogLevel.Debug, $"Restting status for: {_activeTargetName}, Status: {locked.Status}");

                                                    _spaceShip.CurrentMission = ShipMission.HeadingHome;
                                                    _allSpaceShips[_activeTargetName].ResetStats();
                                                    _activeTargetName = string.Empty;
                                                    _lastTargetLocation = _pendingDestination;
                                                    _pendingDestination = this.HomeBaseLocation;
                                                    this.NextDestination = this.HomeBaseLocation;
                                                    _spaceShipsInTow.TryRemove(_activeTargetName, out _);
                                                }
                                            }
                                            else if (_spaceShip.IsTowRig)
                                            {
                                                if (logThis)
                                                    _logger.WriteLine(LogLevel.Debug, $"Still cruising towards: {_activeTargetName}, Location: {locked.Location}");

                                                // TowRig can still pull from range, so update
                                                // destination even if not in hit box.
                                                _lastTargetLocation = _pendingDestination;
                                                _pendingDestination = locked.Location;
                                                this.NextDestination = locked.Location;
                                            }
                                            else
                                            {
                                                if (logThis)
                                                    _logger.WriteLine(LogLevel.Debug, $"#2: Should not be here: {_activeTargetName}, Location: {locked.Location}");

                                                // Break lock if target is out of range, so the ship can
                                                // search for a new one instead of chasing a lost cause.
                                                _activeTargetName = string.Empty;
                                            }
                                        }
                                        else
                                        {
                                            if (logThis)
                                                _logger.WriteLine(LogLevel.Debug, $"No target found, clearning name: {_activeTargetName}");
                                            // Target is dead or gone — clear lock.
                                            _activeTargetName = string.Empty;
                                        }
                                    }
                                    else if (_spaceShip.IsTowRig && _spaceShip.CurrentMission == ShipMission.HeadingHome)
                                    {
                                        if (logThis)
                                            _logger.WriteLine(LogLevel.Debug, $"Heading home, MyLocation: {_spaceShip.Location}, NextDestination: {this.NextDestination}");

                                        if (_spaceShip.DistanceFrom(HomeBaseLocation) <= (_spaceShip.HitBox * 2))
                                        {
                                            if (logThis)
                                                _logger.WriteLine(LogLevel.Debug, $"Found home, Location: {_spaceShip.Location}");
                                            this.Animation = false;
                                            _spaceShip.CurrentMission = ShipMission.Idle;
                                            _spaceShip.ResetStats();
                                            _activeTargetName = string.Empty;
                                        }

                                        //_lastTargetLocation = _pendingDestination;
                                        //_pendingDestination = this.HomeBaseLocation;
                                        this.NextDestination = this.HomeBaseLocation;
                                    }
                                    else if (_spaceShip.IsTowRig && _spaceShip.CurrentMission == ShipMission.Idle)
                                    {
                                        List<SpaceShip> allShips = new List<SpaceShip>();
                                        allShips = _allSpaceShips.Where(w =>
                                                                        !w.Value.IsEmpty && w.Value.Name != this.Name &&
                                                                        w.Value.IsDead && !w.Value.IsRaider).Select(s => s.Value).ToList();

                                        if (allShips.Count == 0)
                                        {
                                            if (logThis)
                                                _logger.WriteLine(LogLevel.Debug, $"No dead ships found, Location: {_spaceShip.Location}");

                                            this.Animation = false;
                                            _activeTargetName = string.Empty;
                                            if (this.NextDestination != this.HomeBaseLocation)
                                            {
                                                if (logThis)
                                                    _logger.WriteLine(LogLevel.Debug, $"Resetting next destination.  Current: {this.NextDestination}, Location: {_spaceShip.Location}");

                                                _spaceShip.CurrentMission = ShipMission.Idle;
                                                _lastTargetLocation = _pendingDestination;
                                                _pendingDestination = this.HomeBaseLocation;
                                                this.NextDestination = this.HomeBaseLocation;
                                            }
                                        }
                                        else
                                        {
                                            foreach (var kvp in allShips)
                                            {
                                                if (_spaceShipsInTow.TryAdd(kvp.Name, _spaceShip))
                                                {
                                                    if (logThis)
                                                        _logger.WriteLine(LogLevel.Debug, $"Found dead ship. {kvp.Name}, Location: {kvp.Location}, Center: {kvp.Center}");

                                                    float distSq = _allSpaceShips[kvp.Name].DistanceFrom(myLoc);
                                                    _allSpaceShips[kvp.Name].SetTower(_spaceShip.Name, distSq);
                                                    this.NextDestination = kvp.Center;

                                                    this.Animation = true;
                                                    _spaceShip.CurrentMission = ShipMission.OnTow;
                                                    _activeTargetName = kvp.Name;

                                                    Debug.WriteLine($"'{Name}' heading to towing '{kvp.Name}' @ {distSq} meters away");

                                                    _lastTargetLocation = _pendingDestination;
                                                    _pendingDestination = kvp.Center;

                                                    _lastCombatTime = DateTime.UtcNow;
                                                }
                                            }

                                            if(_spaceShip.CurrentMission != ShipMission.OnTow && _spaceShip.CurrentMission != ShipMission.HeadingHome)
                                            {
                                                if (logThis)
                                                    _logger.WriteLine(LogLevel.Debug, $"Setting missiong to idle. Location: {_spaceShip.Location}, Center: {_spaceShip.Center}");

                                                _spaceShip.CurrentMission = ShipMission.Idle;
                                                _lastTargetLocation = _pendingDestination;
                                                _pendingDestination = this.HomeBaseLocation;
                                                this.NextDestination = this.HomeBaseLocation;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (logThis)
                                            _logger.WriteLine(LogLevel.Debug, $"I should be here..");

                                        // No locked target — scan for any enemy in detection range.
                                        SpaceShip? closest = null;
                                        List<SpaceShip> allShips = new List<SpaceShip>();
                                        double closestDist = double.MaxValue;

                                        allShips = _allSpaceShips.Where(w => 
                                                                        !w.Value.IsEmpty && w.Value.Name != this.Name && 
                                                                        !w.Value.IsDead && w.Value.Location.X != 0 &&
                                                                        w.Value.Location.Y != 0).Select(s=>s.Value).ToList();

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
                                                    //var dx = kvp.Location.X - myLoc.X;
                                                    //var dy = kvp.Location.Y - myLoc.Y;
                                                    // Using distance squared (distSq) for comparison to avoid the overhead of
                                                    // calculating the square root when determining proximity to targets. Better
                                                    // use of memory and CPU than Math.sqrt() when we only need relative distances
                                                    // for comparison against hitBoxSq and closestDist.
                                                    float distSq = kvp.DistanceFrom(myLoc);// (dx * dx) + (dy * dy);
                                                    if (distSq <= hitBoxSq && distSq < closestDist)
                                                    {
                                                        closestDist = distSq;
                                                        closest = kvp;
                                                    }
                                                }
                                            }
                                        }

                                        if (closest != null)
                                        {
                                            _activeTargetName = closest.Name;
                                            _pendingDestination = closest.Location;
                                            _lastTargetLocation = closest.Location;
                                            _lastCombatTime = DateTime.UtcNow;

                                            // closest.TakeDamage(_spaceShip.Power, Name);
                                            // _allSpaceShips[closest.Name] = closest;
                                            _allSpaceShips[closest.Name].TakeDamage(_spaceShip.Power, Name);
                                        }
                                    }

                                    // keep it up to date for other threads, even if it didn't change here,
                                    // so the latest status is always visible to the UI and other tasks.
                                    _allSpaceShips[_spaceShip.Name] = _spaceShip;

                                    if (_spaceShip.IsDead)
                                    {
                                        this.Animation = false;
                                        this.SpaceBattle = false;
                                        this._dText.Text = $"{this._dText.DeadDisplay}";
                                    }
                                }
                                catch(Exception ex)
                                {
                                    _logger.WriteLine(LogLevel.Error, $"Error in combat scan for '{Name}': {ex}");
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

                    // Consume any pending destination from the combat scan above every frame,
                    // so steering toward an enemy overrides the current path immediately.
                    if (!_pendingDestination.IsEmpty && !_spaceShip.IsTowRig)
                    {
                        // we don't want _spaceShip.IsTowRig random walking around, they should only move toward targets and home base.

                        var lX = Math.Min(_pendingDestination.X, this.ShipInfo.Location.X);
                        var hX = Math.Min(_pendingDestination.X, this.ShipInfo.Location.X);
                        var lY = Math.Min(_pendingDestination.Y, this.ShipInfo.Location.Y);
                        var hY = Math.Min(_pendingDestination.Y, this.ShipInfo.Location.Y);

                        // this way theyare not on top of each other constantly.
                        var rX = Random.Shared.Next((int)lX, (int)hX) + (lX - (int)lX);
                        var rY = Random.Shared.Next((int)lY, (int)hY) + (lY - (int)lY);

                        this.NextDestination = new PointF(
                            Math.Clamp(rX, 0, ParentSize.Width - this.ShipInfo.Width),
                            Math.Clamp(rY, 0, ParentSize.Height - this.Height));
                        _pendingDestination = PointF.Empty;
                    }
                    else if ((this.Location.X == this.NextDestination.X ||
                             this.Location.Y == this.NextDestination.Y) && !_spaceShip.IsTowRig) 
                    {
                        // we don't want _spaceShip.IsTowRig random walking around, they should only move toward targets and home base.

                        // Reached the current waypoint with no new target — pick a random one.
                        //if (string.IsNullOrEmpty(_activeTargetName))
                        {
                            x += Random.Shared.Next(-(int)this.DestinationRange, (int)this.DestinationRange + 1);
                            y += Random.Shared.Next(-(int)this.DestinationRange, (int)this.DestinationRange + 1);

                            this.NextDestination = new PointF(
                                Math.Clamp(x, 0, ParentSize.Width - this.Width),
                                Math.Clamp(y, 0, ParentSize.Height - this.Height)
                            );
                        }
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
                        _spaceShip.Location = this.Center;
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
                    // If the button is in the pressed state, we can use the shadow rectangle for drawing to create a "pressed" effect.
                    clsBtnRect = new RectangleF(clsBtnShdwRect.Location, clsBtnShdwRect.Size);
                    // Adjust line to be based on the shadow rectangle's position to keep the lines centered within the pressed Item.
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
                    if (this._dText.HasShadowing)
                    {
                        clsBtnShdwRect = new RectangleF(clsBtnRect.X + (int)this.ShadowDepth, clsBtnRect.Y + (int)this.ShadowDepth, clsBtnRect.Width, clsBtnRect.Height);
                        g.DrawString(this._dText.Text, this._dText.Font, this._dText.ForeColorShadow.Brush, clsBtnShdwRect, _centerText);
                    }

                    if (_isSpaceBattle)
                        g.DrawString(this._dText.Text, this._dText.Font, _spaceShip.ShipsColorBrush, clsBtnRect, _centerText);
                    else
                        g.DrawString(this._dText.Text, this._dText.Font, this._dText.ForeColor.Brush, clsBtnRect, _centerText);

                    //*
                    if (_isSpaceBattle && _spaceShip.Status != ShipStatus.Dead)
                    {
                        // Draw the detection radius as a circle correctly centered on the ship
                        // and sized to match ShipInfo.HitBox (the actual combat range).
                        var hbR = (float)ShipInfo.HitBox;
                        //var dmg = (float)ShipInfo.DamageLevel;
                        var shipCx = clsBtnRect.X + clsBtnRect.Width / 2;
                        var shipCy = clsBtnRect.Y + clsBtnRect.Height / 2;

                        var hbRect = new RectangleF(shipCx - hbR, shipCy - hbR, hbR * 2, hbR * 2);
                        g.DrawEllipse(_hitboxCircle, hbRect);

                        // If the ship has recently fired (within the last 300ms), draw a laser line toward the last target
                        // location to visually indicate an attack, creating a dynamic combat effect that shows the direction
                        // of fire and adds visual feedback to the battle interactions.
                        if (_showFire.TrySetTrue())
                        {
                            // Draw a brief laser flash line toward the last engaged target.
                            if (!_lastTargetLocation.IsEmpty &&
                                (DateTime.UtcNow - _lastCombatTime).TotalMilliseconds < 300)
                            {
                                var pen = _spaceShip.IsTowRig ? DDefaults.DEF_TOW_LASER_LINE : DDefaults.DEF_LASER_LINE;
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
                    /**/
                }

                // Border of button
                g.DrawRectangle(this.GetBorder, clsBtnRect);
            }
            catch (Exception ex)
            {
                // Handle any exceptions that may occur during drawing, such as issues with graphics context or invalid parameters.
                _logger.WriteLine(LogLevel.Error, ex.Message);
                return false;
            }

            return true;
        }
        private ABool _showFire = ABool.False;
        public bool IsBetween(float location, float low, float high)
        {
            return location >= low && location <= high;
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

                // First, check if the mouse position is within the bounds of the parent form to avoid unnecessary calculations.
                if (mousePos.X < 0 || mousePos.Y < 0 || mousePos.X > ParentSize.Width || mousePos.Y > ParentSize.Height)
                    return false;

                // If the rectangle is empty or not visible, we can immediately
                // return false without performing any calculations.
                if (this.IsEmpty || !Visible)
                    return false;

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
                    {
                        _eventStatus.Set($"{Name}_MouseOver", true);
                        //~this.Refresh();
                    }
                }
                // if mouse out was in Item area and is currently not, then remove
                // mouseover event and invalidate to redraw.
                else if (_eventStatus.Get($"{Name}_MouseOver"))
                {
                    _eventStatus.Set($"{Name}_MouseOver", false);
                    // this.Refresh();
                }

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
                // Handle any exceptions that may occur during the hit test, such as issues with rectangle calculations or invalid parameters.
                _logger.WriteLine(LogLevel.Error, ex.Message);
                return false;
            }
        }
        /// <summary>
        /// Determines whether the mouse pointer is within the rectangle, optionally including the shadow and expanding
        /// the hit area.
        /// </summary>
        /// <param name="mouseX">The X-coordinate of the mouse pointer.</param>
        /// <param name="mouseY">The Y-coordinate of the mouse pointer.</param>
        /// <returns>true if the mouse pointer is within the specified area; otherwise, false.</returns>
        public bool IsMouseInRect(uint mouseX, uint mouseY) => 
                    this.IsMouseInRect(new PointF((int)mouseX, (int)mouseY));
        #endregion

        /// <summary>
        /// 
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
            set { _rectangleF.Right = value; }
        }
        public float Top
        {
            get { return _rectangleF.Top; }
            set { _rectangleF.Top = value; }
        }
        public float Bottom
        {
            get { return _rectangleF.Bottom; }
            set { _rectangleF.Bottom = value; }
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

        /**
        #region Rectangle, Location and Size properties with synchronization
        /// <summary>
        /// Represents the rectangle area with an default origin at (0, 0) and a size of 100x100. This property<br/>
        /// will set and keep in sync with the Location and Size properties. Any updates to any of the<br/>
        /// properties (Location, Size, Left, Top, Right, Bottom, Width, Height) will keep this rectangle in<br/>
        /// sync and update accordingly.<br/>
        /// </summary>
        /// <remarks>
        ///    Any attempt to set the rectangle with negative coordinates or passed bounds of the form will be ignored to maintain valid drawing parameters.
        /// </remarks>
        public RectangleF Rectangle
        {
            get { return _rectangle; }
            set
            {
                (bool loc, bool sz) updated = (false, false);

                if ((value.Location.X >= 0 && value.Location.Y >= 0) &&
                     value.Location.X < _parentForm.Width &&
                     value.Location.Y < _parentForm.Height)
                {
                    this.Location = value.Location;
                    updated.loc = true;
                }

                if ((value.Size.Width >= 0 && value.Size.Height >= 0) 
                    && ((value.Location.X + value.Size.Width)  < ParentSize.Width) 
                    && ((value.Location.Y + value.Size.Height) < ParentSize.Height)) 
                { 
                    this.Size = value.Size; 
                    updated.sz = true;
                }
            }
        }
        /// <summary>
        /// Gets or sets the coordinates of the upper-left corner of the rectangle.<br/>
        /// Rectangle property will also be updated when this property is set.<br/>
        /// </summary>
        /// <remarks>
        /// Any attempt to set the rectangle with negative coordinates or passed bounds of the form will be ignored to maintain valid drawing parameters.
        /// </remarks>
        public PointF Location
        {
            get { return this.Rectangle.Location; }
            set 
            {
                if (value.X >= 0 && value.Y >= 0 && value.X < ParentSize.Width && value.Y < ParentSize.Height)
                {
                    _rectangle.Location = value;
                }
            }
        }
        /// <summary>
        /// Gets the center point of the overall rectangle, calculated based on the current 
        /// Location and Size properties. This is a read-only property that provides a convenient 
        /// way to access the center coordinates of the rectangle for drawing or hit testing 
        /// purposes. The center point is calculated as:<br/>
        /// (Location.X + Size.Width / 2, Location.Y + Size.Height / 2) and is updated whenever 
        /// the Location or Size properties are changed to ensure it always reflects the current 
        /// state of the rectangle.
        /// </summary>
        public PointF LocationCenter
        {
            get 
            {
                if (_locationCenter.X != this.Location.X || _locationCenter.Y != this.Location.Y)
                    _locationCenter = new PointF(this.Location.X + (this.Size.Width / 2), this.Location.Y + (this.Size.Height / 2));

                return _locationCenter; 
            }
        }
        /// <summary>
        /// Gets or sets the size of the rectangle.<br/>
        /// Rectangle property will also be updated when this property is set.<br/>
        /// </summary>
        /// <remarks>
        /// Any attempt to set the rectangle with negative coordinates or passed bounds of the form will be ignored to maintain valid drawing parameters.
        /// </remarks>
        public SizeF Size
        {
            get { return this.Rectangle.Size; }
            set 
            { 
                if (value.Width >= 0 && value.Height >= 0 && (this.Left + value.Width) < ParentSize.Width && (this.Top + value.Height) < ParentSize.Height) 
                { 
                    _rectangle.Size = value;
                    // if the size is too small, we can consider it as not visible to avoid
                    // drawing issues or clutter. Enabling will be up to the user.
                    if (value.Height < 10 || value.Width < 10)  
                        this.Visible = false;
                }
            }
        }
        #endregion

        #region Convenience properties for edges with synchronization
        /// <summary>
        /// Gets or sets the x-coordinate of the left edge of the object.<br/>
        /// Location property will also be updated when this property is set.<br/>
        /// </summary>
        /// <remarks>
        /// Any attempt to set the rectangle with negative coordinates or passed bounds of the form will be ignored to maintain valid drawing parameters.
        /// </remarks>
        public float Left
        {
            get { return this.Location.X; }
            set { if (value >= 0 && value < ParentSize.Width) this.Location = new PointF(value, this.Top); }
        }
        /// <summary>
        /// Gets or sets the y-coordinate of the upper-left corner of the object.<br/>
        /// Location property will also be updated when this property is set.<br/>
        /// </summary>
        /// <remarks>
        /// Any attempt to set the rectangle with negative coordinates or passed bounds of the form will be ignored to maintain valid drawing parameters.
        /// </remarks>
        public float Top
        {
            get { return this.Location.Y; }
            set { if (value >= 0 && value < ParentSize.Height) this.Location = new PointF(this.Left, value); }
        }
        /// <summary>
        /// Gets or sets the x-coordinate of the right edge of the rectangle.<br/>
        /// Size property will also be updated when this property is set.<br/>
        /// </summary>
        /// <remarks>
        /// Any attempt to set the rectangle with negative coordinates or passed bounds of the form will be ignored to maintain valid drawing parameters.
        /// </remarks>
        public float Right
        {
            get { return this.Left + this.Width; }
            set { if (value >= this.Left && value < ParentSize.Width) this.Size = new SizeF(value - this.Left, this.Height); }
        }
        /// <summary>
        /// Gets or sets the y-coordinate of the bottom edge of the rectangle.<br/>
        /// Size property will also be updated when this property is set.<br/>
        /// </summary>
        /// <remarks>
        /// Any attempt to set the rectangle with negative coordinates or passed bounds of the form will be ignored to maintain valid drawing parameters.
        /// </remarks>
        public float Bottom
        {
            get { return this.Top + this.Height; }
            set { if (value >= this.Top && value < ParentSize.Height) this.Size = new SizeF(this.Width, value - this.Top); }
        }
        #endregion

        #region Convenience properties for dimensions with synchronization
        /// <summary>
        /// Gets or sets the width component of the size.<br/>
        /// Size property will also be updated when this property is set.<br/>
        /// </summary>
        /// <remarks>
        /// Any attempt to set the rectangle with negative coordinates or passed bounds of the form will be ignored to maintain valid drawing parameters.
        /// </remarks>
        public float Width
        {
            get { return this.Size.Width; }
            set { if (value >= 0 && (this.Left + value) < ParentSize.Width) this.Size = new SizeF(value, this.Height); }
        }
        /// <summary>
        /// Gets or sets the height component of the size.<br/>
        /// Size property will also be updated when this property is set.<br/>
        /// </summary>
        /// <remarks>
        /// Any attempt to set the rectangle with negative coordinates or passed bounds of the form will be ignored to maintain valid drawing parameters.
        /// </remarks>
        public float Height
        {
            get { return this.Size.Height; }
            set { if (value >= 0 && (this.Top + value) < ParentSize.Height) this.Size = new SizeF(this.Width, value); }
        }
        #endregion
        /**/

        #region Public Methods
        /// <summary>
        /// Invalidates the parent form, causing a repaint if a 
        /// paint event is not already in progress. Since this uses a 
        /// shared EventStatus to track the paint event, it ensures 
        /// that we do not trigger multiple invalidations while a paint 
        /// is already in progress, which can help prevent flickering 
        /// and improve performance.
        /// </summary>
        public void Refresh()
        {
            // only 1 invalidation should be triggered while a paint event is
            // in progress to prevent excessive redraws and potential flickering.
            //if (!_formStatus.Set("Form_Paint", true))
            //    return;

            //try
            //{
            //    _parentForm.Invalidate();
            //}
            //finally
            //{
            //    _formStatus.Set("Form_Paint", false);
            //}
        }
        #endregion


        #region Event handlers for parent form events to trigger redraws
        /// <summary>
        /// Handles the Paint event for the parent form, updating the visual state based on mouse interaction.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">A PaintEventArgs that contains the event data.</param>
        private void _parentForm_Paint(object? sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            if (_eventStatus.Get($"{Name}_MouseOver") || !this.InActiveHide)
                DrawItem(g);
            else
                _eventStatus.Set($"{Name}_MouseDown", false);
        }
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
                    //--this.Refresh();
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
                //--this.Refresh();
            }

            // If the mouse is released while still within the Item area and the left mouse button
            // was used, we should reset the button state and close the run expected function after
            // a short delay to allow the user to see the the release effect.
            if (_eventStatus.Get($"{Name}_MouseInRectangle"))
                this.MouseUp?.Invoke(this, e);
        }
        /// <summary>
        /// FUTURE: If any thread/timers are still referencing this specific class ItemReq, clean them up.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _parentForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            // If the parent form is closing, stop any ongoing animations or timers
            // related to this item to prevent them from trying to access the form
            // after it has been closed, which could lead to exceptions or memory leaks.
            this.Animation = false;
        }
        /// <summary>
        /// Handles changes to the parent form's client size.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void _parentForm_ClientSizeChanged(object? sender, EventArgs e)
        {
            // If the parent form's client size changes, we may need to adjust the
            // rectangle or other properties.
        }
        #endregion

        #region Helper methods and properties
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
        private Size ParentSize { get { return _parentForm.ClientSize; } }
        /// <summary>
        /// Gets the padding within the parent form.
        /// </summary>
        private Padding Padding { get { return _parentForm.Padding; } }
        #endregion
    }
}
