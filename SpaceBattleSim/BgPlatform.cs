using Chizl.Applications;
using Chizl.Configurations;
using Chizl.ThreadSupport;
using SpaceBattleSim.shapes;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Text;
using SpaceBattleSim.Models.Events;
using static SpaceBattleSim.StaticConfig;

namespace SpaceBattleSim
{
    public partial class BgPlatform : Form
    {
        private BufferedGraphicsContext _bufferedContext = BufferedGraphicsManager.Current;
        private BufferedGraphics? _bufferedGraphics;

        // Set to true to make the background transparent
        // while keeping the grid lines visible. 
        static bool _transparentBG = false;
        static ABool _startup = ABool.True;
        const string _appTitle = "Space Battleground Simulation";
        private static string _appInfo = "Version: {0} - F1 (Help)";
        private static ABool _loopStarted = ABool.False;
        private static readonly string _helpInfo = "---===[ F-Keys Support ]===---\n " +
            "Esc - Pause/Unpause screen\n " +
            "F1  - This Help Message\n " +
            "F2  - Ship's class type information\n " +
            "F3  - Battle Stats\n " +
            "F5  - Revive the dead and refresh all ships to 100%\n " +
            "F12 - Open Explorer to root of Sim\n " +
            "Mouse over the far top right for make close button.\n\t" +
            "* Only available when not in Windowed mode\n " +
            "Mouse over far left top and click banner that pops up for toggle background transparent.\n\t" +
            "* May be hard to toggle back, because the background is click through.";
        const string _appTitleAbout = "chizl.com";
        const string _formClosing = "Form_Closed";
        const float _percentRepairRigs = 0.10f;              // set percentage of total ships that are repair rigs, rest will be Fighters and Raiders.
        const float _percentCapitalShips = 0.10f;            // set percentage of total ships that are capital ships, rest will be Fighters and Raiders.

        readonly static (int min, int max) _totalBattleShipsLimits = (10, 150);         // set limits for total number of Fighters and Raiders combined.
        readonly static (int min, int max) _fpsRateLimits = (10, 30);                   // set limits for frames per second.
        // refresh rate chart.
        //      (10 or 100  = 10fps or 100ms)
        //      (20 or 50   = 20fps or 50ms)
        //      (30 or 33   = 30fps or 33ms)
        //      (60 or 16   = 60fps or 16ms)
        private readonly static int[] _refreshRateValidValues = { 33, 50, 100 };    // interval for timer in milliseconds.
        private readonly static int[] _fpsRateValidValues = { 30, 20, 10 };         // FPS in same order of _refreshRateValidValues to bind fps to closes interval.
        private readonly static (int min, int max) _planetSizeLimits = (50, 400);               // set limits for planet size.
        private readonly static (float min, float max) _planetSpinSpeedLimits = (0.0f, 0.5f);   // set limits for planet spin speed.
        private static string _planetTextureFile = ".\\skins\\fungal_planet.png"; // pulled from app config, path to the planet texture image file.
        private Bitmap _planetTexture = new Bitmap(_planetTextureFile); // Load your map here
        // pulled from app config, Total number of Fighters and Raiders combined.
        private SimScreenView _screenViewType = SimScreenView.FullScreenCurrent;
        private bool _showMatrixGrid = false;       // pulled from app config.
        private bool _showPlanets = true;           // pulled from app config
        private bool _showNebulae = true;           // pulled from app config
        private bool _showStars = true;             // pulled from app config
        private bool _showComet = true;             // pulled from app config
        private bool _showVersion = true;           // pulled from app config
        private bool _useShadowing = false;         // pulled from app config, set to true to enable shadow effects on controls for enhanced visual depth. 
        private int _totalBattleShips = 100;        // pulled from app config, Total number of Fighters and Raiders combined.
        private bool _criticalTransferRaiders = false; // pulled from app config
        private bool _criticalTransferAlly = false; // pulled from app config
        private int _planetSize = 150;              // pulled from app config
        private float _planetSpinSpeed = 0.1f;      // pulled from app config
        private bool _naturalStarfield = true;      // pulled from app config
        private bool _disableAutoLock = false;      // pulled from app config
        private int _fpsRate = 30;                  // pulled from app config, frames per second for the form's refresh rate. This is used to calculate the _refreshRate in milliseconds. A common choice is 30fps for smooth animations without excessive CPU usage.
        private int _refreshRate = 33;              // pulled from app config, this is the refresh rate for the form in milliseconds.
                                                    // A lower value will result in smoother animations but higher CPU usage,
                                                    // while a higher value will reduce CPU usage but may result in choppier animations.
                                                    // 33ms is approximately 30 frames per second, which is a common refresh rate for smooth animations.
        private bool _topmostWindow = false;        // pulled from app config
        private bool _auditLogEnabled = false;      // pulled from app config
        private bool _useUnicodeShips = true;       // pulled from app config

        // set to true to pause the screen, which will stop all animations and interactions.
        // Useful for closely examining the current state of the battle or for taking screenshots
        // without any movement.
        private ABool _pauseScreen = ABool.False;

        // const bool _showAsteroids = false;
        private ABool _showCloseButton = ABool.True;// If form is Windowed, we don't need a close button, but if it's full screen, we do.
        private int _capShipCount = 0;              // calculated later, _totalBattleShips / 10, do not modify this here.
        private int _repairRigCount = 0;            // calculated later, _totalBattleShips / 10, do not modify this here.
        private int _planetWrapWidth = 0;           // calculated later, _planetSize * 2, do not modify this here.
        private float _xOffset = 0;
        Rectangle _redPlanetRect = Rectangle.Empty;

        // All for Raiders reset logic based on 50% mark.
        private double _orgTotalRaiders = 0;
        private double _totalRaiders = 0;
        private double _aliveRaiders = 0;
        private double _diffRaiders = 0;
        private double _aliveAlly = 0;

        // Default border width for controls
        const uint _borderWidth = 2;
        // Size of the matrix grid (50pxx50px)
        const int _matrixCellSize = 40;

        // Moves X position of HomeBase and _capitalShip anchor points if changed.
        private static float _moveX = 0.0f;          // Center Screen: 200.0f, Far Left: -680.0f, Far Right: 1080.0f
        // Moves Y position of HomeBase and _capitalShip anchor points if changed.
        private static float _moveY = 0.0f;          // Center Screen: 0.0f, Far Top: -455.0f, Far Bottom: 450.0f.
        // _capitalShip Lines: - Base X anchor point of lines to the large _battleShips moving items.
        private static float _anchorX = 758.0f;
        // _capitalShip Lines: - Base Y anchor point of lines to the large _battleShips moving items.
        private static float _anchorY = 540.0f;
        // Tuple to hold the shadow color and depth for consistent styling across controls.
        readonly (Color color, uint depth) _shadowStyle = (Color.FromArgb(64, Color.White), 5);
        // Fonts for different controls, using Arial as a common font for simplicity. Adjust sizes and styles as needed.
        readonly Font _xSmallFlierFont = new Font("Arial", 8, FontStyle.Regular);
        readonly Font _smallFlierFont = new Font("Arial", 12, FontStyle.Regular);
        readonly Font _largeFlierFont = new Font("Arial", 16, FontStyle.Regular);
        readonly Font _closeBtnFont = new Font("Arial", 22, FontStyle.Regular);
        readonly Font _titleFont = new Font("Arial", 14, FontStyle.Bold);
        readonly Font _statsFont = new Font("Courier New", 12, FontStyle.Regular);
        // Colors for different ship types to provide visual distinction between them.
        readonly Color _homeBaseLinkRepairRigColor = Color.FromArgb(32, 255, 255, 255);
        // Thread-Safe - EventStatus object to track whether the form has already been
        // closed, preventing multiple closure attempts.
        readonly EventStatus _eventStatus = new EventStatus();
        // ItemReq objects for the various controls to paint on the form. These are initialized in the BuildObjects method.
        private static ItemReq CloseButton = ItemReq.Empty;
        private static ItemReq MatrixArray = ItemReq.Empty;
        private static ItemReq TitleText = ItemReq.Empty;
        private static ItemReq HomeBase = ItemReq.Empty;
        private static DShapes _cometShapes = new DShapes();
        // Add this field alongside _spaceShapes / _cometShapes
        private static Bitmap? _spaceCache = null;
        private static DRectangleF _formBounds = DRectangleF.Default;
        private static float _xCounter = 0.0f;
        private static float _yCounter = 0.0f;
        private static Point _lastStartPoint = Point.Empty;
        private static PointF _versionLoc = PointF.Empty;
        private static PointF _percResetLoc = PointF.Empty;
        private static List<float[]> _baseCords = new();
        private static PointF _baseCenter = PointF.Empty;
        private readonly Pen _planetBorderPen = new Pen(Color.FromArgb(32, Color.Black), 10);

        private CancellationTokenSource _loopTokenSource;
        internal static List<ItemReq> _battleShips = new List<ItemReq>();
        private static string[] _fKeyDisplay = { };

        public BgPlatform()
        {
            InitializeComponent();

            // Enable double buffering and custom painting
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.Opaque, true);
            this.UpdateStyles();

            // Set the form's padding based on config.
            this.Padding = FormStyle.Padding;

            // Load background display settings from app config.
            LoadConfigurations();

            // Set the form closed event status to false initially. This will be used to
            // track whether the form has already been closed, preventing multiple closure
            // attempts if the close button is clicked multiple times.
            _eventStatus.Set(_formClosing, false);

            // Subscribe to the ShipStatusChanged event from the ItemReq class. This allows
            // the form to respond to changes in ship status, such as updating the display
            // or triggering animations when a ship's status changes (e.g., from alive to destroyed).
            ItemReq.ShipStatusChanged += ItemReq_ShipStatusChanged;
            // Start the object creation process with a short
            // delay to ensure the form is fully initialized.
            BuildObjects(100);

            //set the app info text with the current file version for display in the bottom-left
            //corner of the form. This provides users with version information about the application,
            //which can be useful for troubleshooting or ensuring they are using the latest version.
            _appInfo = string.Format(_appInfo, About.FileVersion);

            // Initialize the cancellation token source for the animation loop. This will allow us to
            // gracefully stop the animation loop when the form is closed, preventing any potential
            // issues with background threads trying to access disposed resources.
            _loopTokenSource = new CancellationTokenSource();

            // Attach a MouseMove event handler to the form to check for mouse interactions with
            // the controls. This allows for dynamic interaction with the controls, such as changing
            // the cursor when hovering over interactive elements or triggering animations when the
            // mouse moves over certain areas.
            this.MouseMove += (s, e) =>
            {
                // Direct call to all ships to check their hitboxes
                //foreach (var ship in _battleShips) 
                //    ship.IsMouseInRect(e.Location);
                if (CloseButton.IsMouseInRect(e.Location))
                    CloseButton.Visible = true;
                else
                    CloseButton.Visible = false;

                if (TitleText.IsMouseInRect(e.Location))
                    TitleText.Visible = true;
                else
                    TitleText.Visible = false;

                // since the screen is paused, I stil need to allow the user to interact with the close button
                // and title text to unpause or close the form, and to prevent flicker for those controls specifically, I 
                // set critical transfer to allow interaction even when paused. This property flag is unused as
                // a button, so I'm using it for pausing.  If not paused, I reset critical transfer to false so
                // they will hide again when not hovered over.
                if (_pauseScreen && ((CloseButton.Visible && !CloseButton.CriticalTransfer) || (TitleText.Visible && !TitleText.CriticalTransfer)))
                {
                    if (CloseButton.Visible)
                    {
                        CloseButton.CriticalTransfer = true;
                        this.Invalidate(new Region(CloseButton.Rectangle));
                    }
                    else if (TitleText.Visible)
                    {
                        TitleText.CriticalTransfer = true;
                        this.Invalidate(new Region(TitleText.Rectangle));
                    }

                }
                else if (!_pauseScreen)
                {
                    if (CloseButton.CriticalTransfer)
                        CloseButton.CriticalTransfer = false;
                    if (TitleText.CriticalTransfer)
                        TitleText.CriticalTransfer = false;
                }
            };

            this.KeyDown += (s, e) =>
            {
                var isEsc = e.KeyCode == Keys.Escape;   //pause/unpause screen, which will stop all animations and interactions,
                                                        //allowing for closely examining the current state of the battle or for
                                                        //taking screenshots without any movement.
                var isF1 = e.KeyCode == Keys.F1;        //summary and help display
                var isF2 = e.KeyCode == Keys.F2;        //summary with ship status
                var isF3 = e.KeyCode == Keys.F3;        //summary with battle stats
                var isF5 = e.KeyCode == Keys.F5;        //revive and refresh ships
                var isF12 = e.KeyCode == Keys.F12;      //open explorer to root of sim folder

                if (isEsc)
                    _pauseScreen.TrySetValue(!_pauseScreen);
                else if (isF1)
                    _fKeyDisplay = this.HelpDisplayText();
                else if (isF2)
                    _fKeyDisplay = ItemReq.GetShipStatus(true);
                else if (isF3)
                    _fKeyDisplay = ItemReq.GetShipStatus(false);
                else if (isF5)
                {
                    _pauseScreen.TrySetTrue();
                    StopLoop();

                    ItemReq.ResetAllShips();
                }
                else if (isF12)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = About.AppRootDir,
                        UseShellExecute = true,
                        Verb = "open"
                    });
                }
            };

            this.KeyUp += (s, e) =>
            {
                var isF1 = e.KeyCode == Keys.F1;
                var isF2 = e.KeyCode == Keys.F2;
                var isF3 = e.KeyCode == Keys.F3;
                var isF5 = e.KeyCode == Keys.F5;
                if (isF1 || isF2 || isF3)
                    _fKeyDisplay = new string[] { };
                else if (isF5)
                {
                    StartLoop();
                    _pauseScreen.TrySetFalse();
                }
            };

            _startup.SetFalse();
        }

        /// <summary>
        /// One time method to create all the necessary ItemReq objects for the form. 
        /// This is called in the constructor after a short delay to ensure the form 
        /// is fully initialized. The method uses Invoke to ensure that control creation 
        /// happens on the UI thread. It checks if each ItemReq (like CloseButton) is 
        /// empty before creating it, allowing for potential reuse or conditional creation 
        /// in the future.
        /// </summary>
        private async void BuildObjects(int startDelay)
        {
            if (Directory.Exists(".\\logs"))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(".\\logs"))
                        File.Delete(file);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deleting logs directory: {ex.Message}");
                }
            }

            this.DoubleBuffered = true;

            // Delay the creations to ensure the form is fully initialized
            await Task.Delay(startDelay).ContinueWith(_ =>
            {
                // first object created so it appears behind the others
                BuildMatrixArray();

                // =================================================================
                // Build other controls on top of the matrix background, but in the order of which layer they should appear.
                BuildHomeBase();

                //Build out stars, planets, and other space objects in the background before building the HomeBase
                //and _battleShips to create a sense of depth and immersion in the space battle scene. By drawing
                //these elements first, they will appear behind the HomeBase and _battleShips, enhancing the visual
                //complexity and making the overall scene more engaging. You can use simple shapes like circles for
                //stars and planets, or even use images for more detailed backgrounds. Consider adding subtle animations
                //to these background elements (e.g., twinkling stars or slowly rotating planets) to further enhance
                //the dynamic feel of the scene.
                BuildSpaceTime();

                // Fliers are built after the HomeBase so RapairRigs will appear around it. 
                BuildFliers();

                // =================================================================
                // last object created so it appears on top of the others
                BuildTitleText();
                BuildCloseButton();

                // location for version text to be displayed.
                _versionLoc = new PointF(Padding.Left + 10, this.FormSize.Height - Padding.Bottom - 25);
                _percResetLoc = new PointF(Padding.Left, this.FormSize.Height - Padding.Bottom - 10);

                // If transparency background, you can create a fully transparent background while still allowing the
                // grid lines to be visible. Adjusting the Alpha value allows you to control the transparency level of
                // the background without affecting the visibility of the grid lines or other controls. This can be
                // useful for creating an overlay effect where only the grid lines are visible on top of other windows
                // or applications. NOTE: This does make it click-through, so you won't be able to interact with
                // anything except solid controls like the close button.
                if (_transparentBG)
                    this.Invoke(new Action(() => { this.TransparencyKey = this.BackColor; }));
            });

            if (_topmostWindow)
                this.TopMost = true;
            else
                this.TopMost = false;
        }

        #region Build Paint Objects
        /// <summary>
        /// Builds the static space background — star field, nebulae, and a comet —
        /// into <c>_spaceShapes</c> once so it can be replayed every paint frame.
        /// </summary>
        private void BuildSpaceTime()
        {
            var center = new Point(this.ViewSize.Width / 2, this.ViewSize.Height / 2);
            var bounds = new RectangleF(this.Padding.Left, this.Padding.Top,
                                        this.ViewSize.Width, this.ViewSize.Height);

            if (_showPlanets)
            {
                this.Invoke(new Action(() => {
                    int xOffset = Math.Clamp(_planetSize, 25, 100) * 2;
                    int yOffset = Math.Clamp(_planetSize, 25, 100);

                    // Correct location if the planet would be off the screen based on the configured offsets.
                    if (center.X + xOffset + _planetSize > bounds.Width)
                        xOffset = center.X - (((int)bounds.Width - _planetSize) + (this.Padding.Bottom * 2));

                    if (center.Y + yOffset + _planetSize > bounds.Height)
                        yOffset = center.Y - (((int)bounds.Height - _planetSize) + (this.Padding.Left * 2));

                    // Red Planet Setup, placed here so it is behind the HomeBase and
                    // _battleShips but in front of the static starfield and nebulae
                    // background to create a sense of depth. The planet will also
                    // have a simple left-to-right scrolling animation to add some
                    // dynamic movement to the scene.
                    _redPlanetRect = new Rectangle(center.X + xOffset, center.Y + yOffset, _planetSize, _planetSize);
                }));
            }

            if (_spaceCache == null)
            {
                this.Invoke(new Action(() => {
                    if (_showStars || _showNebulae)
                    {
                        // Bake the static background (stars + nebulae) into a bitmap once.
                        // Alpha accumulates properly on a Bitmap, so nebulae build up density
                        // without needing a huge density value.
                        _spaceCache = new Bitmap(this.ViewSize.Width, this.ViewSize.Height,
                                                 System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                        using var bg = Graphics.FromImage(_spaceCache);
                        bg.SmoothingMode = SmoothingMode.AntiAlias;
                        bg.Clear(Color.Transparent);

                        // Collect into a temporary DShapes then draw them all onto the bitmap
                        var tmp = new DShapes();
                        var rng = Random.Shared;

                        if (_showStars)
                        {
                            // --- Star field: two passes for depth ---
                            SpaceBackground.AddStarField(tmp, bounds, 350, rng, _naturalStarfield);
                            var innerBounds = new RectangleF(bounds.X + 60, bounds.Y + 60,
                                                              bounds.Width - 120, bounds.Height - 120);
                            SpaceBackground.AddStarField(tmp, innerBounds, 80, rng, _naturalStarfield);
                        }

                        if (_showNebulae)
                        {
                            // --- Nebulae ---
                            // Purple/blue — upper-left quadrant (density 600–1000 is plenty on a bitmap)
                            SpaceBackground.AddNebula(tmp,
                                new PointF(bounds.Width * 0.22f, bounds.Height * 0.28f),
                                radius: 140, Color.FromArgb(60, 60, 0, 180), density: 11200, rng);

                            // Red/orange — lower-right quadrant
                            SpaceBackground.AddNebula(tmp,
                                new PointF(bounds.Width * 0.75f, bounds.Height * 0.68f),
                                radius: 110, Color.FromArgb(60, 180, 40, 0), density: 8800, rng);

                            // Faint teal — upper-right
                            SpaceBackground.AddNebula(tmp,
                                new PointF(bounds.Width * 0.80f, bounds.Height * 0.20f),
                                radius: 80, Color.FromArgb(50, 0, 140, 130), density: 6400, rng);
                        }

                        using (GraphicsPath path = new GraphicsPath())
                        {
                            List<PointF> newPoints = new List<PointF>();
                            for (int i = 0; i < _baseCords.Count; i++)
                            {
                                using GraphicsPath path2 = new GraphicsPath();
                                var lst = _baseCords[i];

                                for (int j = 0; j < lst.Length; j += 2)
                                    newPoints.Add(new PointF(lst[j] + _moveX, lst[j + 1] + _moveY));

                                path2.AddPolygon(newPoints.ToArray());
                                path.AddPath(path2, true);

                                using (SolidBrush brush = new SolidBrush(Color.FromArgb(150, Color.Blue)))
                                    bg.FillPath(brush, path);

                                newPoints.Clear();
                                path.Reset();
                            }
                        }

                        foreach (var (start, end) in HomeBase.DLine.DrawList)
                            bg.DrawLine(DDefaults.DEF_LINE_SETUP, start, end);

                        // Draw all collected segments onto the bitmap
                        foreach (var (start, end, pen) in tmp.DrawList)
                            bg.DrawLine(pen, start, end);
                    }
                }));
            }

            // Comet stays in _cometShapes (it animates each frame)
            if (_cometShapes.DrawList.Count == 0)
            {
                this.Invoke(new Action(() => {
                    var bounds = new RectangleF(this.Padding.Left, this.Padding.Top,
                                                this.ViewSize.Width, this.ViewSize.Height);

                    SpaceBackground.AddComet(_cometShapes,
                        head: new PointF(bounds.Width * 0.15f, bounds.Height * 0.18f),
                        direction: new PointF(1f, 0.45f), tailLength: 160f, rays: 10);
                }));
            }
        }
        /// <summary>
        /// Initializes the MatrixArray control as a grid of ItemReq objects
        /// representing the matrix background if it is empty.
        /// </summary>
        /// <remarks>
        ///    Ensures thread safety by invoking on the UI thread when 
        ///    creating and modifying UI controls.
        /// </remarks>
        private void BuildMatrixArray()
        {
            // Use Invoke to ensure we're on the UI thread when creating controls
            if (!MatrixArray.IsEmpty || !_showMatrixGrid)
                return;

            // Create a grid of ItemReq objects for the matrix background
            int cols = this.ViewSize.Width / _matrixCellSize;
            int rows = this.ViewSize.Height / _matrixCellSize;

            MatrixArray = new ItemReq(this, $"MatrixArray")
            {
                Location = new PointF(this.Padding.Left, this.Padding.Top),
                Size = new Size(this.ViewSize.Width, this.ViewSize.Height),
                BGColor = Color.Transparent, //Color.FromArgb(255, this.BackColor),
                BorderColor = _shadowStyle.color,
                BorderWidth = _borderWidth,
                ShadowDepth = _shadowStyle.depth,
                BoxShadowing = true,
                Visible = true,
                DLine =
                {
                    // used for the lines in the matrix grid.
                    Pen = new Pen(Color.FromArgb(64, Color.White), 1),
                },
            };

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    MatrixArray.DLine.ItemType = ItemType.Custom;
                    // Add a line for each cell in the grid to create the matrix effect. The +1px creates a dot at the
                    // intersection of the grid lines. No way do we want to create a circle or square for each cell in the
                    // grid, so we use a custom line with a single pixel length to create the dot effect at the grid
                    // intersections.
                    var left = this.Padding.Left + col * _matrixCellSize;
                    var top = this.Padding.Top + row * _matrixCellSize;
                    MatrixArray.DLine.Add(new PointF(left, top), new PointF(left + 1, top + 1));
                }
            }
        }
        /// <summary>
        /// Initializes and displays a close button in the top-right corner of the view
        /// if it does not already exist.
        /// </summary>
        /// <remarks>
        ///    Configures the close button's appearance, position, and event handlers 
        ///    to provide visual feedback and close the form when clicked.
        /// </remarks>
        private void BuildCloseButton()
        {
            if (!CloseButton.IsEmpty || !_showCloseButton)
                return;

            // Size of the close button
            var closeSize = new Size(30, 30);
            // Set to true to use lines instead of text for the "X" button
            var useLines = true;
            // Border width for the close button
            var factorButtonSize = (closeSize.Width + this.Padding.Right + _shadowStyle.depth + MatrixArray.BorderWidth);

            // Calculate the X/Y Location of the close button that will be in the
            // top-right corner of the screen. Accounting for top and right padding along
            // with the MatrixArray border width to ensure it doesn't overlap with the
            // matrix grid lines
            var x = (int)(this.FormSize.Width - factorButtonSize);
            var y = (int)(this.Padding.Top + MatrixArray.BorderWidth);

            this.Invoke(new Action(() =>
            {
                var bgColor = Color.FromArgb(64, Color.Red);
                CloseButton = new ItemReq(this, "CloseButton")
                {
                    Location = new PointF(x, y),
                    Size = closeSize,
                    HitBox = 30,
                    BGColor = bgColor,
                    MouseOverColor = Color.FromArgb(255, Color.Red),
                    BorderWidth = _borderWidth,
                    BorderColor = Color.White,
                    BoxShadowing = true,
                    ShadowColor = _shadowStyle.color,
                    ShadowDepth = _shadowStyle.depth,
                    InActiveHide = true,
                    AnimateClick = true,
                    Visible = true
                };

                CloseButton.MouseMove += (e, args) =>
                {
                    // Change the cursor to a hand when hovering over the close button for better UX
                    if (CloseButton.IsMouseInRect(args.Location))
                        this.Cursor = Cursors.Hand;
                    else if (this.Cursor == Cursors.Hand)
                        this.Cursor = Cursors.Cross;
                };

                CloseButton.MouseUp += (e, args) =>
                {
                    // Only respond to left mouse button clicks to prevent accidental
                    // form closure from right-clicks or other mouse buttons.
                    if (args.Button != MouseButtons.Left)
                        return;

                    // Set the form closed event status to true. This ensures that if the
                    // user clicks the close button multiple times, the form will only
                    // attempt to close once.
                    if (_eventStatus.Set(_formClosing, true))
                    {
                        CloseButton.BGColor = bgColor;
                        // wait 1/2 second to allow user to see the button release effect
                        // before closing the form
                        Task.Delay(500).ContinueWith(_ =>
                        {
                            // Use Invoke to ensure we're on the UI thread when closing the form
                            this.Invoke(new Action(() => { this.Close(); }));
                        });
                    }
                };

                CloseButton.MouseDown += (e, args) =>
                {
                    // Only respond to left mouse button clicks to prevent accidental
                    // form closure from right-clicks or other mouse buttons.
                    if (args.Button != MouseButtons.Left)
                        return;

                    // Change the background color on mouse down for visual feedback
                    CloseButton.BGColor = Color.FromArgb(128, Color.BurlyWood);
                    _ = Task.Delay(1000).ContinueWith(_ =>
                    {
                        // Change the background color back to the original color after 1 second
                        CloseButton.BGColor = bgColor;
                    });
                };

                if (useLines)
                {
                    // Button text: "X" in the middle of the button
                    CloseButton.DLine.Pen = new Pen(Color.White, 2);
                    CloseButton.DLine.Add(new PointF(CloseButton.Left, CloseButton.Top), new PointF(CloseButton.Right, CloseButton.Bottom));
                    CloseButton.DLine.Add(new PointF(CloseButton.Left, CloseButton.Bottom), new PointF(CloseButton.Right, CloseButton.Top));
                }
                else
                {
                    CloseButton.DText.Text = "X";
                    CloseButton.DText.DFont = _closeBtnFont;
                    CloseButton.DText.TextColor = Color.White;
                }
            }));
        }
        /// <summary>
        /// Initializes and adds animated close button items
        /// to the _battleShips collection if it is empty.
        /// </summary>
        /// <remarks>
        ///    Each item is positioned randomly near the center of the form and can display 
        ///    either an 'X' character or intersecting lines, depending on configuration.
        /// </remarks>
        private void BuildFliers()
        {
            if (_battleShips.Count > 0)
                return;

            // Size of the flier button
            var flierSize = new Size(20, 20);
            var capSize = new Size(50, 50);
            var repairSize = new Size(20, 20);

            // Calculate the X/Y Location of the close button that will be in the top-right corner
            // of the screen. Accounting for top and right padding along with the MatrixArray border
            // width to ensure it doesn't overlap with the matrix grid lines
            var w = this.ViewSize.Width;
            var h = this.ViewSize.Height;

            this.Invoke(new Action(() =>
            {
                var raid = new ShipStats(ShipType.Raider);
                var fight = new ShipStats(ShipType.Fighter);

                int x, y;
                var fighterCnt = _totalBattleShips / 3;
                _totalRaiders = _totalBattleShips - fighterCnt;
                _orgTotalRaiders = _totalRaiders;
                _diffRaiders = 100.0f;

                // Create x amount of  animated "X" items that will fling out
                // from the center of the form when triggered.
                for (int cnt = 0; cnt < _totalBattleShips; cnt++)
                {
                    // Randomly position the _battleShips items within the bounds of the form
                    x = Random.Shared.Next(0, w + 1);
                    y = Random.Shared.Next(0, h + 1);

                    // Creating 1/3 of the total ships, the fighter, then building raider ship styles based on the count, with fighters appearing
                    // first. This creates visual variety among the flier items, making the scene more dynamic and interesting.
                    // The critical transfer settings are also applied based on the ship type to add an extra layer of interaction
                    // and strategy to the animation.
                    var shipImg = cnt < fighterCnt ? fight.ShipView : raid.ShipView;
                    var shipType = cnt < fighterCnt ? ShipType.Fighter : ShipType.Raider;
                    var shipColor = cnt < fighterCnt ? fight.ShipColor : raid.ShipColor;
                    var criticalTransfer = ((cnt >= fighterCnt && _criticalTransferRaiders) || (cnt < fighterCnt && _criticalTransferAlly));
                    var partName = $"{shipType}";

                    var fly = new ItemReq(this, $"{partName}_{cnt:000}")
                    {
                        Location = new PointF(x, y),
                        Size = flierSize,
                        ShadowDepth = !_useShadowing ? 0 : _shadowStyle.depth,
                        DText = {
                            DFont = _smallFlierFont,
                            Text = shipImg,
                            ShadowDepth = !_useShadowing?0:_shadowStyle.depth,
                            ShadowColor = !_useShadowing?Color.Empty:Color.FromArgb(32, shipColor),
                        },
                        DestinationRange = (uint)this.Width / 2,
                        CriticalTransfer = criticalTransfer,
                        Animation = true,
                        Visible = true
                    };

                    fly.SetShiptType(shipType);
                    _battleShips.Add(fly);
                }

                var cap = new ShipStats(ShipType.Capital);

                // Create x amount of  animated "X" items that will fling out
                // from the center of the form when triggered.
                for (int cnt = 0; cnt < _capShipCount; cnt++)
                {
                    x = Random.Shared.Next(0, w + 1);
                    y = Random.Shared.Next(0, h + 1);

                    var fly = new ItemReq(this, $"Capital_{cnt:000}")
                    {
                        Location = new PointF(x, y),
                        Size = capSize,
                        ShadowDepth = _useShadowing ? 0 : _shadowStyle.depth,
                        DText = {
                            DFont = _largeFlierFont,
                            Text = cap.ShipView,
                            ShadowDepth = !_useShadowing?0:_shadowStyle.depth,
                            ShadowColor = !_useShadowing?Color.Empty:Color.FromArgb(64, cap.ShipColor),
                        },
                        DestinationRange = (uint)this.Width / 2,
                        CriticalTransfer = _criticalTransferAlly,
                        Animation = true,
                        Visible = true
                    };

                    // When anchoring lines and using Animation, the start of the line is
                    // the anchor location, while the end is dynamic following the ItemRec.
                    fly.SetShiptType(ShipType.Capital);
                    _battleShips.Add(fly);
                }

                var repairRig = new ShipStats(ShipType.RepairRig);
                // Create x amount of  animated "X" items that will fling out
                // from the center of the form when triggered.
                for (int cnt = 0; cnt < _repairRigCount; cnt++)
                {
                    x = Random.Shared.Next((int)(_baseCenter.X - 100.0f), (int)(_baseCenter.X + 100.0f));
                    y = Random.Shared.Next((int)(_baseCenter.Y - 100.0f), (int)(_baseCenter.Y + 100.0f));

                    var fly = new ItemReq(this, $"RepairRig_{cnt:000}")
                    {
                        Location = new PointF(x, y),
                        Size = repairSize,
                        ShadowDepth = _useShadowing ? 0 : _shadowStyle.depth,
                        DText = {
                            DFont = _smallFlierFont,
                            Text = repairRig.ShipView,
                            ShadowDepth = !_useShadowing?0:_shadowStyle.depth,
                            ShadowColor = !_useShadowing?Color.Empty:Color.FromArgb(64, repairRig.ShipColor),
                        },
                        DestinationRange = (uint)this.Width / 2,
                        CriticalTransfer = _criticalTransferAlly,
                        Animation = false,
                        DLine =
                        {
                            // used for the lines in the matrix grid.
                            Pen = new Pen(_homeBaseLinkRepairRigColor, 2),
                            // Set HasAnchor to true to indicate that the line should be anchored
                            // to a specific point (the center of the form in this case).
                            HasAnchor = true,
                        },
                        Visible = true
                    };

                    // When anchoring lines and using Animation, the start of the line is
                    // the anchor location, while the end is dynamic following the ItemRec.
                    //fly.DLine.Add(new PointF(_anchorX + _moveX, _anchorY + _moveY), new PointF(fly.Right, fly.Bottom));
                    fly.DLine.Add(_baseCenter, new PointF(fly.Right, fly.Bottom));
                    fly.SetShiptType(ShipType.RepairRig);
                    _battleShips.Add(fly);
                }
            }));
        }
        private void CreateShipCords()
        {
            var raiderMotherShip = DDefaults.GetRaidersMotherShip();
            var firstCords = raiderMotherShip[0];
            var tLeft = CreateCoords(firstCords, 0.0f, 0.0f);
            var tRight = CreateCoords(firstCords, 74.0f, 0.0f);
            var bLeft = CreateCoords(firstCords, 0.0f, 33.0f);
            var bRight = CreateCoords(firstCords, 74.0f, 33.0f);
            var topRight = ShapeTransformer.FlipShape(tRight.ToList(), true, false);
            var bottomLeft = ShapeTransformer.FlipShape(bLeft.ToList(), false, true);
            var bottomRight = ShapeTransformer.FlipShape(bRight.ToList(), true, true);

            var sb = new StringBuilder();

            Debug.WriteLine("Top Left: ");
            foreach (PointF cord in tLeft)
            {
                if (sb.Length > 0)
                    sb.Append(", ");
                sb.Append($"{cord.X}, {cord.Y}");
            }
            Debug.WriteLine(sb.ToString());
            sb.Clear();

            Debug.WriteLine("Top Right: ");
            foreach (PointF cord in topRight)
            {
                if (sb.Length > 0)
                    sb.Append(", ");
                sb.Append($"{cord.X}, {cord.Y}");
            }
            Debug.WriteLine(sb.ToString());
            sb.Clear();

            Debug.WriteLine("Bottom Left: ");
            foreach (PointF cord in bottomLeft)
            {
                if (sb.Length > 0)
                    sb.Append(", ");
                sb.Append($"{cord.X}, {cord.Y}");
            }
            Debug.WriteLine(sb.ToString());
            sb.Clear();

            Debug.WriteLine("Bottom Rigth: ");
            foreach (PointF cord in bottomRight)
            {
                if (sb.Length > 0)
                    sb.Append(", ");
                sb.Append($"{cord.X}, {cord.Y}");
            }
            Debug.WriteLine(sb.ToString());
            sb.Clear();
        }
        /// <summary>
        /// Initializes the HomeBase item with predefined
        /// coordinates and visual styles if it is empty.
        /// </summary>
        private void BuildHomeBase()
        {
            if (!HomeBase.IsEmpty)
                return;

            this.Invoke(new Action(() =>
            {
                HomeBase = new ItemReq(this, "HomeBase")
                {
                    DLine =
                    {
                        // used for the lines in the matrix grid.
                        Pen = new Pen(Color.FromArgb(128, Color.White), 2),
                        // if LineShadowPen is commented, it wil not have a shadow on the lines.
                        ShadowPen = new Pen(_shadowStyle.color, 2),
                    },
                };
                // Create Ship Testing..
                //CreateShipCords();

                //_baseCords = DDefaults.GetRaidersMotherShip();
                _baseCords = DDefaults.GetHomeBase();

                // Move HomeBase based on window sizes and if multiple monitors are involved or not.
                var anchorX = _formBounds.Center.X;
                var anchorY = _formBounds.Center.Y;
                // Calculate the movement needed to position the HomeBase at the center of the form, accounting
                // for the original anchor point and any existing movement offsets. This ensures that the HomeBase
                // will be correctly centered on the form regardless of its initial position or any previous movements.
                _moveX += anchorX > _anchorX ? anchorX - _anchorX : anchorX - _anchorX;
                _moveY += anchorY - _anchorY;
                // update original anchors with the new movement values so that the HomeBase will be drawn in the
                // correct location on the first paint and the lines will be anchored to the correct location as well.

                // Set the base center point to the new anchor location after applying movement adjustments.
                // This ensures that the HomeBase will be positioned correctly on the form, and all lines anchored
                // to this point will also be drawn in the correct location.
                _baseCenter = new PointF(_anchorX+ _moveX, _anchorY+ _moveY);
                // don't really use this, but setting the HomeBase.Location to the base center point for clarity and
                // potential future use. The HomeBase is primarily drawn using the _baseCords and movement offsets,
                // so the Location property is not critical in this implementation, but it can be useful for reference
                // or if additional features are added later that rely on the HomeBase's location.
                HomeBase.Location = _baseCenter;

                // Draw lines from center point for all corners of the inner and outer HomeBase shape
                foreach (var cords in _baseCords)
                {
                    // Create the points for the lines based on the
                    // coordinates and optional movement offsets
                    var points = CreateCoords(cords, _moveX, _moveY);
                    // Add lines between each point and the next,
                    // wrapping around to the first point at the end to
                    // create a closed shape
                    for (int i = 0; i < points.Length; i++)
                    {
                        // Add lines between each point and the next, wrapping around to the first point at the end
                        HomeBase.DLine.Add(points[i], points[(i == points.Length - 1 ? 0 : i + 1)]);
                    }
                }
            }));
        }
        /// <summary>
        /// Initializes the title text control if it has not been created.
        /// </summary>
        /// <remarks>
        ///    Invokes the creation of a styled text item on the UI thread 
        ///    with predefined appearance and layout settings.
        /// </remarks>
        private void BuildTitleText()
        {
            if (!TitleText.IsEmpty)
                return;

            this.Invoke(new Action(() =>
            {
                TitleText = new ItemReq(this, "TitleText")
                {
                    Location = new PointF(Padding.Left, Padding.Top),
                    Size = new Size(200, 60),
                    BGColor = Color.FromArgb(128, Color.Blue),
                    MouseOverColor = Color.FromArgb(128, Color.Blue),
                    HitBox = 30,
                    DText = {
                            Text = _appTitle,
                            TextColor = Color.White,
                            ShadowDepth = _shadowStyle.depth,
                            ShadowColor = Color.FromArgb(64, Color.White),
                            DFont = _titleFont,
                        },
                    BorderWidth = _borderWidth,
                    BorderColor = Color.White,
                    BoxShadowing = true,
                    ShadowColor = _shadowStyle.color,
                    ShadowDepth = _shadowStyle.depth,
                    InActiveHide = true,
                    Visible = true,
                    AnimateClick = true,
                    DestinationRange = (uint)Math.Min(ViewSize.Width / 2, ViewSize.Height / 2),
                };

                TitleText.MouseMove += (sender, e) =>
                {
                    if (!TitleText.DText.Text.Equals(_appTitleAbout))
                    {
                        TitleText.DText.Text = _appTitleAbout;
                        Task.Delay(5000).ContinueWith(_ =>
                        {
                            this.Invoke(new Action(() =>
                            {
                                TitleText.DText.Text = _appTitle;
                            }));
                        });
                    }
                };

                TitleText.MouseMove += (e, args) =>
                {
                    if (TitleText.IsMouseInRect(args.Location))
                        this.Cursor = Cursors.Hand;
                    else if (this.Cursor == Cursors.Hand)
                        this.Cursor = Cursors.Cross;
                };

                TitleText.MouseDown += (sender, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        _transparentBG = !_transparentBG;

                        if (_transparentBG)
                            this.Invoke(new Action(() => { this.TransparencyKey = this.BackColor; }));
                        else
                            this.Invoke(new Action(() => { this.TransparencyKey = Color.Empty; }));
                    }
                };
            }));
        }
        /// <summary>
        /// Creates an array of Point structures representing the corners of a box, optionally offset by
        /// specified values.
        /// </summary>
        /// <param name="coordinates">An array of eight integers representing the x and y coordinates of the four corners of the box.</param>
        /// <param name="moveX">The horizontal offset to apply to each x-coordinate.</param>
        /// <param name="moveY">The vertical offset to apply to each y-coordinate.</param>
        /// <returns>An array of Point structures representing the adjusted coordinates.</returns>
        /// <exception cref="ArgumentException">Thrown when the coordinates array does not contain exactly eight elements.</exception>
        private PointF[] CreateCoords(float[] coordinates, float moveX = 0, float moveY = 0)
        {
            if (coordinates.Length > 3 && (coordinates.Length % 4) != 0)
                throw new ArgumentException("Coordinates array must contain exactly 8 integers representing the corners of the box.");

            var retVal = new List<PointF>();
            for (int i = 0; i < coordinates.Length; i += 2)
                retVal.Add(new PointF(coordinates[i] + moveX, coordinates[i + 1] + moveY));

            return retVal.ToArray();
        }
        private string[] HelpDisplayText()
        {
            var retVal = new List<string>();

            return _helpInfo.Split('\n'); // retVal.ToArray();
        }
        #endregion

        #region Configuration
        /// <summary>
        /// Loads and applies configuration settings for display and animation options.
        /// </summary>
        /// <remarks>This method updates internal configuration values related to visual features such as
        /// planet display, nebulae, stars, and comet visibility. It also recalculates the planet texture wrap width to
        /// ensure seamless horizontal scrolling of the planet's surface texture. Call this method after changing
        /// configuration fields to apply the latest settings.</remarks>
        private void LoadConfigurations()
        {
            // System configuration settings
            SetConfigValue("ScreenViewType", ref _screenViewType);
            SetConfigValue("ShowVersion", ref _showVersion);
            SetConfigValue("DisableAutoLock", ref _disableAutoLock);
            SetConfigValue("TopmostWindow", ref _topmostWindow);
            SetConfigValue("AuditLogEnabled", ref _auditLogEnabled);
            SetConfigValue("UseUnicodeShips", ref _useUnicodeShips);
            if (_useUnicodeShips)
                SetConfigValue("UseShadowing", ref _useShadowing);
            SetConfigValue("RefreshRate", ref _fpsRate, _fpsRateLimits.min, _fpsRateLimits.max);

            // FPS was used, make it only refresh rate for timer.
            // This allows users to specify their desired frame rate directly, and the application will
            // adjust the refresh rate accordingly to achieve that frame rate. If the provided value is
            // not valid, it defaults to 30fps (the second value in the _fpsRateValidValues array).
            // Also: Adjust ship speed based on the refresh rate to maintain consistent animation speeds across
            // different refresh rates. This ensures that the animation will not appear too fast or too
            // slow regardless of the configured refresh rate, providing a smoother and more visually
            // appealing experience for the user.  Some of these may make it jumpy, but they will maintain
            // the same overall speed for the ships, just with more or less frames per second depending
            // on the refresh rate.

            switch (_fpsRate)
            {
                case 30:
                    _refreshRate = _refreshRateValidValues[0];
                    ShipStats.RefreshRateText = "High (30fps)";
                    // no adjustment needed for 30fps, as it is the default frame rate for the animation. This ensures
                    // that the ships will move at their intended speed without any modifications, providing a smooth
                    // and visually appealing experience for users who prefer the default refresh rate.
                    ShipStats.AdjSpeed = .5f;
                    break;
                case 10:
                    _refreshRate = _refreshRateValidValues[2];
                    ShipStats.RefreshRateText = "Low (10fps)";
                    // slow down the ships more significantly to compensate for the much lower frame rate, preventing them
                    // from moving too fast and creating a smoother animation.
                    ShipStats.AdjSpeed = 3.0f;
                    break;
                case 20:
                default:
                    // if value doesn't match, default to 20fps, 50ms refresh rate.
                    _refreshRate = _refreshRateValidValues[1];
                    ShipStats.RefreshRateText = "Medium (20fps)";
                    // speed up the ships slightly to compensate for the lower frame rate, preventing them
                    // from moving too slow and creating a smoother animation.
                    ShipStats.AdjSpeed = 1.5f;// 1.0f;
                    break;
            }

            // Battle and ship configuration settings
            SetConfigValue("TotalBattleShips", ref _totalBattleShips, _totalBattleShipsLimits.min, _totalBattleShipsLimits.max);
            SetConfigValue("CriticalTransferRaiders", ref _criticalTransferRaiders);
            SetConfigValue("CriticalTransferAlly", ref _criticalTransferAlly);

            // Background and visual configuration settings
            SetConfigValue("ShowMatrixGrid", ref _showMatrixGrid);
            SetConfigValue("ShowComet", ref _showComet);
            SetConfigValue("ShowNebulae", ref _showNebulae);
            SetConfigValue("ShowPlanets", ref _showPlanets);
            SetConfigValue("ShowStars", ref _showStars);

            // only show the planet configuration options if planets are enabled, since they
            // have no effect without planets and would just take up space in the config
            if (_showPlanets)
            {
                // setup default, then load the planet skin from the config, and if it's valid, load
                // the texture for the planet. If the config value is invalid, disable planets to
                // prevent errors and resource issues from trying to load an invalid texture.
                var planetSkin = _planetTextureFile;
                // pull from config
                SetConfigValue("PlanetTextureFile", ref planetSkin);
                planetSkin = planetSkin.ToLower().Trim();

                // verification steps for the planet skin:
                if (!string.IsNullOrEmpty(planetSkin) &&
                    (planetSkin.EndsWith(".png") ||
                        planetSkin.EndsWith(".jpg") ||
                        planetSkin.EndsWith(".bmp")) &&
                        planetSkin.StartsWith(".\\skins\\") &&
                        File.Exists(planetSkin))
                {
                    // if the planet skin has changed from the currently loaded
                    // texture, dispose of the old texture and load the new one
                    if (planetSkin != _planetTextureFile.ToLower())
                    {
                        _planetTexture.Dispose();   // Dispose of the old texture to free up resources before loading the new one
                        _planetTexture = new Bitmap(planetSkin); // Load your map here
                        _planetTextureFile = planetSkin;
                    }
                    // Get planet size to display
                    SetConfigValue("PlanetSize", ref _planetSize, _planetSizeLimits.min, _planetSizeLimits.max);
                    // Get rotation speed for the planet to determine how fast the texture scrolls horizontally.
                    // The faster the speed, the faster the texture will scroll, creating the illusion of a
                    // rotating planet. Clamping the value ensures that it stays within reasonable limits to
                    // maintain a visually appealing animation without causing performance issues or making the
                    // rotation too fast to appreciate.
                    SetConfigValue("PlanetSpinSpeed", ref _planetSpinSpeed, _planetSpinSpeedLimits.min, _planetSpinSpeedLimits.max);
                }
                else
                    _showPlanets = false;
            }

            // only show the natural starfield option if stars are enabled, since it
            // has no effect without stars and would just take up space in the config
            if (_showStars)
                SetConfigValue("NaturalStarfield", ref _naturalStarfield);

            // default is false, but doesn't hurt to set it explicitly based on the config value.
            // This ensures that the BattleStats.Enabled property is always in sync with the
            // configuration setting, allowing for consistent behavior of the audit logging
            // feature throughout the application. By setting this property here, we can control
            // whether battle statistics are collected and logged based on the user's configuration
            // preferences.
            BattleStats.Enabled = _auditLogEnabled;
            ItemReq.UnicodeShips = _useUnicodeShips;

            /*
            // Interactive configuration settings (not implemented yet)
            SetConfigValue("MouseOverShips", ref _mouseOverShips);
			<!-- 
			Allows the user to mouse over ships to see their stats. (NOT IMPLEMENTED YET)
			-->
			<add key="MouseOverShips" value="false"/>
            /**/

            var thisScreen = Screen.AllScreens.Where(w => w.Bounds.Contains(this.Left, this.Top)).FirstOrDefault() ?? Screen.PrimaryScreen;

            if (_screenViewType == SimScreenView.Windowed)
            {
                // In Windowed mode, we disable the painted close button because we
                // don't need both the painted and standard windowed close buttons.
                _showCloseButton.SetFalse();

                var appHeight = 1002;  // 1002 = 1080
                                       //        - 48 (Toolbar height)
                                       //        - 30 Titlebar height (approx, varies based on font and DPI)

                // Since I'm disabling the maximize box, disabling resize, and setting a fixed size, I want to ensure that the form is large enough
                // to show the full animation without being too large for smaller screens. Setting the height to 1002 allows
                // for a good balance between visibility and compatibility with various screen sizes, while also accounting
                // for the space taken up by the toolbar and title bar.

                // Users can still minimize and close via window controls, but not resize or maximize. Animation contained within a consistent window
                // size and prevents layout issues that could arise from resizing.
                this.MaximizeBox = false;

                this.Location = thisScreen != null ? thisScreen.WorkingArea.Location : new Point(0, 0); // Assuming toolbar on bottom.
                this.Size = thisScreen != null ? new Size(thisScreen.WorkingArea.Width, appHeight) : new Size(1920, appHeight);

                this.StartPosition = FormStartPosition.CenterScreen;
                this.FormBorderStyle = FormBorderStyle.Fixed3D;
                this.WindowState = FormWindowState.Normal;
            }
            else if (_screenViewType == SimScreenView.FullScreenAll)
            {
                this.FormBorderStyle = FormBorderStyle.None;
                this.WindowState = FormWindowState.Normal;

                // If a user has a monitor to the left of the primary, its Left will be
                // negative (e.g., -1920). SystemInformation.VirtualScreen already accounts
                // for this in its X/Y. With the Union approach, Left will also be negative.
                ///var virtualBounds = Screen.AllScreens
                ///    .Select(s => s.Bounds)
                ///    .Aggregate(Rectangle.Union);
                ///this.Location = new Point(virtualBounds.Left, virtualBounds.Top);
                ///this.Size = new Size(virtualBounds.Width, virtualBounds.Height);

                // SystemInformation.VirtualScreen wins — just want to cover all screens with
                // no filtering, and it handles all edge cases including negative coordinates
                // automatically. Save Rectangle.Union for when you need per-screen control.
                var virtualScreen = SystemInformation.VirtualScreen;
                this.Location = new Point(virtualScreen.Left, virtualScreen.Top);
                this.Size = new Size(virtualScreen.Width, virtualScreen.Height);
            }
            else  // default to FullScreenCurrent
            {
                this.Location = thisScreen != null ? thisScreen.Bounds.Location : new Point(0, 0);
                this.Size = thisScreen != null ? thisScreen.Bounds.Size : new Size(1920, 1080);
                this.FormBorderStyle = FormBorderStyle.None;
                this.WindowState = FormWindowState.Maximized;
            }

            _formBounds = new DRectangleF(this.Padding.Left, this.Padding.Top,
                                        this.ViewSize.Width, this.ViewSize.Height, this.ViewSize);

            // If auto-lock is disabled in the configuration, call the method to prevent the
            // system from automatically locking the screen. This is important for ensuring
            // that the animation can run uninterrupted without the screen locking due to
            // inactivity. By preventing auto-lock, users can enjoy the visual experience
            // without having to interact with the system to keep it active.
            if (_disableAutoLock)
                Externs.PreventAutoLock();

            // Create count for Capital ships based on percentage of total battleships
            _capShipCount = (int)((float)_totalBattleShips * _percentCapitalShips);
            // Create count for RepairRig ships based on percentage of total battleships
            _repairRigCount = (int)((float)_totalBattleShips * _percentRepairRigs);

            // Calculate the wrap width for the planet texture based on the planet size. The wrap width
            // is set to twice the planet size to allow for seamless horizontal scrolling of the texture
            // across the planet's surface. This ensures that as the texture scrolls, it will repeat
            // without visible seams, creating a continuous animation effect on the planet.
            _planetWrapWidth = _planetSize * 2;

            // Must be done after the form size has been determined. This is because the buffered graphics
            // context needs to know the size of the area it will be drawing to in order to allocate the
            // appropriate amount of memory for the back buffer. Allocating the buffered graphics context
            // before the form size is set could lead to issues where the back buffer is too small or too
            // large, resulting in rendering problems or inefficient memory usage. By initializing the
            // buffered graphics context after setting the form size, we ensure that it is properly configured
            // to handle the drawing operations for our animation.
            this.Load += (s, e) =>
            {
                _bufferedGraphics = _bufferedContext.Allocate(
                    this.CreateGraphics(),
                    this.ClientRectangle
                );

                StartLoop();
            };
        }

        /// <summary>
        /// Retrieves a configuration value associated with the specified key, or sets the configuration to the provided
        /// value if the key does not exist.
        /// </summary>
        /// <remarks>If the configuration value for the specified key does not exist, the method sets it
        /// to the provided default value. The method updates the referenced value only if a configuration entry is
        /// found.</remarks>
        /// <typeparam name="T">The type of the configuration value to retrieve or set.</typeparam>
        /// <param name="key">The key identifying the configuration value.</param>
        /// <param name="retval">When this method returns, contains the configuration value associated with the specified key if found;
        /// otherwise, contains the value that was set.</param>
        private void SetConfigValue<T>(string key, ref T retval)
        {
            if (AppConfig.GetConfigValue<T>(key, out T? value) && value != null)
                retval = value;
            else
                AppConfig.SetConfigValue(key, $"{retval}");

            // audit only.
            BattleStats.AddSetting(key, $"{retval}");
        }
        /// <summary>
        /// Retrieves a configuration value for the specified key and assigns it to the provided variable, ensuring the
        /// value is within the specified minimum and maximum bounds. If the configuration value does not exist, writes
        /// the current value to the configuration.
        /// </summary>
        /// <remarks>This method ensures that configuration values remain within a valid range. If the
        /// configuration does not contain a value for the specified key, the current value of <paramref name="retval"/>
        /// is persisted as the default.</remarks>
        /// <typeparam name="T">The type of the configuration value. Must implement <see cref="IComparable{T}"/> to allow range checking.</typeparam>
        /// <param name="key">The key identifying the configuration value to retrieve or set.</param>
        /// <param name="retval">A reference to the variable that receives the configuration value. If the value is not found, the current
        /// value is written to the configuration.</param>
        /// <param name="min">The minimum allowed value for the configuration setting. If the retrieved value is less than this, the
        /// minimum is used.</param>
        /// <param name="max">The maximum allowed value for the configuration setting. If the retrieved value is greater than this, the
        /// maximum is used.</param>
        private void SetConfigValue<T>(string key, ref T retval, T min, T max) where T : IComparable<T>
        {
            SetConfigValue<T>(key, ref retval);             // default is passed in, so this will write the default if the key is not found.
            if (retval.CompareTo(min) < 0) retval = min;    // if the value is less than the minimum, set it to the minimum
            if (retval.CompareTo(max) > 0) retval = max;    // if the value is greater than the maximum, set it to the maximum
        }
        #endregion

        #region Form Events
        private void StartLoop()
        {
            if (!_loopStarted.TrySetTrue())
                return;

            //_loopTokenSource.Dispose();
            _loopTokenSource = new CancellationTokenSource();
            // Run the loop on a background thread pool thread
            Task.Run(async () => await GameLoopAsync(_loopTokenSource.Token));
        }
        private void StopLoop()
        {
            _loopStarted.SetFalse();
            _loopTokenSource.Cancel();
        }
        private async Task GameLoopAsync(CancellationToken token)
        {
            // Targeting ~33 FPS (approx 30ms)
            var frameTarget = TimeSpan.FromMilliseconds(_refreshRate);
            var stopwatch = new Stopwatch();

            while (!token.IsCancellationRequested)
            {
                stopwatch.Restart();

                // UPDATE: Run your physics, positions, collisions here (Off-thread!)
                //UpdateSpaceBattleSimulation();

                // RENDER: Safely invoke the UI thread to draw the updated state
                if (this.IsHandleCreated && !this.IsDisposed)
                {
                    // BeginInvoke is asynchronous; it won't block your physics loop
                    this.BeginInvoke(new Action(() => {
                        RefreshPaint();
                    }));
                }

                // FRAME THROTTLING: Smart delay to maintain a steady frame rate
                stopwatch.Stop();
                int msToWait = (int)(frameTarget.TotalMilliseconds - stopwatch.ElapsedMilliseconds);

                if (msToWait > 0)
                    await Task.Delay(msToWait, token);
            }
        }

        /// <summary>
        /// Handles the timer tick event to trigger a repaint of the control.
        /// </summary>
        private void RefreshPaint()
        {
            if (_bufferedGraphics == null) return;

            // Get the buffer's Graphics object - NO Invalidate() needed!
            var g = _bufferedGraphics.Graphics;

            if (!_pauseScreen)
                // Clear and draw directly to buffer
                g.Clear(this.BackColor);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (!_pauseScreen)
            {
                //// Draw the grid background
                if (!MatrixArray.IsEmpty) MatrixArray.DrawItem(g);

                // Replace the existing foreach over _spaceShapes.DrawList with this:
                if (_spaceCache != null)
                    g.DrawImage(_spaceCache, this.Padding.Left, this.Padding.Top);

                if (_showVersion)
                    g.DrawString(_appInfo, _smallFlierFont, Brushes.White, _versionLoc);

                if (_showComet)
                {
                    // if comet is off the screen, lets reset it.
                    if (_lastStartPoint.IsEmpty || !ClientRectangle.Contains(_lastStartPoint))
                    {
                        _xCounter = -110.0f;
                        _yCounter = 0.0f;
                    }
                    else
                    {
                        _xCounter += 0.1f;
                        _yCounter += 0.05f;
                    }

                    foreach (var (start, end, pen) in _cometShapes.DrawList)
                    {
                        var xStart = start.X + _xCounter;
                        var yStart = start.Y + _yCounter;

                        var sPf = new PointF(xStart, yStart);
                        var ePf = new PointF(end.X + _xCounter, end.Y + _yCounter);

                        // use for ClientRectangle.Contains later to ensure the comet isn't off the screen and it requires an int.
                        _lastStartPoint = new Point((int)xStart, (int)yStart);

                        g.DrawLine(pen, sPf, ePf);
                    }
                }

                if (_showPlanets)
                {
                    using (GraphicsPath path = new GraphicsPath())
                    {
                        path.AddEllipse(_redPlanetRect);
                        g.SetClip(path);
                    }

                    _xOffset += _planetSpinSpeed;
                    if (_xOffset >= _planetSize * 2) _xOffset = 0;

                    // Draw moving texture (scaled dynamically)
                    // We draw it at wrapWidth so the image stretches/shrinks to fit the planet
                    g.DrawImage(_planetTexture, _redPlanetRect.X - _xOffset, _redPlanetRect.Y, _planetWrapWidth, _planetSize);
                    g.DrawImage(_planetTexture, (_redPlanetRect.X - _xOffset) + _planetWrapWidth, _redPlanetRect.Y, _planetWrapWidth, _planetSize);

                    // reset clipping region to prevent it from affecting other drawing operations
                    g.ResetClip();

                    // Optional: Draw a border so the edges look sharp
                    g.DrawEllipse(_planetBorderPen, _redPlanetRect);
                }

                // Draw the Space Battle (Fighters & Raiders)
                foreach (var ship in _battleShips)
                    if (ship.Visible) ship.DrawItem(g);
            }

            if (_fKeyDisplay.Length > 0)
            {
                float y = this.FormSize.Height - this.Padding.Bottom - (_fKeyDisplay.Length * 24);
                float width = _fKeyDisplay.OrderByDescending(s => s.Length).FirstOrDefault()?.Length ?? 60.0f;
                RectangleF rect = new RectangleF(this.Padding.Left + 5, y - 5, (width * 10.0f) + 10.0f, (_fKeyDisplay.Length * 20) + 20);
                g.FillRectangle(Brushes.Blue, rect);
                g.DrawRectangle(new Pen(Brushes.Yellow, 1), rect);

                var yStart = this.ViewSize.Height - Padding.Bottom - 30 - ((_fKeyDisplay.Length - 1) * 20);
                for (int i = 0; i < _fKeyDisplay.Length; i++)
                    g.DrawString(_fKeyDisplay[i], _statsFont, Brushes.Yellow, new PointF(Padding.Left + 10, yStart + ((i * 20))));
            }
            else
                g.DrawString($"{_diffRaiders:0.0}%", _xSmallFlierFont, Brushes.White, _percResetLoc);

            if (TitleText.Visible) TitleText.DrawItem(g);
            if (CloseButton.Visible) CloseButton.DrawItem(g);

            // Render buffer to screen (THIS is the only drawing that hits the screen)
            _bufferedGraphics.Render();
        }

        private void ItemReq_ShipStatusChanged(object sender, StatusChangeArgs e)
        {
            //only care about dead Raiders for now.
            if (e.ShipStatus != ShipStatus.Dead || e.ShipType != ShipType.Raider)
                return;

            _aliveRaiders = ItemReq.RaiderAliveCount;
            _aliveAlly = ItemReq.AllyAliveCount;

            if (_aliveRaiders == 0 || _aliveAlly == 0)
            {
                ItemReq.ResetAllShips();
                // resync
                _aliveRaiders = _orgTotalRaiders;
                _totalRaiders = _orgTotalRaiders;
                _diffRaiders = 100.0f;
            }
            else
            {
                _diffRaiders = (_aliveRaiders / _totalRaiders) * 100;
                if (_diffRaiders <= 50.0f)
                {
                    ItemReq.ResetAliveRaiders();
                    _totalRaiders = _aliveRaiders;
                    _diffRaiders = (_aliveRaiders / _totalRaiders) * 100;
                }
            }
        }

        /// <summary>
        /// Handles the actions required when the form is closed.
        /// </summary>
        /// <remarks>This method releases resources associated with the form and ensures proper cleanup.
        /// It is called when the form is closed, either by the user or programmatically.</remarks>
        /// <param name="e">A FormClosedEventArgs that contains the event data.</param>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // Stop timers first
            StopLoop();

            // Dispose buffer
            _bufferedGraphics?.Dispose();
            _bufferedGraphics = null;

            // Dispose all ships
            foreach (var ship in _battleShips)
                ship?.Dispose();

            _battleShips.Clear();

            _spaceCache?.Dispose();
            _spaceCache = null;

            if (_disableAutoLock)
                Externs.RestoreAutoLock();

            base.OnFormClosed(e);
        }
        #endregion

        #region Private Support Properties
        /// <summary>
        /// Gets the current FormSize of the form excluding padding.
        /// </summary>
        /// <remarks>
        /// Single point of Change (SPOC) for any future adjustments to how the view size is calculated or returned.
        /// </remarks>
        private Size ViewSize => new Size(this.FormSize.Width - (this.Padding.Left + this.Padding.Right),
                                          this.FormSize.Height - (this.Padding.Top + this.Padding.Bottom));
        /// <summary>
        /// Gets the current size of the form.<br/>
        /// </summary>
        /// <remarks>
        /// Single point of Change (SPOC) for any future adjustments to how the form size is calculated or returned.
        /// </remarks>
        private Size FormSize
            => this.ClientSize;
        #endregion
    }
}
