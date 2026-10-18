using System.Drawing;
using System.Net.Http.Json;
using static ResortManagementSystem.Winforms.UiTheme;

namespace ResortManagementSystem.Winforms
{
    // Shows every promo email: Pending, Sent or Failed. Refreshes by itself while emails are still being sent.
    public class EmailLogForm : Form
    {
        private readonly HttpClient _http;
        private readonly int _companyId;
        private readonly int? _promotionId;

        private readonly DataGridView _grid = new() { Dock = DockStyle.Fill };
        private readonly Pager _pager;
        private readonly Label _summary = new();
        private readonly System.Windows.Forms.Timer _timer = new() { Interval = 3000 };
        private bool _loading;

        private class LogRow
        {
            public string Created { get; set; } = "";
            public string ToEmail { get; set; } = "";
            public string ToName { get; set; } = "";
            public string Status { get; set; } = "";
            public int Attempts { get; set; }
            public string Detail { get; set; } = "";
        }

        private class LogDto
        {
            public string ToEmail { get; set; } = "";
            public string ToName { get; set; } = "";
            public string Status { get; set; } = "";
            public int Attempts { get; set; }
            public string? LastError { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? SentAt { get; set; }
        }

        public EmailLogForm(HttpClient http, int companyId, int? promotionId, string title)
        {
            _http = http;
            _companyId = companyId;
            _promotionId = promotionId;

            Text = title;
            ClientSize = new Size(1020, 600);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = ContentBg;
            Font = new Font("Segoe UI", 9.5f);
            Padding = new Padding(20);

            var top = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = ContentBg };
            var heading = new Label { Text = title, Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = TextDark, AutoSize = true, Left = 0, Top = 2, UseMnemonic = false };
            _summary.AutoSize = true; _summary.Left = 2; _summary.Top = 32; _summary.ForeColor = TextMuted; _summary.UseMnemonic = false;
            var btnRetry = MakePillButton("Retry failed", AccentCoral, Color.White, 0, 8, 130);
            var btnRefresh = MakePillButton("Refresh", CardBg, TextDark, 0, 8, 110);
            var right = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 270, FlowDirection = FlowDirection.RightToLeft, BackColor = ContentBg };
            right.Controls.Add(btnRetry);
            right.Controls.Add(btnRefresh);
            top.Controls.Add(right);
            top.Controls.Add(heading);
            top.Controls.Add(_summary);

            StyleGrid(_grid);
            AddTextColumn(_grid, nameof(LogRow.Created), "Queued", 110);
            AddTextColumn(_grid, nameof(LogRow.ToEmail), "Email", 190);
            AddTextColumn(_grid, nameof(LogRow.ToName), "Guest", 130);
            AddTextColumn(_grid, nameof(LogRow.Status), "Status", 80);
            AddTextColumn(_grid, nameof(LogRow.Attempts), "Tries", 50);
            AddTextColumn(_grid, nameof(LogRow.Detail), "Detail", 230);
            MakeBadgeColumn(_grid, nameof(LogRow.Status), st => st switch
            {
                "Sent" => BadgeGreen,
                "Pending" => BadgeAmber,
                _ => BadgeRed
            });

            _pager = new Pager(_grid);

            Controls.Add(_grid);
            Controls.Add(_pager);
            Controls.Add(top);

            btnRefresh.Click += async (s, e) => await LoadAsync();
            btnRetry.Click += async (s, e) => await RetryAsync();
            _timer.Tick += async (s, e) => await LoadAsync();
            Shown += async (s, e) => { await LoadAsync(); _timer.Start(); };
            FormClosed += (s, e) => _timer.Dispose();
        }

        private List<LogRow> _rows = new();

        private async Task LoadAsync()
        {
            if (_loading || IsDisposed) return;
            _loading = true;
            try
            {
                var url = $"/tenant/{_companyId}/email-log" + (_promotionId is null ? "" : $"?promotionId={_promotionId}");
                var data = await _http.GetFromJsonAsync<List<LogDto>>(url) ?? new();

                _rows = data.Select(d => new LogRow
                {
                    Created = d.CreatedAt.ToLocalTime().ToString("MM/dd h:mm tt"),
                    ToEmail = d.ToEmail,
                    ToName = d.ToName,
                    Status = d.Status,
                    Attempts = d.Attempts,
                    Detail = d.Status == "Sent" ? $"Sent {d.SentAt?.ToLocalTime():MM/dd h:mm tt}" : d.LastError ?? ""
                }).ToList();

                int sent = data.Count(d => d.Status == "Sent"), pending = data.Count(d => d.Status == "Pending"), failed = data.Count(d => d.Status == "Failed");
                _summary.Text = $"Sent {sent}   ·   Pending {pending}   ·   Failed {failed}";

                _pager.Bind(_rows);
                if (pending == 0) _timer.Stop(); else if (!_timer.Enabled) _timer.Start();
            }
            catch (Exception ex)
            {
                _summary.Text = "Could not load the email log: " + ex.Message;
                _timer.Stop();
            }
            finally { _loading = false; }
        }

        private async Task RetryAsync()
        {
            try
            {
                using var r = await _http.PostAsync($"/tenant/{_companyId}/email/retry", null);
                if (!r.IsSuccessStatusCode) { MessageBox.Show(await r.Content.ReadAsStringAsync()); return; }
                _timer.Start();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not reach the server: {ex.Message}", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}