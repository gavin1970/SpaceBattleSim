using Chizl.Applications;
using Chizl.ThreadSupport;
using System.Drawing.Drawing2D;
using static DynamicTimeDraw.StaticConfig;

namespace DynamicTimeDraw
{
    public partial class BgPlatform : Form
    {
        // Set to true to make the background transparent
        // while keeping the grid lines visible.
        static bool _transparentBG = false;
        const string _appTitle = "WinForm Random Battleground";
        static string _appInfo = "Version: {0} - Press F5 reset dead, Press F1 or F2 for Ship Info/Stats, Mouse over far right top for close button, Mouse over far left top and click banner that pops up for transparent background..";
        const string _appTitleAbout = "chizl.com";
        const string _formClosing = "Form_Closed";
        const bool _useBattlegrounds = true;

        const bool _usePlanets = true;
        const int _planetSize = 100;
        const float _planetSpinSpeed = 0.1f;
        private readonly Bitmap _planetTexture = new Bitmap(".\\skins\\fungal_planet.png"); // Load your map here
        private float _xOffset = 0;
        const int _planetWrapWidth = _planetSize * 2;
        Rectangle _redPlanetRect = Rectangle.Empty;


        // Default border width for controls
        const uint _borderWidth = 2;
        // Size of the matrix grid (50pxx50px)
        const int _matrixCellSize = 40;
        // Total number of flighters and Raiders combined.
        const int _flierCount = 100;
        // Number of capital shipss to create
        const int _capShipCount = (_flierCount / 10);
        // Number of RepairRig ships to create
        const int _repairRigCount = (_flierCount / 10);

        // Character to use for the first set of BattleShips shapes. If you use things like '⣮ ⣭ ⣪', remember,
        // you must make the size wider and lower the height to see them side by side. If you want them
        // stacked, then make the size taller and narrower. You can use any Unicode character for the ship
        // shapes, so feel free to experiment with different symbols to find ones that you like and that fit
        // well with the overall design of the ships look.
        // I found the font Arial shows these characters well, but you may want to experiment with other fonts
        // to find the best look for your ships. Some good resources for finding interesting Unicode characters
        // include websites like UnicodeTable.com.
        // I wrote a EmojiLive library found on https://github.com/gavin1970/Chizl.EmojiLive, where it will
        // provide 4098 different Unicode characters and allow you to save as an image. You can use some of
        // these for your ships, including a wide variety of symbols, shapes, and icons that can add visual
        // interest and variety to your animation. You can browse through the available characters in the
        // library and experiment with different combinations
        // (e.g., "X", "❿", "⬤", "⧉", "⭄", "❖", "⬙", "░", "▒", "▓", "▢", "▣", "⣮ ⣭ ⣪", etc.) 
        //readonly string _fighterShip = char.ConvertFromUtf32(11033);    // 11033 - ⬙ = \u2b59
        //readonly string _raiderShip = char.ConvertFromUtf32(10618);     // 10618 - ⥺ = \u293a
        //readonly string _capitalShip = char.ConvertFromUtf32(11159);    // 11159 - ⮗ = \u2b97
        //readonly string _repairRigShip = char.ConvertFromUtf32(10070);     // 10070 - ❖ = \u2756

        // Moves X position of HomeBase and _capitalShip anchor points if changed.
        const float _moveX = 0.0f;          // Center Screen: 200.0f, Far Left: -680.0f, Far Right: 1080.0f
        // Moves Y position of HomeBase and _capitalShip anchor points if changed.
        const float _moveY = 0.0f;          // Center Screen: 0.0f, Far Top: -455.0f, Far Bottom: 450.0f.
        // _capitalShip Lines: - Base X anchor point of lines to the large BattleShips moving items.
        const float _anchorX = 758.0f;
        // _capitalShip Lines: - Base Y anchor point of lines to the large BattleShips moving items.
        const float _anchorY = 540.0f;
        // Tuple to hold the shadow color and depth for consistent styling across controls.
        readonly (Color color, uint depth) _shadowStyle = (Color.FromArgb(64, Color.White), 5);
        // Fonts for different controls, using Arial as a common font for simplicity. Adjust sizes and styles as needed.
        readonly Font _smallFlierFont = new Font("Arial", 12, FontStyle.Regular);
        readonly Font _largeFlierFont = new Font("Arial", 16, FontStyle.Regular);
        readonly Font _closeBtnFont = new Font("Arial", 22, FontStyle.Regular);
        readonly Font _titleFont = new Font("Arial", 14, FontStyle.Bold);
        readonly Font _statsFont = new Font("Courier New", 12, FontStyle.Regular);
        // Colors for different ship types to provide visual distinction between them.
        readonly Color _raiderColor = Color.FromArgb(255, 255, 0, 0);
        readonly Color _fighterColor = Color.FromArgb(255, 0, 255, 0);
        readonly Color _capitalShipColor = Color.FromArgb(255, 0, 255, 255);
        readonly Color _repairRigShipColor = Color.FromArgb(255, 255, 0, 255);
        readonly Color _homeBaseLinkRepairRigColor = Color.FromArgb(32, 255, 255, 255);
        // Thread-Safe - EventStatus object to track whether the form has already been
        // closed, preventing multiple closure attempts.
        readonly EventStatus _eventStatus = new EventStatus();
        // ItemReq objects for the various controls to paint on the form. These are initialized in the BuildObjects method.
        private static ItemReq CloseButton = ItemReq.Empty;
        private static ItemReq MatrixArray = ItemReq.Empty;
        private static ItemReq TitleText = ItemReq.Empty;
        private static ItemReq HomeBase = ItemReq.Empty;
        //private static ItemReq SpaceAndTime = ItemReq.Empty;
        private static DShapes _spaceShapes = new DShapes();
        private static DShapes _cometShapes = new DShapes();
        // Add this field alongside _spaceShapes / _cometShapes
        private static Bitmap? _spaceCache = null;


        internal static List<ItemReq> BattleShips = new List<ItemReq>();
        private static string[] _shipInfo = { };

        public BgPlatform()
        {
            InitializeComponent();

            // Set the form's padding based on config.
            this.Padding = FormStyle.Padding;

            // Set the form closed event status to false initially. This will be used to
            // track whether the form has already been closed, preventing multiple closure
            // attempts if the close button is clicked multiple times.
            _eventStatus.Set(_formClosing, false);

            // Start the object creation process with a short
            // delay to ensure the form is fully initialized.
            BuildObjects(100);

            //set the app info text with the current file version for display in the bottom-left
            //corner of the form. This provides users with version information about the application,
            //which can be useful for troubleshooting or ensuring they are using the latest version.
            _appInfo = string.Format(_appInfo, About.FileVersion);

            // Attach a MouseMove event handler to the form to check for mouse interactions with
            // the controls. This allows for dynamic interaction with the controls, such as changing
            // the cursor when hovering over interactive elements or triggering animations when the
            // mouse moves over certain areas.
            this.MouseMove += (s, e) =>
            {
                // Direct call to all ships to check their hitboxes
                //foreach (var ship in BattleShips) 
                //    ship.IsMouseInRect(e.Location);
                if (CloseButton.IsMouseInRect(e.Location))
                    CloseButton.Visible = true;
                else
                    CloseButton.Visible = false;

                if (TitleText.IsMouseInRect(e.Location))
                    TitleText.Visible = true;
                else
                    TitleText.Visible = false;
            };

            this.KeyDown += (s, e) =>
            {
                var isF1 = e.KeyCode == Keys.F1;    //details
                var isF2 = e.KeyCode == Keys.F2;    //summary
                if (isF1 || isF2)
                {
                    // Use Invoke to ensure we're on the UI thread when closing the form
                    this.Invoke(new Action(() =>
                    {
                        // if not F1, then F2
                        _shipInfo = ItemReq.GetShipStatus(isF1);
                    }));
                }
            };
            this.KeyUp += (s, e) =>
            {
                var isF5 = e.KeyCode == Keys.F5;    //refresh
                var isF1 = e.KeyCode == Keys.F1;
                var isF2 = e.KeyCode == Keys.F2;
                if (isF1 || isF2)
                {
                    // Use Invoke to ensure we're on the UI thread when closing the form
                    this.Invoke(new Action(() =>
                    {
                        _shipInfo = new string[] { };
                    }));
                }
                else if (isF5)
                    ItemReq.ResetDeadShips();
            };
        }

        private static float _xCounter = 0.0f;
        private static float _yCounter = 0.0f;
        private static Point _lastStartPoint = Point.Empty;
        private static bool _use3DPlanets = false;

        private void BgPlatform_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;

            // Set global quality once for the whole frame
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            //// Draw the grid background
            if (!MatrixArray.IsEmpty) MatrixArray.DrawItem(e.Graphics);

            // Replace the existing foreach over _spaceShapes.DrawList with this:
            if (_spaceCache != null)
                g.DrawImage(_spaceCache, this.Padding.Left, this.Padding.Top);

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

            g.DrawString(_appInfo, _smallFlierFont, Brushes.White, new PointF(Padding.Left + 10, this.FormSize.Height - Padding.Bottom - 25));

            if (_usePlanets)
            {
                // ############################# PLANETS
                // The math: The full wrap of the planet is twice the visible width
                if (_use3DPlanets)
                {
                    // Place this inside your OnPaint, after drawing the moving texture
                    using (GraphicsPath shadowPath = new GraphicsPath())
                    {
                        // Define a path that matches your planet's circle
                        shadowPath.AddEllipse(_redPlanetRect);
                        g.SetClip(shadowPath);

                        using (PathGradientBrush pgb = new PathGradientBrush(shadowPath))
                        {
                            // Set the "Highlight" (the part that looks like light hitting the ball)
                            // CenterPoint moves the light source (e.g., top-left)
                            pgb.CenterPoint = new PointF(_redPlanetRect.X + _planetSize * 0.3f, _redPlanetRect.Y + _planetSize * 0.3f);
                            pgb.CenterColor = Color.FromArgb(0, Color.White); // Transparent in center to see texture

                            // Set the "Shadow" (the dark edge of the planet)
                            pgb.SurroundColors = new Color[] { Color.FromArgb(180, Color.Black) };

                            // Fill the circle with this gradient
                            g.FillEllipse(pgb, _redPlanetRect);
                        }
                    }
                }
                else
                {
                    using (GraphicsPath path = new GraphicsPath())
                    {
                        path.AddEllipse(_redPlanetRect);
                        g.SetClip(path);
                    }
                }

                // 2. Draw moving texture (scaled dynamically)
                // We draw it at wrapWidth so the image stretches/shrinks to fit the planet
                g.DrawImage(_planetTexture, _redPlanetRect.X - _xOffset, _redPlanetRect.Y, _planetWrapWidth, _planetSize);
                g.DrawImage(_planetTexture, (_redPlanetRect.X - _xOffset) + _planetWrapWidth, _redPlanetRect.Y, _planetWrapWidth, _planetSize);

                // 3. Clean up
                g.ResetClip();

                // Optional: Draw a border so the edges look sharp
                g.DrawEllipse(Pens.Black, _redPlanetRect);
                // #############################
            }

            // Draw the Space Battle (Fighters & Raiders)
            foreach (var ship in BattleShips)
                if (ship.Visible) ship.DrawItem(e.Graphics);

            // Draw the Infrastructure (HomeBase & UI)
            if (!HomeBase.IsEmpty) HomeBase.DrawItem(e.Graphics);
            if (TitleText.Visible) TitleText.DrawItem(e.Graphics);

            if (_shipInfo.Length > 0)
            {
                float y = this.FormSize.Height - this.Padding.Bottom - (_shipInfo.Length * 24);
                float width = _shipInfo.OrderByDescending(s => s.Length).FirstOrDefault()?.Length ?? 60.0f;
                RectangleF rect = new RectangleF(this.Padding.Left + 5, y - 5, (width * 10.0f) + 10.0f, (_shipInfo.Length * 20) + 20);
                g.FillRectangle(Brushes.Blue, rect);
                g.DrawRectangle(new Pen(Brushes.Yellow, 1), rect);

                for (int i = 0; i < _shipInfo.Length; i++)
                    g.DrawString(_shipInfo[i], _statsFont, Brushes.Yellow, new PointF(Padding.Left + 10, this.FormSize.Height - Padding.Bottom - 30 - ((i + 1) * 20)));
            }

            if (CloseButton.Visible) CloseButton.DrawItem(e.Graphics);
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
            // Delay the creations to ensure the form is fully initialized
            await Task.Delay(startDelay).ContinueWith(_ =>
            {
                // first object created so it appears behind the others
                BuildMatrixArray();

                //Build out stars, planets, and other space objects in the background before building the HomeBase
                //and BattleShips to create a sense of depth and immersion in the space battle scene. By drawing
                //these elements first, they will appear behind the HomeBase and BattleShips, enhancing the visual
                //complexity and making the overall scene more engaging. You can use simple shapes like circles for
                //stars and planets, or even use images for more detailed backgrounds. Consider adding subtle animations
                //to these background elements (e.g., twinkling stars or slowly rotating planets) to further enhance
                //the dynamic feel of the scene.
                BuildSpaceTime();

                // =================================================================
                // Build other controls on top of the matrix background, but in the order of which layer they should appear.
                BuildHomeBase();
                BuildFliers();

                // =================================================================
                // last object created so it appears on top of the others
                BuildTitleText();
                BuildCloseButton();

                // If transparency background, you can create a fully transparent background while still allowing the
                // grid lines to be visible. Adjusting the Alpha value allows you to control the transparency level of
                // the background without affecting the visibility of the grid lines or other controls. This can be
                // useful for creating an overlay effect where only the grid lines are visible on top of other windows
                // or applications. NOTE: This does make it click-through, so you won't be able to interact with
                // anything except solid controls like the close button.
                if (_transparentBG)
                    this.Invoke(new Action(() => { this.TransparencyKey = this.BackColor; }));
            });

#if DEBUG
            this.TopMost = false;
#else 
            this.TopMost = true;
#endif
        }

        #region Build Paint Objects
        /// <summary>
        /// Builds the static space background — star field, nebulae, and a comet —
        /// into <c>_spaceShapes</c> once so it can be replayed every paint frame.
        /// </summary>
        private void BuildSpaceTime()
        {
            var center = new Point(this.ViewSize.Width / 2, this.ViewSize.Height / 2);
            if (_spaceCache == null)
            {
                this.Invoke(new Action(() =>
                {
                    // Red Planet Setup, placed here so it is behind the HomeBase and
                    // BattleShips but in front of the static starfield and nebulae
                    // background to create a sense of depth. The planet will also
                    // have a simple left-to-right scrolling animation to add some
                    // dynamic movement to the scene.
                    _redPlanetRect = new Rectangle(center.X + _planetWrapWidth, center.Y + _planetSize, _planetSize, _planetSize);

                    var rng = Random.Shared;
                    var bounds = new RectangleF(this.Padding.Left, this.Padding.Top,
                                                this.ViewSize.Width, this.ViewSize.Height);

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

                    // --- Star field: two passes for depth ---
                    SpaceBackground.AddStarField(tmp, bounds, 350, rng);
                    var innerBounds = new RectangleF(bounds.X + 60, bounds.Y + 60,
                                                      bounds.Width - 120, bounds.Height - 120);
                    SpaceBackground.AddStarField(tmp, innerBounds, 80, rng);

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

                    // Draw all collected segments onto the bitmap
                    foreach (var (start, end, pen) in tmp.DrawList)
                        bg.DrawLine(pen, start, end);
                }));
            }

            // Comet stays in _cometShapes (it animates each frame)
            if (_cometShapes.DrawList.Count == 0)
            {
                this.Invoke(new Action(() =>
                {
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
            if (!MatrixArray.IsEmpty)
                return;

            // Create a grid of ItemReq objects for the matrix background
            int cols = this.ViewSize.Width / _matrixCellSize;
            int rows = this.ViewSize.Height / _matrixCellSize;

            MatrixArray = new ItemReq(this, $"MatrixArray")
            {
                Location = new PointF(this.Padding.Left, this.Padding.Top),
                Size = new Size(this.ViewSize.Width, this.ViewSize.Height),
                BGColor = Color.FromArgb(255, this.BackColor),
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
            if (!CloseButton.IsEmpty)
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
                    CloseButton.DText.Font = _closeBtnFont;
                    CloseButton.DText.TextColor = Color.White;
                }
            }));
        }
        /// <summary>
        /// Initializes and adds animated close button items
        /// to the BattleShips collection if it is empty.
        /// </summary>
        /// <remarks>
        ///    Each item is positioned randomly near the center of the form and can display 
        ///    either an 'X' character or intersecting lines, depending on configuration.
        /// </remarks>
        private void BuildFliers()
        {
            if (BattleShips.Count > 0)
                return;

            // Size of the flier button
            var flierSize = new Size(20, 20);
            var capSize = new Size(100, 100);
            var repairSize = new Size(20, 20);

            /*
            int raiderCodePoint = char.ConvertToUtf32(_raiderShip, 0);
            int fighterCodePoint = char.ConvertToUtf32(_fighterShip, 0);
            int capitalCodePoint = char.ConvertToUtf32(_capitalShip, 0);
            int repairRigCodePoint = char.ConvertToUtf32(_repairRigShip, 0);
            string raiderChar = char.ConvertFromUtf32(raiderCodePoint);
            string fighterChar = char.ConvertFromUtf32(fighterCodePoint);
            string capitalChar = char.ConvertFromUtf32(capitalCodePoint);
            string repairRigChar = char.ConvertFromUtf32(repairRigCodePoint);
            /**/

            // Calculate the X/Y Location of the close button that will be in the top-right corner
            // of the screen. Accounting for top and right padding along with the MatrixArray border
            // width to ensure it doesn't overlap with the matrix grid lines
            var w = this.FormSize.Width;
            var h = this.FormSize.Height;

            this.Invoke(new Action(() =>
            {
                var raidImage = new ShipStats(ShipType.Raider).ShipView;
                var fighterImage = new ShipStats(ShipType.Fighter).ShipView;
                int x, y;
                var fighterCnt = _flierCount / 3;

                // Create x amount of  animated "X" items that will fling out
                // from the center of the form when triggered.
                for (int cnt = 0; cnt < _flierCount; cnt++)
                {
                    // Randomly position the BattleShips items within the bounds of the form
                    x = Random.Shared.Next(0, w + 1);
                    y = Random.Shared.Next(0, h + 1);

                    // Alternate between two different ship characters for visual variety
                    // By use 3, it there will be more raiders than fighters, which adds more
                    // visual interest and variety to the animation. You can adjust the modulo
                    // value and the conditions to create different patterns of ship
                    // types (e.g., every 2nd item is a fighter, every 5th item is a raider, etc.)
                    // depending on the look you want to achieve.
                    //var shipImg = (x % 3) == 0 ? fighterImage : raidImage;
                    //var shipType = (x % 3) == 0 ? ShipType.Fighter : ShipType.Raider;
                    //var shipColor = (x % 3) == 0 ? _fighterColor : _raiderColor;
                    var shipImg = cnt < fighterCnt ? fighterImage : raidImage;
                    var shipType = cnt < fighterCnt ? ShipType.Fighter : ShipType.Raider;
                    var shipColor = cnt < fighterCnt ? _fighterColor : _raiderColor;
                    // fighterCnt
                    var partName = $"{shipType}";

                    var fly = new ItemReq(this, $"{partName}_{cnt:000}")
                    {
                        Location = new PointF(x, y),
                        Size = flierSize,
                        ShadowDepth = _shadowStyle.depth,
                        DText = {
                            Font = _smallFlierFont,
                            Text = shipImg,
                            ShadowDepth = _shadowStyle.depth,
                            ShadowColor = Color.FromArgb(32, shipColor),
                        },
                        DestinationRange = (uint)this.Width / 2,
                        Animation = true,
                        Visible = true
                    };

                    fly.SpaceBattle = _useBattlegrounds;
                    fly.SetShiptType(shipType, shipColor);

                    BattleShips.Add(fly);
                }

                var capImage = new ShipStats(ShipType.Capital).ShipView;
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
                        ShadowDepth = _shadowStyle.depth,
                        SpaceBattle = _useBattlegrounds,
                        DText = {
                            Font = _largeFlierFont,
                            Text = capImage,
                            ShadowDepth = _shadowStyle.depth,
                            ShadowColor = Color.FromArgb(64, _capitalShipColor),
                        },
                        DestinationRange = (uint)this.Width / 2,
                        Animation = true,
                        Visible = true
                    };

                    // When anchoring lines and using Animation, the start of the line is
                    // the anchor location, while the end is dynamic following the ItemRec.
                    fly.SpaceBattle = _useBattlegrounds;
                    fly.SetShiptType(ShipType.Capital, _capitalShipColor);
                    BattleShips.Add(fly);
                }

                var repairRigImage = new ShipStats(ShipType.RepairRig).ShipView;
                // Create x amount of  animated "X" items that will fling out
                // from the center of the form when triggered.
                for (int cnt = 0; cnt < _repairRigCount; cnt++)
                {
                    x = Random.Shared.Next((int)(_anchorX - 100.0f), (int)(_anchorX + 100.0f));
                    y = Random.Shared.Next((int)(_anchorY - 100.0f), (int)(_anchorY + 100.0f));

                    var fly = new ItemReq(this, $"RepairRig_{cnt:000}")
                    {
                        Location = new PointF(x, y),
                        Size = repairSize,
                        ShadowDepth = _shadowStyle.depth,
                        SpaceBattle = _useBattlegrounds,
                        DText = {
                            Font = _smallFlierFont,
                            Text = repairRigImage,
                            ShadowDepth = _shadowStyle.depth,
                            ShadowColor = Color.FromArgb(64, _repairRigShipColor),
                        },
                        DestinationRange = (uint)this.Width / 2,
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
                    fly.DLine.Add(new PointF(_anchorX + _moveX, _anchorY + _moveY), new PointF(fly.Right, fly.Bottom));
                    fly.SpaceBattle = _useBattlegrounds;
                    fly.SetShiptType(ShipType.RepairRig, _repairRigShipColor);
                    BattleShips.Add(fly);
                }
            }));
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

                List<float[]> coordsList = new List<float[]>();

                // ---===[ Outer, Top, Left ]===---
                coordsList.Add(new float[] { 697, 506, 723, 491, 723, 520, 697, 536 });
                // ---===[ Outer, Top ]===---
                coordsList.Add(new float[] { 732, 486, 758, 471, 784, 486, 758, 501 });
                // ---===[ Outer, Top, Right ]===---
                coordsList.Add(new float[] { 793, 491, 819, 506, 819, 536, 793, 521 });
                // ---===[ Inner, Top, Left ]===---
                coordsList.Add(new float[] { 697, 541, 728, 523, 728, 489, 755, 506, 755, 539, 725.5f, 556 });
                // ---===[ Inner, Top, Right ]===---
                coordsList.Add(new float[] { 760.5f, 539, 760.5f, 505, 788.5f, 489, 788.5f, 523, 818, 541, 790.5f, 556 });
                // ---===[ Outer, Bottom, Left ]===---
                coordsList.Add(new float[] { 697, 546, 723, 561, 723, 591, 697, 576 });
                // ---===[ Outer, Bottom ]===---
                coordsList.Add(new float[] { 732, 596, 758, 581, 784, 596, 758, 611 });
                // ---===[ Outer, Bottom, Right ]===---
                coordsList.Add(new float[] { 793, 561, 819, 546, 819, 575, 793, 591 });
                // ---===[ Inner, Bottom ]===---
                coordsList.Add(new float[] { 727.5f, 560, 757.5f, 543, 788, 560, 788, 593, 757.5f, 575, 727.5f, 593 });

                // lines from center point for all corners of the inner HomeBase shape
                foreach (var cords in coordsList)
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
                            Font = _titleFont,
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

        /// <summary>
        /// Handles the timer tick event to trigger a repaint of the control.
        /// </summary>
        /// <param name="sender">The source of the event, typically the timer that raised the event.</param>
        /// <param name="e">An EventArgs object that contains the event data.</param>
        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            if (_usePlanets)
            {
                _xOffset += _planetSpinSpeed;
                if (_xOffset >= _planetSize * 2) _xOffset = 0; // Dynamic reset point
            }

            this.Invalidate();
        }
        /// <summary>
        /// Runs ever 30sec to see if a reset is needed.
        /// </summary>
        private void AutoResetTimer_Tick(object sender, EventArgs e)
        {
            if (ItemReq.NeedsDeadReset())
                ItemReq.ResetDeadShips();
        }
        private void BgPlatform_FormClosed(object sender, FormClosedEventArgs e)
        {
            // Ensure that the space cache bitmap is properly disposed of when the form is closed
            _spaceCache?.Dispose();
            _spaceCache = null;
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _spaceCache?.Dispose();
            _spaceCache = null;
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
        private Size FormSize =>
            this.Size;
        #endregion
    }
}
