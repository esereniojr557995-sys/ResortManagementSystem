using System.Drawing;
using static ResortManagementSystem.Winforms.UiTheme;

namespace ResortManagementSystem.Winforms
{
    // Reusable pager that sits under a DataGridView. Give it the full (already filtered)
    // list with Bind(); it shows one page at a time so nobody has to scroll a long list.
    //
    //   _pager = new Pager(dgv);
    //   Controls.Add(dgv);  Controls.Add(_pager);  ...      (Fill control first, pager after it)
    //   _pager.Bind(filteredList);                           (instead of dgv.DataSource = ...)
    //   call _pager.ResetPage() when the user changes a search/filter box
    internal sealed class Pager : Panel
    {
        private readonly DataGridView _grid;
        private readonly Button _first, _prev, _next, _last;
        private readonly Label _info, _pageLabel;
        private readonly ComboBox _size;

        private Func<int, int, object>? _slice;   // (skip, take) -> list to show
        private int _total;
        private int _page = 1;
        private int _lastAutoSize = -1;

        public Pager(DataGridView grid)
        {
            _grid = grid;
            Dock = DockStyle.Bottom;
            Height = 48;
            BackColor = ContentBg;
            Padding = new Padding(0, 8, 0, 0);

            _info = new Label { AutoSize = true, ForeColor = TextMuted, Font = new Font("Segoe UI", 9f), Left = 4, Top = 16, UseMnemonic = false };

            _first = MakeNavButton("«");
            _prev = MakeNavButton("‹");
            _pageLabel = new Label { AutoSize = false, Width = 110, Height = 28, TextAlign = ContentAlignment.MiddleCenter, ForeColor = TextDark, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), UseMnemonic = false };
            _next = MakeNavButton("›");
            _last = MakeNavButton("»");

            var sizeLabel = new Label { Text = "Rows per page", AutoSize = true, ForeColor = TextMuted, Font = new Font("Segoe UI", 9f), UseMnemonic = false };
            _size = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70 };
            _size.Items.AddRange(new object[] { "Auto", "10", "25", "50" });
            _size.SelectedIndex = 0;

            Controls.AddRange(new Control[] { _info, _first, _prev, _pageLabel, _next, _last, sizeLabel, _size });

            // Position the right-hand cluster; it follows the right edge on resize
            void PlaceRight()
            {
                int right = Width - 4;
                _size.Left = right - _size.Width; _size.Top = 10;
                sizeLabel.Left = _size.Left - sizeLabel.Width - 8; sizeLabel.Top = 15;
                int x = sizeLabel.Left - 24;
                x -= _last.Width; _last.Left = x; _last.Top = 8;
                x -= _next.Width + 2; _next.Left = x; _next.Top = 8;
                x -= _pageLabel.Width + 2; _pageLabel.Left = x; _pageLabel.Top = 8;
                x -= _prev.Width + 2; _prev.Left = x; _prev.Top = 8;
                x -= _first.Width + 2; _first.Left = x; _first.Top = 8;
            }
            Resize += (s, e) => PlaceRight();
            PlaceRight();

            _first.Click += (s, e) => GoTo(1);
            _prev.Click += (s, e) => GoTo(_page - 1);
            _next.Click += (s, e) => GoTo(_page + 1);
            _last.Click += (s, e) => GoTo(PageCount);
            _size.SelectedIndexChanged += (s, e) => { _page = 1; Render(); };

            // "Auto" fits as many rows as the grid can show without a scroll bar
            _grid.SizeChanged += (s, e) =>
            {
                if (!IsAuto) return;
                int fit = AutoRows();
                if (fit != _lastAutoSize) Render();
            };
        }

        private static Button MakeNavButton(string text)
        {
            var b = new Button
            {
                Text = text,
                Width = 34,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = CardBg,
                ForeColor = TextDark,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            b.FlatAppearance.BorderColor = BorderGray;
            return b;
        }

        private bool IsAuto => (_size.SelectedItem?.ToString() ?? "Auto") == "Auto";

        private int AutoRows()
        {
            int usable = _grid.ClientSize.Height - _grid.ColumnHeadersHeight;
            return Math.Max(5, usable / Math.Max(1, _grid.RowTemplate.Height));
        }

        public int PageSize => IsAuto ? AutoRows() : int.Parse(_size.SelectedItem!.ToString()!);
        private int PageCount => Math.Max(1, (int)Math.Ceiling(_total / (double)Math.Max(1, PageSize)));

        // Go back to page 1 (call when a search or filter changes)
        public void ResetPage() => _page = 1;

        // Give the pager the full list. Keeps the current page when it still exists.
        public void Bind<T>(IReadOnlyList<T> items)
        {
            _total = items.Count;
            _slice = (skip, take) => items.Skip(skip).Take(take).ToList();
            Render();
        }

        private void GoTo(int page)
        {
            _page = Math.Max(1, Math.Min(page, PageCount));
            Render();
        }

        private void Render()
        {
            if (_slice is null) return;

            int size = PageSize;
            _lastAutoSize = IsAuto ? size : -1;
            if (_page > PageCount) _page = PageCount;
            if (_page < 1) _page = 1;

            int skip = (_page - 1) * size;
            _grid.DataSource = _slice(skip, size);
            _grid.ClearSelection();

            int from = _total == 0 ? 0 : skip + 1;
            int to = Math.Min(skip + size, _total);
            _info.Text = _total == 0 ? "No records" : $"Showing {from}–{to} of {_total}";
            _pageLabel.Text = $"Page {_page} of {PageCount}";

            _first.Enabled = _prev.Enabled = _page > 1;
            _next.Enabled = _last.Enabled = _page < PageCount;
        }
    }
}