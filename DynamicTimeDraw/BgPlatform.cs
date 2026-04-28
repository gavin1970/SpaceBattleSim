using Chizl.ThreadSupport;
using System.Drawing;
using System.Runtime.ConstrainedExecution;
using static DynamicTimeDraw.StaticConfig;

namespace DynamicTimeDraw
{
    public partial class BgPlatform : Form
    {
        // Set to true to make the background transparent
        // while keeping the grid lines visible.
#if DEBUG
        static bool _transparentBG = false;
#else
        static bool _transparentBG = true;
#endif

        const string _appTitle = "Gavin's Animation Demo";
        const string _appTitleAbout = "chizl.com";
        const string _formClosing = "Form_Closed";
        const bool _useBattlegrounds = true;

        const uint _borderWidth = 2;        // Default border width for controls
        const int _matrixCellSize = 40;     // Size of the matrix grid (50pxx50px)

        // Character to use for the first set of flingX items (e.g., "X", "❿", "⬤", etc.)
        const string _fling1Str = "⬙";
        // Number of flingX items to create
        const int _fling1Count = 30;

        // Character to use for the second set of flingX items (e.g., "⭄", "⬙", "⬤", "❿", "░", "▒", "▓", "▢", "▣", "⣮ ⣭ ⣪", etc.) 
        const string _fling2Str = "⭄";
        // Number of flingX items to create for the second set with different styling
        const int _fling2Count = (_fling1Count / 7);

        const float _moveX = 0.0f;          // Moves X position of HomeBase and _fling2Str anchor points if changed.
                                            // Center Screen: 200.0f, Far Left: -680.0f, Far Right: 1080.0f
        const float _moveY = 0.0f;          // Moves Y position of HomeBase and _fling2Str anchor points if changed.
                                            // Center Screen: 0.0f, Far Top: -455.0f, Far Bottom: 450.0f.
        const float _anchorX = 758.0f;      // _fling2Str Lines: - Base X anchor point of lines to the large flingX moving items.
        const float _anchorY = 540.0f;      // _fling2Str Lines: - Base Y anchor point of lines to the large flingX moving items.

        readonly (Color color, uint depth) _shadowStyle = (Color.FromArgb(64, Color.White), 5);
        readonly Font _flyxFont = new Font("Arial", 12, FontStyle.Regular);
        readonly Font _flyLrgXFont = new Font("Arial", 20, FontStyle.Regular);
        readonly Font _closeBtnFont = new Font("Arial", 22, FontStyle.Regular);
        readonly Font _titleFont = new Font("Arial", 14, FontStyle.Bold);

        readonly Color fgColor4 = Color.FromArgb(255, Color.White);         // divisible by 11   (rare, make brightest)
        readonly Color fgColor6 = Color.FromArgb(255, Color.Green);   // divisible by 7    (rare, make brightest)
        readonly Color fgColor1 = Color.FromArgb(192, Color.Turquoise);    // divisible by 10   (kinda rare, make brigher)
        readonly Color fgColor2 = Color.FromArgb(128, Color.RosyBrown);    // divisible by 3    (most common,t make dimmer)
        readonly Color fgColor5 = Color.FromArgb(128, Color.Snow);         // divisible by 2    (most common, make dimmer)
        readonly Color fgColor3 = Color.FromArgb(128, Color.Orange);       // default           (rest, dimmer, not divisible by 2, 3, 7, 10, or 11)
        readonly EventStatus _eventStatus = new EventStatus();

        internal static ItemReq CloseButton = ItemReq.Empty;
        internal static ItemReq MatrixArray = ItemReq.Empty;
        internal static ItemReq TitleText = ItemReq.Empty;
        internal static ItemReq HomeBase = ItemReq.Empty;
        internal static List<ItemReq> FlingX = new List<ItemReq>();

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

            this.Paint += BgPlatform_Paint;
            this.MouseMove += (s, e) => {
                // Direct call to all ships to check their hitboxes
                foreach (var ship in FlingX) ship.IsMouseInRect(e.Location);
                CloseButton.IsMouseInRect(e.Location);
            };
        }

        private void BgPlatform_Paint(object sender, PaintEventArgs e)
        {
            // 1. Set global quality once for the whole frame
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // 2. Draw the grid background
            if (!MatrixArray.IsEmpty) MatrixArray.DrawItem(e.Graphics);

            // 3. Draw the Space Battle (Fighters & Raiders)
            foreach (var ship in FlingX)
            {
                if (ship.Visible) ship.DrawItem(e.Graphics);
            }

            // 4. Draw the Infrastructure (HomeBase & UI)
            if (!HomeBase.IsEmpty) HomeBase.DrawItem(e.Graphics);
            if (!TitleText.IsEmpty) TitleText.DrawItem(e.Graphics);
            if (!CloseButton.IsEmpty) CloseButton.DrawItem(e.Graphics);
        }

        private void BgPlatform_Paint2(object sender, PaintEventArgs e)
        {
            // The form's Paint event is used to trigger the drawing of all ItemReq controls.
            // Each ItemReq is responsible for drawing itself when the form repaints. By calling
            // Refresh on each ItemReq, we ensure that they are redrawn with their current properties
            // and states. This allows for dynamic updates to the controls (like animations, color changes, etc.)
            // to be reflected visually on the form whenever it repaints.
            MatrixArray.Refresh();
            HomeBase.Refresh();
            TitleText.Refresh();
            CloseButton.Refresh();
            foreach (var flier in FlingX) flier.Refresh();
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

                // =================================================================
                // Build other controls on top of the matrix background, but in the order of which layer they should appear.
                BuildHomeBase();
                BuildFlingX();

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
            if (MatrixArray.IsEmpty)
            {
                this.Invoke(new Action(() =>
                {
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
                }));
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
            if (CloseButton.IsEmpty)
            {
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
                            // Force the form to redraw to update the button's appearance
                            CloseButton.Refresh();
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
                            // Force the form to redraw to update the button's appearance
                            CloseButton.Refresh();
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
        }
        /// <summary>
        /// Initializes and adds animated close button items
        /// to the FlingX collection if it is empty.
        /// </summary>
        /// <remarks>
        ///    Each item is positioned randomly near the center of the form and can display 
        ///    either an 'X' character or intersecting lines, depending on configuration.
        /// </remarks>
        private void BuildFlingX()
        {
            if (FlingX.Count == 0)
            {
                // Size of the close button
                var closeSize = new Size(30, 30);
                // Set to true to use lines instead of text for the "X" button
                var useLines = false;

                // Calculate the X/Y Location of the close button that will be in the top-right corner
                // of the screen. Accounting for top and right padding along with the MatrixArray border
                // width to ensure it doesn't overlap with the matrix grid lines
                var w = this.FormSize.Width;
                var h = this.FormSize.Height;

                this.Invoke(new Action(() =>
                {
                    int x, y;
                    // Create x amount of  animated "X" items that will fling out
                    // from the center of the form when triggered.
                    for (int cnt = 0; cnt < _fling1Count; cnt++)
                    {
                        // Randomly position the flingX items within the bounds of the form
                        x = Random.Shared.Next(0, w + 1);
                        y = Random.Shared.Next(0, h + 1);
                        var clr = (x % 3) == 0 ? fgColor2 :   // divisible by 3 is the most common other than 2
                                  (x % 7) == 0 ? fgColor6 :   // divisible by 7 is the rarest other than 11.
                                  (x % 10) == 0 ? fgColor1 :  // divisible by 10 is the second rarest.
                                  (x % 11) == 0 ? fgColor4 :  // divisible by 11 is the rarest other than 7.
                                  (x % 2) == 0 ? fgColor5 :   // divisible by 2 is the most common other than 3.
                                  fgColor3;                   // anything else that isn't divisible by 2, 3, 7, 10, or 11.

                        clr = (x % 2) == 0 ? fgColor4 : fgColor6;   // TESTING
                        var partName = clr == fgColor6 ? $"{ShipType.Fighter}" : $"{ShipType.Raider}";

                        var fly = new ItemReq(this, $"{partName}_{cnt:000}")
                        {
                            Location = new PointF(x, y),
                            Size = closeSize,
                            //HitBox = 50,
                            ShadowDepth = _shadowStyle.depth,
                            DText = {
                                TextColor = clr,
                                Font = _flyxFont,
                                Text = _fling1Str,
                                ShadowDepth = _shadowStyle.depth,
                                ShadowColor = Color.FromArgb(32, Color.White)
                            },
                            DestinationRange = (uint)this.Width / 2,
                            // DrunkEffect = true,
                            Animation = true,
                            Visible = true
                        };

                        if (clr == fgColor6 || clr == fgColor4)
                        {
                            fly.SpaceBattle = _useBattlegrounds;
                            fly.SetShiptType(clr == fgColor6 ? ShipType.Fighter : ShipType.Raider, clr);
                        }

                        FlingX.Add(fly);
                    }

                    closeSize = new Size(100, 100);
                    var fgColor = Color.FromArgb(64, Color.Yellow);
                    var fgColorTip = Color.FromArgb(128, Color.Gold);
                    useLines = true;

                    // Create x amount of  animated "X" items that will fling out
                    // from the center of the form when triggered.
                    for (int cnt = 0; cnt < _fling2Count; cnt++)
                    {
                        x = Random.Shared.Next(0, w + 1);
                        y = Random.Shared.Next(0, h + 1);

                        var fly = new ItemReq(this, $"Capital_{cnt:000}")
                        {
                            Location = new PointF(x, y),
                            Size = closeSize,
                            ShadowDepth = _shadowStyle.depth,
                            SpaceBattle = _useBattlegrounds,
                            //HitBox = 75,
                            DText = {
                                Text = _fling2Str,
                                TextColor = fgColorTip,
                                ShadowDepth = _shadowStyle.depth,
                                ShadowColor = Color.FromArgb(64, Color.White),
                                Font = _flyLrgXFont,
                            },
                            DestinationRange = (uint)this.Width / 2,
                            DrunkEffect = false,
                            Animation = true,
                            DLine =
                            {
                                // used for the lines in the matrix grid.
                                Pen = new Pen(fgColor, 2),
                                // if LineShadowPen is commented, it wil not have a shadow on the lines.
                                ShadowPen = new Pen(Color.FromArgb(28, fgColor), 2),
                                // Set HasAnchor to true to indicate that the line should be anchored
                                // to a specific point (the center of the form in this case).
                                HasAnchor = true,
                            },
                            Visible = true
                        };

                        if (useLines)
                        {
                            // When anchoring lines and using Animation, the start of the line is
                            // the anchor location, while the end is dynamic following the ItemRec.
                            fly.DLine.Add(new PointF(_anchorX + _moveX, _anchorY + _moveY), new PointF(fly.Right, fly.Bottom));
                        }

                        fly.SpaceBattle = _useBattlegrounds;
                        fly.SetShiptType(ShipType.Capital, fgColorTip);
                        FlingX.Add(fly);
                    }
                }));
            }
        }
        /// <summary>
        /// Initializes the HomeBase item with predefined
        /// coordinates and visual styles if it is empty.
        /// </summary>
        private void BuildHomeBase()
        {
            if (HomeBase.IsEmpty)
            {
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
            if (TitleText.IsEmpty)
            {
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
                                    TitleText.Refresh();
                                }));
                            });
                        }
                    };

                    TitleText.MouseUp += (sender, e) =>
                    {
                        this.Invoke(new Action(() =>
                        {
                            TitleText.Refresh();
                        }));
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

                            _ = Task.Delay(1000).ContinueWith(_ =>
                            {
                                this.Invoke(new Action(() =>
                                {
                                    TitleText.Refresh();
                                }));
                            });
                        }
                        else
                        {
                            this.Invoke(new Action(() =>
                            {
                                // Static reset of all ships to their original positions and states.
                                // This allows the user to click on the title text to reset the animation
                                // and return all flingX items back to their starting positions, providing
                                // a way to restart the animation without having to close and reopen the form.
                                ItemReq.ResetDeadShips();

                                foreach (var flier in FlingX)
                                {
                                    if (!flier.SpaceBattle)
                                    {
                                        flier.Animation = true;
                                        flier.SpaceBattle = _useBattlegrounds;
                                        //flier.DText.Text = _fling1Str;
                                        //flier.DText.TextColor = flier.ShipInfo.ShipsColor;
                                        flier.Refresh();
                                    }
                                }
                            }));
                        }
                    };
                }));
            }
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

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            this.Invalidate();
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
