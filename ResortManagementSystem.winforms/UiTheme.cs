using System.Drawing;
using System.Windows.Forms;

namespace ResortManagementSystem.Winforms
{
    internal static class UiTheme
    {
        // ---------------- Ocean theme ----------------
        // Sidebar: a deep-blue to teal gradient. Nav rows are translucent white so the gradient shows through.
        public static readonly Color SidebarTop = Color.FromArgb(0x0A, 0x2E, 0x4D);
        public static Color SidebarBottom { get; private set; } = Color.FromArgb(0x0E, 0x6E, 0x7E);
        public static readonly Color SidebarBg = Color.FromArgb(0x0C, 0x4A, 0x66);          // mid tone, used for text on white chips
        public static readonly Color SidebarActive = Color.FromArgb(60, 255, 255, 255);
        public static readonly Color SidebarHoverBg = Color.FromArgb(30, 255, 255, 255);
        public static readonly Color SidebarText = Color.FromArgb(0xDC, 0xEF, 0xF3);
        public static readonly Color SidebarHeader = Color.FromArgb(0x8F, 0xC3, 0xCF);
        public static readonly Color SidebarBorder = Color.FromArgb(50, 255, 255, 255);
        public static readonly Color SidebarActiveText = Color.White;

        // Accent colors. Accent is a property so a resort can have its own brand color (see SetAccent).
        public static Color Accent { get; private set; } = Color.FromArgb(0x0F, 0x8B, 0x8D);
        public static Color AccentDark { get; private set; } = Color.FromArgb(0x0B, 0x6B, 0x6E);
        public static readonly Color AccentCoral = Color.FromArgb(0xFF, 0x7A, 0x59);
        public static readonly Color AccentAmber = Color.FromArgb(0xF2, 0xA3, 0x3A);
        public static readonly Color ContentBg = Color.FromArgb(0xF7, 0xF3, 0xEA);          // warm sand
        public static readonly Color CardBg = Color.White;
        public static readonly Color BorderGray = Color.FromArgb(0xE8, 0xE1, 0xD3);
        public static readonly Color TextDark = Color.FromArgb(0x17, 0x32, 0x3D);
        public static readonly Color TextMuted = Color.FromArgb(0x6B, 0x7B, 0x80);
        public static readonly Color Danger = Color.FromArgb(0xD6, 0x45, 0x45);
        public static Color GridSelection { get; private set; } = Color.FromArgb(0x0E, 0x6E, 0x7E);

        // Grid row colors
        public static readonly Color RowAlt = Color.FromArgb(0xFB, 0xF8, 0xF2);
        public static readonly Color RowHover = Color.FromArgb(0xE3, 0xF2, 0xF1);

        public static readonly Color ChartGreen = Color.FromArgb(0x1F, 0xA7, 0x9A);
        public static readonly Color ChartTeal = Color.FromArgb(0x2A, 0x9D, 0xB5);
        public static readonly Color ChartAmber = Color.FromArgb(0xF4, 0xA2, 0x61);
        public static readonly Color ChartRed = Color.FromArgb(0xE7, 0x6F, 0x51);
        public static readonly Color ChartBlue = Color.FromArgb(0x3D, 0x6F, 0x9E);
        public static readonly Color ChartGray = Color.FromArgb(0x8D, 0x99, 0x96);

        // Lets a resort use its own brand color. Call once, after login and before the screens are built.
        public static void SetAccent(Color accent)
        {
            Accent = accent;
            AccentDark = Color.FromArgb(
                (int)(accent.R * 0.78), (int)(accent.G * 0.78), (int)(accent.B * 0.78));
            SidebarBottom = AccentDark;
            GridSelection = AccentDark;
        }

        // Back to the default Ocean teal (Super Admin, or a resort with no brand color)
        public static void ResetAccent() => SetAccent(Color.FromArgb(0x0F, 0x8B, 0x8D));

        // "#0F8B8D" -> Color. Returns false for anything else.
        public static bool TryParseHex(string? hex, out Color color)
        {
            color = Color.Empty;
            if (string.IsNullOrWhiteSpace(hex)) return false;
            var h = hex.Trim().TrimStart('#');
            if (h.Length != 6 || !int.TryParse(h, System.Globalization.NumberStyles.HexNumber, null, out var rgb)) return false;
            color = Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
            return true;
        }

        public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        public static readonly (Color Bg, Color Fg) BadgeGreen = (Color.FromArgb(0xE1, 0xF3, 0xEB), Color.FromArgb(0x1F, 0x6B, 0x51));
        public static readonly (Color Bg, Color Fg) BadgeBlue = (Color.FromArgb(0xE1, 0xF0, 0xFC), Color.FromArgb(0x1D, 0x6F, 0xB8));
        public static readonly (Color Bg, Color Fg) BadgeAmber = (Color.FromArgb(0xFD, 0xF0, 0xDA), Color.FromArgb(0xB0, 0x71, 0x0A));
        public static readonly (Color Bg, Color Fg) BadgeRed = (Color.FromArgb(0xFC, 0xE4, 0xE4), Color.FromArgb(0xC4, 0x2B, 0x2B));
        public static readonly (Color Bg, Color Fg) BadgeGray = (Color.FromArgb(0xEE, 0xF0, 0xF2), Color.FromArgb(0x6B, 0x76, 0x85));

        public const int LabelWidth = 115;
        public const int FieldWidth = 250;
        public const int ColGap = 60;

        public static Panel BuildTopBar(string title, string subtitle, out Label clockLabel)
        {
            var bar = new Panel { Dock = DockStyle.Top, Height = 80, Padding = new Padding(0, 0, 0, 12) };

            var titleLabel = new Label { Text = title, Font = new Font("Segoe UI", 18f, FontStyle.Bold), ForeColor = TextDark, AutoSize = true, Left = 0, Top = 0, UseMnemonic = false };
            var subtitleLabel = new Label { Text = subtitle, Font = new Font("Segoe UI", 9.5f), ForeColor = TextMuted, AutoSize = true, Left = 2, UseMnemonic = false };
            subtitleLabel.Top = titleLabel.Top + titleLabel.PreferredHeight + 2;

            clockLabel = new Label
            {
                Text = DateTime.Now.ToString("dddd, MMMM d  ·  h:mm:ss tt"),
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 9f),
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Left = 900,
                Top = 8
            };

            bar.Controls.AddRange(new Control[] { titleLabel, subtitleLabel, clockLabel });
            return bar;
        }

        public static Label MakeChip(string text, Color back, Color fore)
        {
            return new Label
            {
                Text = text,
                BackColor = back,
                ForeColor = fore,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                AutoSize = true,
                UseMnemonic = false,
                Padding = new Padding(8, 3, 8, 3)
            };
        }

        public static TableLayoutPanel MakeStatsRow(params (string label, Color accent, string subtext)[] stats)
        {
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = stats.Length,
                RowCount = 1,
                Margin = new Padding(0),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            for (int i = 0; i < stats.Length; i++)
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / stats.Length));

            foreach (var (label, accent, subtext) in stats)
            {
                var card = MakeStatCardFill(label, accent, subtext, out var valueLabel);
                table.Controls.Add(card);
                card.Tag = valueLabel;
            }
            return table;
        }

        private static Panel MakeStatCardFill(string label, Color accent, string subtext, out Label valueLabel)
        {
            var card = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 16, 0), BackColor = CardBg, BorderStyle = BorderStyle.None, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            var stripe = new Panel { Dock = DockStyle.Left, Width = 5, BackColor = accent };

            var inner = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(20, 14, 12, 16),
                Margin = new Padding(0)
            };
            inner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            inner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            inner.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lbl = new Label { Text = label.ToUpper(), ForeColor = TextMuted, Font = new Font("Segoe UI", 7.75f, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 0, 8), UseMnemonic = false };
            valueLabel = new Label { Text = "0", ForeColor = TextDark, Font = new Font("Segoe UI", 20f, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 0, 8), UseMnemonic = false };
            var sub = new Label { Text = subtext, ForeColor = TextMuted, Font = new Font("Segoe UI", 7.75f), AutoSize = true, Margin = new Padding(0), UseMnemonic = false };

            inner.Controls.Add(lbl, 0, 0);
            inner.Controls.Add(valueLabel, 0, 1);
            inner.Controls.Add(sub, 0, 2);

            card.Controls.Add(inner);
            card.Controls.Add(stripe);
            return card;
        }

        public static Label MakeFieldLabel(string text, int left, int top) => new()
        {
            Text = text,
            Left = left,
            Top = top,
            Width = LabelWidth,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = TextMuted,
            UseMnemonic = false
        };

        public static TextBox MakeTextBox(int left, int top, int width = FieldWidth) => new()
        {
            Left = left,
            Top = top - 3,
            Width = width,
            Height = 26,
            BorderStyle = BorderStyle.FixedSingle
        };

        public static ComboBox MakeComboBox(int left, int top, int width = FieldWidth) => new()
        {
            Left = left,
            Top = top - 3,
            Width = width,
            Height = 26,
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        public static Button MakePillButton(string text, Color back, Color fore, int left, int top, int width = 150)
        {
            var b = new Button
            {
                Text = text,
                Left = left,
                Top = top,
                Width = width,
                Height = 34,
                BackColor = back,
                ForeColor = fore,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            b.FlatAppearance.BorderSize = back == CardBg ? 1 : 0;
            b.FlatAppearance.BorderColor = BorderGray;
            return b;
        }

        public static void StyleGrid(DataGridView grid)
        {
            grid.ReadOnly = true;
            grid.AutoGenerateColumns = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.AllowUserToAddRows = false;
            grid.RowHeadersVisible = false;
            grid.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            grid.RowHeadersWidth = 4;
            grid.BackgroundColor = CardBg;
            grid.BorderStyle = BorderStyle.None;
            grid.GridColor = BorderGray;
            grid.Font = new Font("Segoe UI", 9.5f);
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.White,
                ForeColor = TextDark,
                SelectionBackColor = Color.White,
                SelectionForeColor = TextDark,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(8, 4, 0, 4)
            };
            grid.ColumnHeadersHeight = 36;
            grid.DefaultCellStyle = new DataGridViewCellStyle
            {
                Padding = new Padding(8, 3, 0, 3),
                SelectionBackColor = GridSelection,
                SelectionForeColor = Color.White
            };

            // Alternating row shading makes long lists easier to scan
            grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = RowAlt,
                Padding = new Padding(8, 3, 0, 3),
                SelectionBackColor = GridSelection,
                SelectionForeColor = Color.White
            };

            grid.EnableHeadersVisualStyles = false;
            grid.RowTemplate.Height = 36;

            // Hover highlight (a row-level color overrides the alternating shade, Empty restores it)
            grid.CellMouseEnter += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < grid.Rows.Count)
                    grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = RowHover;
            };
            grid.CellMouseLeave += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < grid.Rows.Count)
                    grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.Empty;
            };

            // Friendly empty state instead of a blank white box
            grid.Paint += (s, e) =>
            {
                if (grid.Rows.Count > 0) return;

                var area = new Rectangle(0, grid.ColumnHeadersHeight, grid.Width,
                    Math.Max(0, grid.Height - grid.ColumnHeadersHeight));
                using var font = new Font("Segoe UI", 10f, FontStyle.Italic);
                TextRenderer.DrawText(e.Graphics, "No records to show yet.", font, area, TextMuted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
        }

        public static void AddTextColumn(DataGridView grid, string propertyName, string headerText, int fillWeight = 100)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = propertyName,
                HeaderText = headerText,
                Name = propertyName,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = fillWeight
            });
        }

        public static void MakeBadgeColumn(DataGridView grid, string columnName,
            Func<string, (Color Bg, Color Fg)> mapFunc, Func<string, string>? displayFunc = null)
        {
            grid.CellPainting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (grid.Columns[e.ColumnIndex].Name != columnName) return;

                var raw = e.Value?.ToString() ?? "";
                if (string.IsNullOrEmpty(raw)) return;

                var value = displayFunc != null ? displayFunc(raw) : raw;
                var (bg, fg) = mapFunc(raw);

                e.PaintBackground(e.CellBounds, true);

                var textSize = TextRenderer.MeasureText(value, grid.Font);
                var pillWidth = Math.Min(textSize.Width + 24, e.CellBounds.Width - 12);
                var pillHeight = 22;
                var pillX = e.CellBounds.X + 8;
                var pillY = e.CellBounds.Y + (e.CellBounds.Height - pillHeight) / 2;

                using var brush = new SolidBrush(bg);
                using var path = RoundedRect(new Rectangle(pillX, pillY, pillWidth, pillHeight), 10);
                e.Graphics!.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.FillPath(brush, path);

                TextRenderer.DrawText(e.Graphics, value, grid.Font, new Rectangle(pillX, pillY, pillWidth, pillHeight), fg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                e.Handled = true;
            };
        }

        // Yes / No pill instead of raw True / False
        public static void MakeYesNoColumn(DataGridView grid, string columnName,
            (Color Bg, Color Fg) yesColor, (Color Bg, Color Fg) noColor)
        {
            MakeBadgeColumn(grid, columnName,
                v => v == "True" ? yesColor : noColor,
                v => v == "True" ? "Yes" : "No");
        }

        // Search / filter bar with "+ New" and "Refresh" docked on the right
        public static Panel MakeToolbar(string newButtonText, out Button btnNew, out Button btnRefresh)
        {
            var bar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = ContentBg };
            var right = new Panel { Dock = DockStyle.Right, Width = 300, BackColor = ContentBg };
            btnRefresh = MakePillButton("Refresh", CardBg, TextDark, 0, 8, 110);
            btnNew = MakePillButton(newButtonText, Accent, Color.White, 120, 8, 180);
            right.Controls.Add(btnRefresh);
            right.Controls.Add(btnNew);
            bar.Controls.Add(right);

            // Lay the search / filter controls out left to right using their real widths,
            // so labels never overlap the boxes next to them (fonts are wider on some screens)
            bar.HandleCreated += (s, e) => FlowToolbar(bar);
            return bar;
        }

        public static void FlowToolbar(Panel bar)
        {
            int x = 0;
            foreach (var c in bar.Controls.Cast<Control>()
                         .Where(c => c.Dock == DockStyle.None).OrderBy(c => c.Left).ToList())
            {
                c.Left = x;
                x += c.Width + (c is Label ? 8 : 22);
            }
        }

        public static Label MakeToolbarLabel(string text, int left) => new()
        {
            Text = text,
            Left = left,
            Top = 17,
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = TextMuted,
            UseMnemonic = false
        };

        public static TextBox MakeSearchBox(int left, int width) => new()
        {
            Left = left,
            Top = 13,
            Width = width,
            Height = 26,
            BorderStyle = BorderStyle.FixedSingle
        };

        public static ComboBox MakeFilterCombo(int left, int width, params string[] items)
        {
            var combo = new ComboBox { Left = left, Top = 13, Width = width, DropDownStyle = ComboBoxStyle.DropDownList };
            combo.Items.AddRange(items);
            combo.SelectedIndex = 0;
            return combo;
        }

        // A form card that starts hidden; the screen opens it for "New" or "Edit"
        public static Panel MakeFormCard(int height, out Label titleLabel)
        {
            var card = MakeCard(0, height);
            card.Dock = DockStyle.Top;
            card.Visible = false;
            titleLabel = new Label
            {
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = TextDark,
                Left = 24,
                Top = 18,
                AutoSize = true,
                UseMnemonic = false
            };
            card.Controls.Add(titleLabel);
            return card;
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Panel MakeCard(int top, int height) => new()
        {
            Left = 0,
            Top = top,
            Width = 1200,
            Height = height,
            BackColor = CardBg,
            BorderStyle = BorderStyle.None,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        public static Panel MakeStatCard(string label, Color accent, string subtext, out Label valueLabel)
            => MakeStatCardFill(label, accent, subtext, out valueLabel);

        public static TableLayoutPanel MakeEqualRow(params Control[] cards)
        {
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 150,
                ColumnCount = cards.Length,
                RowCount = 1,
                Margin = new Padding(0)
            };
            for (int i = 0; i < cards.Length; i++)
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / cards.Length));

            foreach (var c in cards)
            {
                c.Dock = DockStyle.Fill;
                c.Margin = new Padding(0, 0, 16, 0);
                table.Controls.Add(c);
            }
            return table;
        }

        public static Panel MakeActionCard(string title, params (string Text, Color Back, Color Fore, Action OnClick)[] actions)
        {
            var card = new Panel { BackColor = CardBg, BorderStyle = BorderStyle.None };
            var titleLabel = new Label { Text = title, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = TextMuted, Left = 20, Top = 16, AutoSize = true, UseMnemonic = false };
            card.Controls.Add(titleLabel);

            int top = 48;
            foreach (var action in actions)
            {
                var btn = MakePillButton(action.Text, action.Back, action.Fore, 20, top, 250);
                btn.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                var localAction = action.OnClick;
                btn.Click += (s, e) => localAction();
                card.Controls.Add(btn);
                top += 44;
            }
            return card;
        }

        public static Panel MakeAlertListCard(string headerText, Color headerBg, Color headerFg,
            IEnumerable<(string MainText, string BadgeText, (Color Bg, Color Fg) Badge)> items)
        {
            var card = new Panel { BackColor = CardBg, BorderStyle = BorderStyle.None };

            var header = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = headerBg };
            var headerLabel = new Label { Text = headerText, ForeColor = headerFg, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Left = 16, Top = 10, AutoSize = true, UseMnemonic = false };
            header.Controls.Add(headerLabel);

            var list = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            int top = 4;
            bool any = false;

            foreach (var item in items)
            {
                any = true;
                var row = new Panel { Left = 0, Top = top, Height = 40, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Width = 600 };

                var mainLabel = new Label { Text = item.MainText, ForeColor = TextDark, Font = new Font("Segoe UI", 9.5f), Left = 16, Top = 10, AutoSize = true, UseMnemonic = false };

                var badge = new Label
                {
                    Text = item.BadgeText,
                    ForeColor = item.Badge.Fg,
                    BackColor = item.Badge.Bg,
                    Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                    AutoSize = true,
                    UseMnemonic = false,
                    Padding = new Padding(10, 3, 10, 3),
                    Top = 8
                };
                row.Resize += (s, e) => badge.Left = row.Width - badge.Width - 16;

                var divider = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = BorderGray };

                row.Controls.AddRange(new Control[] { mainLabel, badge, divider });
                list.Controls.Add(row);
                top += 40;
            }

            if (!any)
            {
                var emptyLabel = new Label { Text = "Nothing to show right now.", ForeColor = TextMuted, Font = new Font("Segoe UI", 9f, FontStyle.Italic), Left = 16, Top = 16, AutoSize = true };
                list.Controls.Add(emptyLabel);
            }

            card.Controls.Add(list);
            card.Controls.Add(header);
            return card;
        }

        // ------------------------------------------------------------------
        // Greeting banner for dashboards: a rounded ocean gradient with soft waves
        // ------------------------------------------------------------------
        public static Panel MakeBanner(string title, string subtitle)
        {
            var banner = new BannerPanel(title, subtitle) { Dock = DockStyle.Top, Height = 118 };
            return banner;
        }

        private sealed class BannerPanel : Panel
        {
            private readonly string _title, _subtitle;
            public BannerPanel(string title, string subtitle)
            {
                _title = title; _subtitle = subtitle;
                DoubleBuffered = true;
                BackColor = ContentBg;
                ResizeRedraw = true;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, Width - 1, Height - 16);
                if (rect.Width < 10 || rect.Height < 10) return;

                using var path = RoundedRect(rect, 14);
                using (var grad = new System.Drawing.Drawing2D.LinearGradientBrush(rect, SidebarTop, Accent, 0f))
                    g.FillPath(grad, path);

                // Soft decorative circles, clipped to the banner
                var state = g.Save();
                g.SetClip(path);
                using (var b1 = new SolidBrush(Color.FromArgb(28, 255, 255, 255)))
                    g.FillEllipse(b1, rect.Right - 260, rect.Top - 90, 320, 320);
                using (var b2 = new SolidBrush(Color.FromArgb(40, AccentCoral)))
                    g.FillEllipse(b2, rect.Right - 120, rect.Bottom - 70, 190, 190);
                using (var b3 = new SolidBrush(Color.FromArgb(18, 255, 255, 255)))
                    g.FillEllipse(b3, rect.Right - 420, rect.Bottom - 50, 200, 200);
                g.Restore(state);

                using var fTitle = new Font("Segoe UI", 20f, FontStyle.Bold);
                using var fSub = new Font("Segoe UI", 10f);
                TextRenderer.DrawText(g, _title, fTitle, new Point(28, 18), Color.White);
                TextRenderer.DrawText(g, _subtitle, fSub, new Point(30, 62), Color.FromArgb(0xDC, 0xEF, 0xF3));
                TextRenderer.DrawText(g, DateTime.Now.ToString("dddd, MMMM d, yyyy"), fSub,
                    new Point(30, 82), Color.FromArgb(0xFF, 0xC9, 0xB8));
            }
        }

        // Panel that paints the sidebar gradient (nav rows on top are translucent)
        public sealed class GradientPanel : Panel
        {
            public GradientPanel()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
                SetStyle(ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;      // children inherit "transparent" and show the gradient
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                if (Width <= 0 || Height <= 0) return;
                using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    new Rectangle(0, 0, Width, Height), SidebarTop, SidebarBottom, 90f);
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
        }
    }
}