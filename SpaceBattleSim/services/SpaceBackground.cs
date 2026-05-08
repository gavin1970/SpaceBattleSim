namespace SpaceBattleSim
{
    /// <summary>
    /// Provides pure-draw generators for space background elements: star fields,
    /// interstellar nebula clouds, and comets.  All geometry is emitted as line
    /// segments into a <see cref="DShapes"/> instance so it slots directly into
    /// the existing <c>AddPolygonalShapes</c> / <c>DShapes.Add</c> pipeline.
    /// </summary>
    public static class SpaceBackground
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Star Field
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Scatters <paramref name="count"/> stars randomly inside <paramref name="bounds"/>.
        /// Each star is a 4-point sparkle (two crossing lines) whose brightness and
        /// arm length are chosen randomly to give a natural depth illusion.
        /// <para>
        /// Tip — call twice with different bounds/counts to layer dim background
        /// stars behind brighter foreground ones.
        /// </para>
        /// </summary>
        /// <param name="shapes">Target shape collection.</param>
        /// <param name="bounds">Screen area in which stars may appear.</param>
        /// <param name="count">Total number of stars to generate.</param>
        /// <param name="rng">Shared <see cref="Random"/> instance.</param>
        /// <param name="natrualStarfield">Indicates whether to use a natural starfield style.</param>
        public static void AddStarField(DShapes shapes, RectangleF bounds, int count, Random rng, bool natrualStarfield)
        {
            var coordsList = new List<float[]>(count);
            var penSize = natrualStarfield ? 2f : 1f;

            for (int i = 0; i < count; i++)
            {
                float cx = bounds.Left + (float)rng.NextDouble() * bounds.Width;
                float cy = bounds.Top  + (float)rng.NextDouble() * bounds.Height;

                // Three size tiers: tiny dot (1 px arm), small sparkle, larger sparkle
                float tier = (float)rng.NextDouble();
                float arm  = tier < 0.65f ? 1f          // ~65 % — distant pin-pricks
                           : tier < 0.90f ? 2.5f         // ~25 % — mid-field
                                          : 4f;          // ~10 % — close/bright

                // Alpha proportional to arm size so big stars pop
                int alpha = tier < 0.65f ? rng.Next(40, 100)
                          : tier < 0.90f ? rng.Next(100, 180)
                                         : rng.Next(180, 255);

                var pen = new Pen(Color.FromArgb(alpha, Color.White), penSize);

                if (arm <= 1f || natrualStarfield)
                {
                    // Dot — single 1-px segment (reuses the matrix trick)
                    shapes.Add(new PointF(cx, cy), new PointF(cx + 1f, cy + 1f), pen);
                }
                else
                {
                    // Sparkle — horizontal arm + vertical arm (crossing "+")
                    // Encoded as two 4-element float arrays so AddPolygonalShapes
                    // can handle them, but we call Add directly for simplicity.
                    shapes.Add(new PointF(cx - arm, cy), new PointF(cx + arm, cy), pen);
                    shapes.Add(new PointF(cx, cy - arm), new PointF(cx, cy + arm), pen);

                    // Diagonal flair for the larger tier — makes an "✦" shape
                    if (arm >= 4f)
                    {
                        float diag = arm * 0.55f;
                        var dimPen = new Pen(Color.FromArgb(alpha / 2, Color.White), 1f);
                        shapes.Add(new PointF(cx - diag, cy - diag), new PointF(cx + diag, cy + diag), dimPen);
                        shapes.Add(new PointF(cx - diag, cy + diag), new PointF(cx + diag, cy - diag), dimPen);
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Nebula / Interstellar Cloud with Layered Ellipses
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Renders a nebula cloud directly onto a <see cref="Graphics"/> surface using
        /// layered, semi-transparent filled ellipses with a radial gradient falloff.
        /// Far more efficient and visually richer than the segment-based <see cref="AddNebula"/> 
        /// approach — use this overload when baking to a cached <see cref="Bitmap"/>.
        /// </summary>
        /// <param name="g">Graphics surface to draw onto (typically a Bitmap's Graphics).</param>
        /// <param name="center">Centre of the cloud.</param>
        /// <param name="radius">Outer radius of the cloud in pixels.</param>
        /// <param name="color">
        ///   Base colour. The alpha channel controls peak core opacity (40–90 works well).
        /// </param>
        /// <param name="rng">Shared <see cref="Random"/> instance.</param>
        /// <param name="layers">
        ///   Number of layered ellipses (8–16 gives good depth without many calls).
        /// </param>
        public static void AddNebulaDirect(Graphics g, PointF center, float radius,
                                           Color color, Random rng, int layers = 12)
        {
            // Stretch horizontally for a more natural cloud silhouette
            const float xScale = 1.6f;
            const float yScale = 1.0f;

            // Outer → inner pass: large/faint ellipses build the halo,
            // small/opaque ones build up the glowing core.
            for (int i = 0; i < layers; i++)
            {
                float t = (float)i / layers;   // 0 = outermost, approaches 1 = innermost

                // Radius shrinks toward inner layers so the core is richest
                float layerRadius = radius * (1.0f - t * 0.55f);

                // Random offset keeps the cloud organic, not perfectly round
                float offX = (float)(rng.NextDouble() - 0.5) * radius * 0.4f;
                float offY = (float)(rng.NextDouble() - 0.5) * radius * 0.3f;

                float w = layerRadius * xScale * 2f;
                float h = layerRadius * yScale * 2f;

                // Alpha builds from faint (outer) to richer (inner)
                int alpha = (int)(color.A * (0.15f + t * 0.65f));
                alpha = Math.Clamp(alpha, 6, color.A);

                using var brush = new SolidBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
                g.FillEllipse(brush, center.X + offX - w / 2f, center.Y + offY - h / 2f, w, h);
            }

            // Second pass: bright compact core glow — simulates an emission hotspot
            float coreR = radius * 0.25f;
            for (int i = 0; i < 4; i++)
            {
                float offX = (float)(rng.NextDouble() - 0.5) * radius * 0.12f;
                float offY = (float)(rng.NextDouble() - 0.5) * radius * 0.12f;

                float w = coreR * xScale * 2f;
                float h = coreR * yScale * 2f;

                int alpha = Math.Clamp((int)(color.A * 0.55f), 20, 120);

                using var brush = new SolidBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
                g.FillEllipse(brush, center.X + offX - w / 2f, center.Y + offY - h / 2f, w, h);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Nebula / Interstellar Cloud with Density-Based Line Segments
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Draws an interstellar cloud centred at <paramref name="center"/> using
        /// <paramref name="density"/> short, low-alpha line segments scattered
        /// within a soft elliptical falloff.  The opacity of each segment fades
        /// toward the cloud edge, producing a gaseous glow effect without any
        /// filled shapes or bitmaps.
        /// <para>
        /// Suggested colours — purple nebula: <c>Color.FromArgb(18, 80, 0, 160)</c>;
        /// red/orange emission: <c>Color.FromArgb(18, 180, 40, 0)</c>;
        /// blue reflection: <c>Color.FromArgb(18, 0, 80, 200)</c>.
        /// </para>
        /// </summary>
        /// <param name="shapes">Target shape collection.</param>
        /// <param name="center">Centre point of the cloud.</param>
        /// <param name="radius">Approximate radius of the cloud in pixels.</param>
        /// <param name="color">Base colour; alpha is ignored — density controls opacity.</param>
        /// <param name="density">Number of micro-segments (200–600 works well).</param>
        /// <param name="rng">Shared <see cref="Random"/> instance.</param>
        public static void AddNebula(DShapes shapes, PointF center, float radius, 
                                     Color color, int density, Random rng, int layers = 12)
        {
            // Aspect ratio stretches the cloud horizontally for a more natural look
            float xScale = 1.6f;
            float yScale = 1.0f;

            for (int i = 0; i < density; i++)
            {
                // Box-Muller transform — Gaussian distribution keeps the core denser
                double u1 = 1.0 - rng.NextDouble();
                double u2 = 1.0 - rng.NextDouble();
                double mag = Math.Sqrt(-2.0 * Math.Log(u1));
                double z0  = mag * Math.Cos(2.0 * Math.PI * u2);
                double z1  = mag * Math.Sin(2.0 * Math.PI * u2);

                float px = center.X + (float)(z0 * radius * 0.38 * xScale);
                float py = center.Y + (float)(z1 * radius * 0.38 * yScale);

                // Distance ratio [0..1] from centre — used to fade alpha at the edges
                float dx    = (px - center.X) / (radius * xScale);
                float dy    = (py - center.Y) / (radius * yScale);
                float dist  = Math.Min(1f, MathF.Sqrt(dx * dx + dy * dy));
                int   alpha = (int)(color.A == 0 ? 22 : color.A * (1f - dist * 0.75f));
                alpha = Math.Clamp(alpha, 4, 60);

                var pen = new Pen(Color.FromArgb(alpha, color.R, color.G, color.B), 1f);

                // Short segment in a random direction — length 1–4 px
                float segLen = 1f + (float)rng.NextDouble() * 3f;
                double angle = rng.NextDouble() * Math.PI * 2.0;
                float ex = px + (float)(Math.Cos(angle) * segLen);
                float ey = py + (float)(Math.Sin(angle) * segLen);

                shapes.Add(new PointF(px, py), new PointF(ex, ey), pen);
            }

            //// Second pass: bright compact core glow — simulates an emission hotspot
            //float coreR = radius * 0.25f;
            //for (int i = 0; i < 4; i++)
            //{
            //    float offX = (float)(rng.NextDouble() - 0.5) * radius * 0.12f;
            //    float offY = (float)(rng.NextDouble() - 0.5) * radius * 0.12f;

            //    float w = coreR * xScale * 2f;
            //    float h = coreR * yScale * 2f;

            //    int alpha = Math.Clamp((int)(color.A * 0.55f), 20, 120);

            //    using var brush = new SolidBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
            //    g.FillEllipse(brush, 
            //        center.X + offX - w / 2f, 
            //        center.Y + offY - h / 2f, 
            //        w, h);
            //}
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Comet
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Draws a static comet with a bright polygonal head and a fanned tail of
        /// fading rays.  The tail fans away from the direction of travel, exactly
        /// mirroring the "closed polygon" style of the HomeBase diamonds.
        /// </summary>
        /// <remarks>
        /// The <paramref name="direction"/> vector does not need to be normalised —
        /// it is normalised internally.  To aim the comet travelling toward the
        /// lower-right, pass <c>new PointF(1f, 0.5f)</c>.
        /// </remarks>
        /// <param name="shapes">Target shape collection.</param>
        /// <param name="head">Position of the comet's nucleus.</param>
        /// <param name="direction">
        ///   Direction of travel (tail points opposite to this).
        /// </param>
        /// <param name="tailLength">Length of the longest tail ray in pixels.</param>
        /// <param name="rays">
        ///   Number of tail rays (6–14 gives a good spread).
        /// </param>
        public static void AddComet(DShapes shapes, PointF head, PointF direction, float tailLength, int rays)
        {
            Color baseColor = Color.DarkGray;

            // ── Normalise direction ───────────────────────────────────────────
            float len = MathF.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
            if (len < float.Epsilon) len = 1f;
            float nx = direction.X / len;
            float ny = direction.Y / len;

            // ── Head — compact 4-point diamond ───────────────────────────────
            float hr = 4.0f;// 5f;   // head radius in pixels
            var headCoords = new float[]
            {
                head.X,        head.Y - hr,   // top
                head.X + hr,   head.Y,         // right
                head.X,        head.Y + hr,    // bottom
                head.X - hr,   head.Y          // left
            };

            // By changing both head radius above to 4.0f from 5.0f, and this to 4.0f from from 1.5f, the
            // head looks more rounded and less like a diamond, which better suits the comet aesthetic.
            // The slightly thicker pen also gives it more presence against the tail rays.
            var headPen = new Pen(Color.FromArgb(255, baseColor), 4.0f); 
            shapes.AddPolygonalShapes(new List<float[]> { headCoords }, headPen);

            // Bright centre dot
            shapes.Add(new PointF(head.X - 1f, head.Y - 1f),
                        new PointF(head.X + 1f, head.Y + 1f),
                        new Pen(Color.FromArgb(220, baseColor), 2f));

            // ── Tail — fan of rays opposite to direction ──────────────────────
            // Fan half-angle: wider spread for more rays, narrower for a tight jet
            float halfAngle = MathF.PI * 0.18f + (rays - 6) * 0.012f;
            halfAngle = Math.Clamp(halfAngle, 0.1f, MathF.PI * 0.35f);

            // Base tail direction is exactly opposite travel direction
            double tailBaseAngle = Math.Atan2(-ny, -nx);

            for (int i = 0; i < rays; i++)
            {
                // Spread rays evenly across the fan
                float t = rays > 1 ? (float)i / (rays - 1) : 0.5f;   // 0..1
                double rayAngle = tailBaseAngle - halfAngle + t * halfAngle * 2.0;

                // Centre rays are longer; edge rays shorter — natural comet taper
                float edgeFactor = 1f - MathF.Abs(t - 0.5f) * 1.0f;   // 0.5 at edges, 1.0 at centre
                float rayLen = tailLength * (0.4f + 0.6f * edgeFactor);

                float ex = head.X + (float)(Math.Cos(rayAngle) * rayLen);
                float ey = head.Y + (float)(Math.Sin(rayAngle) * rayLen);

                // Alpha fades along the ray and also toward the fan edges
                int alpha = (int)(160 * edgeFactor);
                alpha = Math.Clamp(alpha, 20, 160);

                var tailPen = new Pen(Color.FromArgb(alpha, baseColor), 1f);
                shapes.Add(new PointF(head.X, head.Y), new PointF(ex, ey), tailPen);

                // Secondary inner ray — half length, softer, fills in the core glow
                float ex2 = head.X + (float)(Math.Cos(rayAngle) * rayLen * 0.45f);
                float ey2 = head.Y + (float)(Math.Sin(rayAngle) * rayLen * 0.45f);
                var innerPen = new Pen(Color.FromArgb(Math.Clamp(alpha + 40, 40, 200), baseColor), 1f);
                shapes.Add(new PointF(head.X, head.Y), new PointF(ex2, ey2), innerPen);
            }
        }
    }
}
