using ResortManagementSystem.Winforms;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net.Http.Json;
using static ResortManagementSystem.Winforms.UiTheme;

namespace ResortManagementSystem.Winforms
{
    public class LoginForm : Form
    {
        private TextBox txtEmail = null!, txtPassword = null!;
        private Label lblError = null!;
        private Label lblConnStatus = null!;
        private Button btnLogin = null!;
        private CheckBox chkShow = null!;

        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5226"),
            Timeout = TimeSpan.FromSeconds(20)   // don't let a slow/asleep cloud DB freeze the whole app
        };

        public UserRole LoggedInRole { get; private set; }
        public string LoggedInDisplayName { get; private set; } = "";
        public int SelectedCompanyId { get; private set; } = 0;   // 0 = platform-level (Super Admin)

        private enum ConnectionStatus { Checking, Offline, LocalOnly, Full }

        private class HealthStatusDto
        {
            public bool Master { get; set; }
            public bool? Tenant { get; set; }
        }

        private class LoginResult
        {
            public string Role { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public int CompanyId { get; set; }
        }

        // Remembers the last email typed so repeat sign-ins are quicker (the password is never saved)
        private static string LastEmailPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ResortCRM", "lastemail.txt");

        public LoginForm()
        {
            Text = "Resort CRM — Sign In";
            ClientSize = new Size(900, 580);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = CardBg;
            Font = new Font("Segoe UI", 9.5f);

            BuildUi();

            Load += async (s, e) => await RefreshConnectionStatusAsync();
        }

        // Left side: ocean gradient with the product name and selling points
        private sealed class BrandPanel : Panel
        {
            public BrandPanel() { DoubleBuffered = true; ResizeRedraw = true; }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var grad = new LinearGradientBrush(ClientRectangle, SidebarTop, Accent, 60f))
                    g.FillRectangle(grad, ClientRectangle);

                using (var b1 = new SolidBrush(Color.FromArgb(26, 255, 255, 255)))
                    g.FillEllipse(b1, -120, Height - 260, 420, 420);
                using (var b2 = new SolidBrush(Color.FromArgb(46, AccentCoral)))
                    g.FillEllipse(b2, Width - 190, -90, 300, 300);
                using (var b3 = new SolidBrush(Color.FromArgb(18, 255, 255, 255)))
                    g.FillEllipse(b3, Width - 150, Height - 170, 260, 260);

                using var fBrand = new Font("Segoe UI", 11f, FontStyle.Bold);
                using var fTitle = new Font("Segoe UI", 26f, FontStyle.Bold);
                using var fSub = new Font("Segoe UI", 11f);
                using var fItem = new Font("Segoe UI", 10.5f);

                TextRenderer.DrawText(g, "●  RESORT CRM", fBrand, new Point(44, 44), Color.FromArgb(0xFF, 0xC9, 0xB8));
                TextRenderer.DrawText(g, "Run your resort,", fTitle, new Point(40, 130), Color.White);
                TextRenderer.DrawText(g, "beautifully.", fTitle, new Point(40, 180), Color.White);
                TextRenderer.DrawText(g, "Guests, bookings, promotions and reports\nin one place.", fSub, new Point(44, 246), Color.FromArgb(0xDC, 0xEF, 0xF3));

                var items = new[] { "Guest profiles and reservations", "Promotions that reach your guests", "Reports you can export as PDF", "Each resort's data kept separate" };
                int y = 340;
                foreach (var item in items)
                {
                    using var dot = new SolidBrush(AccentCoral);
                    g.FillEllipse(dot, 46, y + 7, 8, 8);
                    TextRenderer.DrawText(g, item, fItem, new Point(64, y), Color.White);
                    y += 34;
                }
            }
        }

        private void BuildUi()
        {
            var brand = new BrandPanel { Dock = DockStyle.Left, Width = 400 };

            // Right side: a table that stacks the form top to bottom, so nothing can overlap at any DPI
            var form = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                Padding = new Padding(52, 36, 52, 20),
                BackColor = CardBg
            };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            void AddRow(Control c, SizeType type = SizeType.AutoSize, float height = 0)
            {
                form.RowStyles.Add(new RowStyle(type, height));
                form.Controls.Add(c, 0, form.RowCount++);
            }
            form.RowCount = 0;

            var title = new Label { Text = "Welcome back", Font = new Font("Segoe UI", 22f, FontStyle.Bold), ForeColor = TextDark, AutoSize = true, Margin = new Padding(0, 0, 0, 2), UseMnemonic = false };
            var subtitle = new Label { Text = "Sign in to continue to your resort.", Font = new Font("Segoe UI", 10.5f), ForeColor = TextMuted, AutoSize = true, Margin = new Padding(0, 0, 0, 10), UseMnemonic = false };

            lblConnStatus = new Label
            {
                Text = "●  Checking connection…",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = TextMuted,
                AutoSize = false,
                Height = 40,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 6),
                UseMnemonic = false
            };

            var lblEmail = new Label { Text = "Email", Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = TextMuted, AutoSize = true, Margin = new Padding(0, 0, 0, 4), UseMnemonic = false };
            txtEmail = new TextBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 11.5f), Margin = new Padding(0, 0, 0, 14), PlaceholderText = "you@resort.com" };

            var lblPassword = new Label { Text = "Password", Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = TextMuted, AutoSize = true, Margin = new Padding(0, 0, 0, 4), UseMnemonic = false };
            txtPassword = new TextBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 11.5f), UseSystemPasswordChar = true, Margin = new Padding(0, 0, 0, 6) };

            chkShow = new CheckBox { Text = "Show password", AutoSize = true, ForeColor = TextMuted, Margin = new Padding(0, 0, 0, 4) };
            chkShow.CheckedChanged += (s, e) => txtPassword.UseSystemPasswordChar = !chkShow.Checked;

            lblError = new Label
            {
                AutoSize = false,
                Height = 44,
                Dock = DockStyle.Fill,
                ForeColor = Danger,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 4),
                Text = "",
                UseMnemonic = false
            };

            btnLogin = MakePillButton("Sign In", Accent, Color.White, 0, 0, 100);
            btnLogin.Dock = DockStyle.Fill;
            btnLogin.Height = 48;
            btnLogin.Margin = new Padding(0, 0, 0, 12);
            btnLogin.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);

            var lnkChangePassword = new LinkLabel
            {
                Text = "Change password",
                AutoSize = true,
                LinkColor = Accent,
                ActiveLinkColor = AccentDark,
                Margin = new Padding(0, 0, 0, 0)
            };
            lnkChangePassword.Click += (s, e) =>
            {
                using var f = new ChangePasswordForm(_http, txtEmail.Text.Trim());
                f.ShowDialog(this);
            };

            AddRow(title);
            AddRow(subtitle);
            AddRow(lblConnStatus, SizeType.Absolute, 46);
            AddRow(lblEmail);
            AddRow(txtEmail);
            AddRow(lblPassword);
            AddRow(txtPassword);
            AddRow(chkShow);
            AddRow(lblError, SizeType.Absolute, 50);
            AddRow(btnLogin, SizeType.Absolute, 60);
            AddRow(lnkChangePassword);

            // Remember the last email
            try
            {
                if (File.Exists(LastEmailPath)) txtEmail.Text = File.ReadAllText(LastEmailPath).Trim();
            }
            catch { /* not important */ }

            // ==================== EVENTS ====================

            btnLogin.Click += async (s, e) => await AttemptLoginAsync();

            txtPassword.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;   // stops the "ding" and a double submit
                    await AttemptLoginAsync();
                }
            };

            Controls.Add(form);     // Fill first
            Controls.Add(brand);

            AcceptButton = btnLogin;
            Shown += (s, e) => (string.IsNullOrEmpty(txtEmail.Text) ? txtEmail : txtPassword).Focus();
        }

        // -------------------------------------------------- Connection status

        private async Task RefreshConnectionStatusAsync()
        {
            SetStatusLabel(ConnectionStatus.Checking);

            ConnectionStatus status = ConnectionStatus.Offline;
            for (int attempt = 1; attempt <= 10; attempt++)
            {
                status = await CheckConnectionAsync();
                if (status != ConnectionStatus.Offline) break;   // API answered, stop retrying

                if (IsDisposed) return;
                lblConnStatus.Text = $"●  Waiting for the API to start… ({attempt}/10)";
                lblConnStatus.ForeColor = TextMuted;
                await Task.Delay(4000);
            }

            if (!IsDisposed) SetStatusLabel(status);
        }

        private async Task<ConnectionStatus> CheckConnectionAsync()
        {
            try
            {
                // No company is chosen before login, so only the master database is checked.
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                using var response = await _http.GetAsync("/health/status", cts.Token);

                if (!response.IsSuccessStatusCode)
                    return ConnectionStatus.Offline;

                var result = await response.Content.ReadFromJsonAsync<HealthStatusDto>(cancellationToken: cts.Token);
                if (result is null) return ConnectionStatus.Offline;

                return result.Master ? ConnectionStatus.Full : ConnectionStatus.LocalOnly;
            }
            catch (TaskCanceledException)
            {
                // Local API answered but the cloud query took too long: slow/asleep cloud DB.
                return ConnectionStatus.LocalOnly;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Health check failed] {ex}");
                return ConnectionStatus.Offline;
            }
        }

        private void SetStatusLabel(ConnectionStatus status)
        {
            if (lblConnStatus is null || lblConnStatus.IsDisposed) return;

            (lblConnStatus.Text, lblConnStatus.ForeColor) = status switch
            {
                ConnectionStatus.Full => ("●  Connected (local + cloud)", Accent),
                ConnectionStatus.LocalOnly => ("●  Local only — cloud database unreachable", AccentAmber),
                ConnectionStatus.Offline => ("●  Offline — local API unreachable. Start the API and try again.", Danger),
                _ => ("●  Checking connection…", TextMuted)
            };
        }

        // -------------------------------------------------- Sign in

        private async Task AttemptLoginAsync()
        {
            if (!btnLogin.Enabled) return;   // ignore double clicks / Enter while a request is running

            lblError.Text = "";
            btnLogin.Enabled = false;
            try
            {
                if (string.IsNullOrWhiteSpace(txtEmail.Text) || string.IsNullOrEmpty(txtPassword.Text))
                {
                    lblError.Text = "Enter your email and password.";
                    return;
                }

                var status = await CheckConnectionAsync();
                SetStatusLabel(status);

                if (status == ConnectionStatus.Offline)
                {
                    lblError.Text = "Can't reach the server. Make sure the API is running.";
                    return;
                }
                if (status == ConnectionStatus.LocalOnly)
                {
                    lblError.Text = "Cloud database is slow right now. Trying to sign in anyway...";
                }

                using var response = await _http.PostAsJsonAsync("/auth/login", new
                {
                    email = txtEmail.Text.Trim(),
                    password = txtPassword.Text
                });

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    lblError.Text = "Invalid email or password.";
                    return;
                }
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    lblError.Text = "This company's subscription is inactive.";
                    try
                    {
                        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                        if (body != null && body.TryGetValue("message", out var msg) && !string.IsNullOrWhiteSpace(msg))
                            lblError.Text = msg;
                    }
                    catch { /* keep the default message */ }
                    return;
                }
                if (!response.IsSuccessStatusCode)
                {
                    lblError.Text = $"Sign-in failed ({(int)response.StatusCode}). Try again.";
                    return;
                }

                var result = await response.Content.ReadFromJsonAsync<LoginResult>();
                if (result is null || !Enum.TryParse<UserRole>(result.Role, out var role))
                {
                    lblError.Text = "Unexpected response from the server.";
                    return;
                }

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LastEmailPath)!);
                    File.WriteAllText(LastEmailPath, txtEmail.Text.Trim());
                }
                catch { /* not important */ }

                LoggedInRole = role;
                LoggedInDisplayName = result.DisplayName;
                SelectedCompanyId = result.CompanyId;   // 0 for Super Admin
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (TaskCanceledException)
            {
                lblError.Text = "The server took too long to respond. Try again.";
            }
            catch (Exception ex)
            {
                lblError.Text = "Sign-in failed: " + ex.Message;
            }
            finally
            {
                if (!IsDisposed) btnLogin.Enabled = true;
            }
        }
    }
}