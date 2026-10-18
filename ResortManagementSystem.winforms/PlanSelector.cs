using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using static ResortManagementSystem.Winforms.UiTheme;

namespace ResortManagementSystem.Winforms
{
    public class PlanOption
    {
        public string Name { get; set; } = "";          // must match the tier value sent to the API
        public string Price { get; set; } = "";
        public string Period { get; set; } = "/ month";
        public string Tagline { get; set; } = "";
        public bool Popular { get; set; }
        public Color From { get; set; }
        public Color To { get; set; }
        public string[] Inclusions { get; set; } = Array.Empty<string>();
    }

    /// <summary>Netflix-style selectable plan cards.</summary>
    public class PlanSelector : Control
    {
        // >>> EDIT THESE: prices and inclusions are placeholders <<<
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<PlanOption> Plans { get; } = new()
        {
            new PlanOption
            {
                Name = "Micro", Price = "₱999", Tagline = "Basic · small resorts",
                From = ColorTranslator.FromHtml("#4B5563"), To = ColorTranslator.FromHtml("#9CA3AF"),
                Inclusions = new[]
                {
                    "1 property",
                    "Up to 5 staff users",
                    "Guest & booking records",
                    "Basic reports",
                    "Email support"
                }
            },
            new PlanOption
            {
                Name = "Small", Price = "₱2,499", Tagline = "Good · growing resorts",
                From = ColorTranslator.FromHtml("#047857"), To = ColorTranslator.FromHtml("#34D399"),
                Inclusions = new[]
                {
                    "1 property",
                    "Up to 20 staff users",
                    "Everything in Micro",
                    "Billing & invoicing",
                    "Housekeeping & maintenance",
                    "Chat & email support"
                }
            },
            new PlanOption
            {
                Name = "Medium", Price = "₱4,999", Tagline = "Best · full-service resorts", Popular = true,
                From = ColorTranslator.FromHtml("#1D4ED8"), To = ColorTranslator.FromHtml("#7C3AED"),
                Inclusions = new[]
                {
                    "Up to 3 properties",
                    "Unlimited staff users",
                    "Everything in Small",
                    "Advanced BI dashboards",
                    "Data export & API access",
                    "Priority support"
                }
            }
        };

        private int _selected = 0;
        private int _hover = -1;

        public event EventHandler? SelectedTierChanged;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string SelectedTier
        {
            get => Plans[_selected].Name;
            set
            {
                int i = Plans.FindIndex(p => p.Name.Equals(value, StringComparison.OrdinalIgnoreCase));
                if (i < 0 || i == _selected) return;
                _selected = i;
                Invalidate();
                SelectedTierChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public PlanSelector()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
            Height = 300;
        }

        private const int Gap = 16, MaxCardW = 360, TopPad = 14;

        private Rectangle CardRect(int i)
        {
            int n = Plans.Count;
            int w = Math.Min(MaxCardW, (Width - Gap * (n - 1)) / n);
            return new Rectangle(i * (w + Gap), TopPad, w, Height - TopPad - 2);
        }

        private int HitTest(Point p)
        {
            for (int i = 0; i < Plans.Count; i++)
                if (CardRect(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = HitTest(e.Location);
            if (h != _hover) { _hover = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = -1; Invalidate(); base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int h = HitTest(e.Location);
            if (h >= 0) SelectedTier = Plans[h].Name;
            base.OnMouseClick(e);
        }

        private static GraphicsPath Rounded(Rectangle r, int radius)
        {
            int d = radius * 2;
            var gp = new GraphicsPath();
            gp.AddArc(r.X, r.Y, d, d, 180, 90);
            gp.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            gp.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            gp.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            gp.CloseFigure();
            return gp;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(BackColor);

            using var fName = new Font("Segoe UI", 13f, FontStyle.Bold);
            using var fPrice = new Font("Segoe UI", 18f, FontStyle.Bold);
            using var fSmall = new Font("Segoe UI", 8.5f);
            using var fItem = new Font("Segoe UI", 9.5f);
            using var fTag = new Font("Segoe UI", 8f, FontStyle.Bold);
            using var fCheck = new Font("Segoe UI Symbol", 10f, FontStyle.Bold);
            using var itemFormat = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter };

            for (int i = 0; i < Plans.Count; i++)
            {
                var plan = Plans[i];
                var r = CardRect(i);
                bool sel = i == _selected;
                bool hov = i == _hover;
                int headerH = 92;

                // Card body
                using (var path = Rounded(r, 12))
                {
                    using var body = new SolidBrush(Color.White);
                    g.FillPath(body, path);

                    // Header band (clipped to the rounded card)
                    var state = g.Save();
                    g.SetClip(path);
                    using (var grad = new LinearGradientBrush(new Rectangle(r.X, r.Y, r.Width, headerH),
                                                              plan.From, plan.To, 45f))
                        g.FillRectangle(grad, r.X, r.Y, r.Width, headerH);
                    g.Restore(state);

                    using var pen = new Pen(sel ? Accent : (hov ? Color.FromArgb(150, 150, 150) : Color.FromArgb(210, 210, 210)),
                                            sel ? 3f : 1.2f);
                    g.DrawPath(pen, path);
                }

                // Header text
                g.DrawString(plan.Name, fName, Brushes.White, r.X + 16, r.Y + 12);
                g.DrawString(plan.Tagline, fSmall, Brushes.White, r.X + 16, r.Y + 36);
                g.DrawString(plan.Price, fPrice, Brushes.White, r.X + 14, r.Y + 54);
                var priceW = g.MeasureString(plan.Price, fPrice).Width;
                g.DrawString(plan.Period, fSmall, Brushes.White, r.X + 14 + priceW - 4, r.Y + 68);

                // "Most popular" tag: sized from its text so nothing is clipped
                if (plan.Popular)
                {
                    const string tagText = "MOST POPULAR";
                    int tagW = (int)Math.Ceiling(g.MeasureString(tagText, fTag).Width) + 22;
                    var tag = new Rectangle(r.X + 14, r.Y - 11, tagW, 22);
                    using var tp = Rounded(tag, 11);
                    using var tb = new SolidBrush(Color.FromArgb(229, 9, 20));
                    g.FillPath(tb, tp);
                    using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(tagText, fTag, Brushes.White, tag, sf);
                }

                // Selected check badge
                if (sel)
                {
                    var c = new Rectangle(r.Right - 38, r.Y + 12, 26, 26);
                    using var cb = new SolidBrush(Color.White);
                    using var ck = new SolidBrush(Accent);
                    g.FillEllipse(cb, c);
                    using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("✓", fCheck, ck, c, sf);
                }

                // Inclusions: row spacing shrinks a little if the card is short, so every item shows
                using var textBrush = new SolidBrush(TextDark);
                using var checkBrush = new SolidBrush(Accent);
                int y = r.Y + headerH + 14;
                g.DrawString("What's included", fTag, Brushes.Gray, r.X + 16, y);
                y += 22;

                int count = Math.Max(1, plan.Inclusions.Length);
                int step = Math.Min(26, Math.Max(18, (r.Bottom - 10 - y) / count));

                foreach (var item in plan.Inclusions)
                {
                    g.DrawString("✓", fCheck, checkBrush, r.X + 14, y - 2);
                    var textRect = new RectangleF(r.X + 38, y, r.Width - 38 - 12, step);
                    g.DrawString(item, fItem, textBrush, textRect, itemFormat);
                    y += step;
                }
            }
        }
    }
}