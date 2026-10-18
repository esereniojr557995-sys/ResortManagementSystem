using System.Drawing;
using System.Net.Http.Json;
using static ResortManagementSystem.Winforms.UiTheme;

namespace ResortManagementSystem.Winforms
{
    public partial class Shell : Form
    {
        private const int SidebarWidth = 310;   // was 280: the bold active item and lock icon did not fit
        private const int HeaderHeight = 64;
        private const int StatusPollSeconds = 30;

        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5226"),
            Timeout = TimeSpan.FromSeconds(25)   // give retry-on-failure room to work before the client itself gives up
        };
        private readonly int CompanyId;

        private readonly UserRole _role;
        private readonly string _displayName;
        private int _tierRank = 0;
        private string _companyName = "Resort CRM";

        private Panel pnlSidebar = null!;
        private Panel pnlContent = null!;
        private Panel _topBar = null!;
        private Label _pageTitle = null!;
        private Label _statusChip = null!;
        private System.Windows.Forms.Timer _statusTimer = null!;
        private readonly ToolTip _tip = new();
        private int _navTop = 0;
        private int _navStart = 0;

        private readonly List<(Panel navPanel, Label iconLabel, Label textLabel, Panel bar, Control panel)> _navItems = new();

        public bool LoggedOut { get; private set; } = false;

        private enum ConnectionStatus { Checking, Offline, LocalOnly, Full }

        private class HealthStatusDto
        {
            public bool Master { get; set; }
            public bool? Tenant { get; set; }
        }

        public Shell(UserRole role, string displayName, int companyId)
        {
            _role = role;
            _displayName = displayName;
            CompanyId = companyId;
        }

        public void Initialize()
        {
            if (_role == UserRole.SuperAdmin)
            {
                _companyName = "Resort CRM Platform";
            }
            else
            {
                FetchTierSync();
            }

            BuildShell();

            Load += async (_, _) => await RefreshConnectionStatusAsync();
            FormClosed += (_, _) => _statusTimer?.Stop();

            _statusTimer = new System.Windows.Forms.Timer { Interval = StatusPollSeconds * 1000 };
            _statusTimer.Tick += async (_, _) => await RefreshConnectionStatusAsync();
            _statusTimer.Start();
        }

        // Fails within ~6 seconds instead of hanging on a slow/asleep cloud database
        private void FetchTierSync()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
                var company = Task.Run(async () =>
                    await _http.GetFromJsonAsync<CompanyDto>($"/companies/{CompanyId}", cts.Token)
                ).GetAwaiter().GetResult();

                _tierRank = company?.SubscriptionTier switch
                {
                    "Medium" => 2,
                    "Small" => 1,
                    _ => 0
                };

                if (!string.IsNullOrWhiteSpace(company?.CompanyName))
                    _companyName = company!.CompanyName!;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FetchTier failed] {ex.Message}");
                _tierRank = 0;
            }
        }

        private class CompanyDto
        {
            public string SubscriptionTier { get; set; } = "Micro";
            public string? CompanyName { get; set; }
        }

        private void BuildShell()
        {
            Text = "Resort CRM";
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            BackColor = ContentBg;
            Font = new Font("Segoe UI", 9.5f);

            pnlSidebar = new GradientPanel { Dock = DockStyle.Left, Width = SidebarWidth };

            var logo = new Label
            {
                Text = _companyName,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = HeaderHeight,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(20, 0, 0, 0),
                UseMnemonic = false
            };
            logo.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = SidebarBorder });
            pnlSidebar.Controls.Add(logo);

            _topBar = new Panel { Dock = DockStyle.Top, Height = HeaderHeight, BackColor = CardBg };
            _topBar.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = BorderGray });

            // Shows the current page; updated whenever a menu item is opened
            _pageTitle = new Label
            {
                Text = "Dashboard",
                ForeColor = TextDark,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                AutoSize = true,
                Left = 32,
                Top = 21,
                UseMnemonic = false
            };
            _topBar.Controls.Add(_pageTitle);

            // ---- Connection status chip, top-right ----
            _statusChip = new Label
            {
                Text = "●  Checking connection…",
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                AutoSize = true,
                UseMnemonic = false,
                Top = 24
            };
            _topBar.Controls.Add(_statusChip);
            _statusChip.BringToFront();
            void RepositionChip() => _statusChip.Left = Math.Max(0, _topBar.Width - _statusChip.PreferredWidth - 24);
            _topBar.Resize += (s, e) => RepositionChip();
            RepositionChip();

            var tierLabel = _tierRank == 2 ? "Medium" : _tierRank == 1 ? "Small" : "Micro";

            pnlContent = new Panel { Dock = DockStyle.Fill, BackColor = ContentBg };

            _navStart = HeaderHeight + 24;
            _navTop = _navStart;
            switch (_role)
            {
                case UserRole.Staff:
                    AddSectionHeader("GENERAL");
                    AddNav("\uE80F", "Dashboard", new DashboardControl(_http, CompanyId, _role, _displayName, NavigateToPanelType));
                    AddNav("\uE77B", "Guest Profiles", new CustomersControl(_http, CompanyId));
                    AddNav("\uE787", "Reservations", new ReservationsControl(_http, CompanyId));
                    AddNav("\uE8BD", "Guest Feedback", new FeedbackControl(_http, CompanyId, canResolve: false));
                    break;

                case UserRole.Manager:
                    AddSectionHeader("GENERAL");
                    AddNav("\uE80F", "Dashboard", new DashboardControl(_http, CompanyId, _role, _displayName, NavigateToPanelType));
                    AddNav("\uE77B", "Guest Profiles", new CustomersControl(_http, CompanyId));
                    AddNav("\uE787", "Reservations", new ReservationsControl(_http, CompanyId));
                    AddSectionHeader("MANAGEMENT");
                    AddNav("\uE7C3", "Rooms & Services", new ProductsControl(_http, CompanyId));
                    if (_tierRank >= 1)
                        AddNav("\uE8EC", "Promotions & Loyalty", new PromotionsControl(_http, CompanyId));
                    else
                        AddLockedNav("\uE8EC", "Promotions & Loyalty", "Small");
                    AddNav("\uE730", "Resolve Complaints", new FeedbackControl(_http, CompanyId, canResolve: true));
                    break;

                case UserRole.Admin:
                    AddSectionHeader("GENERAL");
                    AddNav("\uE80F", "Dashboard", new DashboardControl(_http, CompanyId, _role, _displayName, NavigateToPanelType));
                    AddSectionHeader("MANAGEMENT");
                    AddNav("\uE716", "Staff & Manager Accounts", new StaffControl(_http, CompanyId));
                    AddNav("\uE7C3", "Rooms & Services", new ProductsControl(_http, CompanyId));
                    if (_tierRank >= 1)
                        AddNav("\uE9D2", "BI Dashboard & Reports", new BIDashboardControl(_http, CompanyId, _companyName, _tierRank == 2 ? "Medium" : _tierRank == 1 ? "Small" : "Micro"));
                    else
                        AddLockedNav("\uE9D2", "BI Dashboard & Reports", "Small");
                    if (_tierRank >= 2)
                        AddNav("\uE821", "Branch Management", new BranchControl(_http, CompanyId));
                    else
                        AddLockedNav("\uE821", "Branch Management", "Medium");
                    break;

                case UserRole.SuperAdmin:
                    AddSectionHeader("GENERAL");
                    AddNav("\uE80F", "Dashboard", new DashboardControl(_http, CompanyId, _role, _displayName, NavigateToPanelType));
                    AddSectionHeader("PLATFORM");
                    AddNav("\uE821", "Companies & Subscriptions", new CompaniesControl(_http));
                    AddNav("\uE9D2", "Platform BI Dashboard", new MasterBIDashboardControl(_http));
                    AddNav("\uEA18", "System Health", new ComingSoonControl("System Health & Uptime",
                        "This module would monitor CPU, memory, and deployment status across tenant databases. It's intentionally left as a placeholder — there's no real infrastructure-monitoring data source available in this project to report on honestly."));
                    break;
            }

            var contentInner = new Panel { Dock = DockStyle.Fill, BackColor = ContentBg, Padding = new Padding(24, 20, 24, 20) };
            foreach (var item in _navItems)
                contentInner.Controls.Add(item.panel);

            pnlContent.Controls.Add(contentInner);
            pnlContent.Controls.Add(_topBar);

            var footerPanel = new Panel { Dock = DockStyle.Bottom, Height = 140, BackColor = Color.Transparent };
            footerPanel.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = SidebarBorder });

            var signedInLabel = new Label { Text = "Signed in as", ForeColor = SidebarHeader, Font = new Font("Segoe UI", 8f), Left = 20, Top = 16, AutoSize = true, UseMnemonic = false };
            var nameLabel = new Label { Text = _displayName, ForeColor = Color.White, Font = new Font("Segoe UI", 11f, FontStyle.Bold), Left = 20, Top = 34, AutoSize = true, UseMnemonic = false };

            var roleChip = MakeChip(_role.ToString().ToLower(), Color.White, SidebarBg);
            roleChip.Left = 20;
            roleChip.Top = 62;

            var tierChip = MakeChip(_role == UserRole.SuperAdmin ? "Platform" : $"{tierLabel} tier", SidebarBorder, Color.White);
            tierChip.Left = roleChip.Left + roleChip.GetPreferredSize(Size.Empty).Width + 6;
            tierChip.Top = 62;

            var btnLogout = new Button
            {
                Text = "↩  Log out",
                Left = 20,
                Top = 94,
                Width = SidebarWidth - 40,
                Height = 34,
                FlatStyle = FlatStyle.Flat,
                BackColor = AccentCoral,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnLogout.BackColor = AccentCoral;
            btnLogout.FlatAppearance.BorderSize = 0;
            btnLogout.FlatAppearance.MouseOverBackColor = Color.FromArgb(0xE8, 0x62, 0x40);
            btnLogout.Click += (s, e) => { LoggedOut = true; Close(); };

            footerPanel.Controls.AddRange(new Control[] { signedInLabel, nameLabel, roleChip, tierChip, btnLogout });

            Controls.Add(pnlContent);
            Controls.Add(pnlSidebar);
            pnlSidebar.Controls.Add(footerPanel);

            if (_navItems.Count > 0)
                ActivateAndLoad(_navItems[0]);
        }

        // -------------------------------------------------- Connection status

        private async Task RefreshConnectionStatusAsync()
        {
            SetStatusChip(ConnectionStatus.Checking);
            try
            {
                // Give this more room than before: EnableRetryOnFailure on the API side means
                // a successful check can legitimately take several seconds if the cloud DB
                // had a transient hiccup and needed a retry.
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                var url = CompanyId > 0 ? $"/health/status?companyId={CompanyId}" : "/health/status";
                using var response = await _http.GetAsync(url, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    SetStatusChip(ConnectionStatus.Offline);
                    return;
                }

                var status = await response.Content.ReadFromJsonAsync<HealthStatusDto>(cancellationToken: cts.Token);
                if (status is null)
                {
                    SetStatusChip(ConnectionStatus.Offline);
                    return;
                }

                // Tenant is null when the request didn't ask about a specific company; treat that as "not a concern"
                bool cloudOk = status.Master && (status.Tenant ?? true);
                SetStatusChip(cloudOk ? ConnectionStatus.Full : ConnectionStatus.LocalOnly);
            }
            catch (TaskCanceledException)
            {
                // The local API accepted the connection but the cloud query behind it took too
                // long (or the client's own Timeout fired first) - that's a slow/flaky cloud DB,
                // not proof the local API itself is down.
                SetStatusChip(ConnectionStatus.LocalOnly);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Health check failed] {ex}");
                SetStatusChip(ConnectionStatus.Offline);
            }
        }

        private void SetStatusChip(ConnectionStatus status)
        {
            if (_statusChip is null || _statusChip.IsDisposed) return;

            (_statusChip.Text, _statusChip.ForeColor) = status switch
            {
                ConnectionStatus.Full => ("●  Connected (local + cloud)", Accent),
                ConnectionStatus.LocalOnly => ("●  Local only — cloud database slow or unreachable", AccentAmber),
                ConnectionStatus.Offline => ("●  Offline — local API unreachable", Danger),
                _ => ("●  Checking connection…", TextMuted)
            };

            _statusChip.Left = Math.Max(0, _topBar.Width - _statusChip.PreferredWidth - 24);
        }

        // -------------------------------------------------- Sidebar

        private void AddSectionHeader(string text)
        {
            if (_navTop > _navStart) _navTop += 12;

            var header = new Label
            {
                Text = text,
                ForeColor = SidebarHeader,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                BackColor = Color.Transparent,
                Left = 20,
                Top = _navTop,
                AutoSize = true,
                UseMnemonic = false
            };
            pnlSidebar.Controls.Add(header);
            _navTop += 28;
        }

        private void AddNav(string icon, string text, Control panel)
        {
            var navPanel = new Panel
            {
                Left = 0,
                Top = _navTop,
                Width = SidebarWidth,
                Height = 46,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            var accentBar = new Panel { Dock = DockStyle.Left, Width = 4, BackColor = Color.Transparent };

            var iconLabel = new Label
            {
                Text = icon,
                Font = new Font("Segoe MDL2 Assets", 12f),
                ForeColor = SidebarText,
                Left = 24,
                Top = 13,
                AutoSize = true,
                UseMnemonic = false,
                BackColor = Color.Transparent
            };

            var textLabel = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9.9f),
                ForeColor = SidebarText,
                Left = 60,
                Top = 13,
                AutoSize = true,
                UseMnemonic = false,
                BackColor = Color.Transparent
            };

            navPanel.Controls.Add(iconLabel);
            navPanel.Controls.Add(textLabel);
            navPanel.Controls.Add(accentBar);

            panel.Dock = DockStyle.Fill;
            panel.Visible = false;

            var item = (navPanel, iconLabel, textLabel, accentBar, panel);
            _navItems.Add(item);

            EventHandler clickHandler = (s, e) => ActivateAndLoad(item);
            navPanel.Click += clickHandler;
            iconLabel.Click += clickHandler;
            textLabel.Click += clickHandler;
            accentBar.Click += clickHandler;

            EventHandler enterHandler = (s, e) => { if (navPanel.BackColor != SidebarActive) navPanel.BackColor = SidebarHoverBg; };
            EventHandler leaveHandler = (s, e) => { if (navPanel.BackColor != SidebarActive) navPanel.BackColor = Color.Transparent; };
            navPanel.MouseEnter += enterHandler;
            navPanel.MouseLeave += leaveHandler;
            iconLabel.MouseEnter += enterHandler;
            iconLabel.MouseLeave += leaveHandler;
            textLabel.MouseEnter += enterHandler;
            textLabel.MouseLeave += leaveHandler;

            pnlSidebar.Controls.Add(navPanel);

            _navTop += 48;
        }

        // A menu item the current plan does not include: shown dimmed with a lock.
        // Clicking it explains which plan unlocks it. It is not part of _navItems.
        private void AddLockedNav(string icon, string text, string requiredTier)
        {
            var message = $"{text} is available on the {requiredTier} plan and above.\n\nAsk your platform administrator to upgrade your subscription.";

            var navPanel = new Panel
            {
                Left = 0,
                Top = _navTop,
                Width = SidebarWidth,
                Height = 46,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            var iconLabel = new Label
            {
                Text = icon,
                Font = new Font("Segoe MDL2 Assets", 12f),
                ForeColor = SidebarHeader,       // dimmer than normal items
                Left = 24,
                Top = 13,
                AutoSize = true,
                UseMnemonic = false,
                BackColor = Color.Transparent
            };

            var textLabel = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9.9f),
                ForeColor = SidebarHeader,
                Left = 60,
                Top = 13,
                AutoSize = true,
                UseMnemonic = false,
                BackColor = Color.Transparent
            };

            var lockLabel = new Label
            {
                Text = "\uE72E",                  // lock glyph
                Font = new Font("Segoe MDL2 Assets", 10f),
                ForeColor = AccentAmber,
                Left = SidebarWidth - 40,
                Top = 15,
                AutoSize = true,
                UseMnemonic = false,
                BackColor = Color.Transparent
            };

            navPanel.Controls.Add(iconLabel);
            navPanel.Controls.Add(textLabel);
            navPanel.Controls.Add(lockLabel);

            EventHandler clickHandler = (s, e) =>
                MessageBox.Show(message, "Upgrade required", MessageBoxButtons.OK, MessageBoxIcon.Information);

            EventHandler enterHandler = (s, e) => navPanel.BackColor = SidebarHoverBg;
            EventHandler leaveHandler = (s, e) => navPanel.BackColor = Color.Transparent;

            foreach (Control c in new Control[] { navPanel, iconLabel, textLabel, lockLabel })
            {
                c.Click += clickHandler;
                c.MouseEnter += enterHandler;
                c.MouseLeave += leaveHandler;
                _tip.SetToolTip(c, $"Requires the {requiredTier} plan");
            }

            pnlSidebar.Controls.Add(navPanel);
            _navTop += 48;
        }

        private void ActivateAndLoad((Panel navPanel, Label iconLabel, Label textLabel, Panel bar, Control panel) active)
        {
            foreach (var item in _navItems)
            {
                bool isActive = item == active;
                var bg = isActive ? SidebarActive : Color.Transparent;
                var fg = isActive ? SidebarActiveText : SidebarText;
                item.navPanel.BackColor = bg;
                item.iconLabel.ForeColor = fg;
                item.textLabel.ForeColor = fg;
                item.textLabel.Font = new Font("Segoe UI", 9.9f, isActive ? FontStyle.Bold : FontStyle.Regular);
                item.bar.BackColor = isActive ? AccentCoral : Color.Transparent;
                item.panel.Visible = isActive;
            }

            _pageTitle.Text = active.textLabel.Text;

            switch (active.panel)
            {
                case DashboardControl dc: _ = dc.LoadAsync(); break;
                case CustomersControl cc: _ = cc.LoadAsync(); break;
                case ReservationsControl rc: _ = rc.LoadAsync(); break;
                case StaffControl sc: _ = sc.LoadAsync(); break;
                case CompaniesControl comp: _ = comp.LoadAsync(); break;
                case PromotionsControl pc: _ = pc.LoadAsync(); break;
                case FeedbackControl fc: _ = fc.LoadAsync(); break;
                case BIDashboardControl bc: _ = bc.LoadAsync(); break;
                case BranchControl brc: _ = brc.LoadAsync(); break;
                case ProductsControl pdc: _ = pdc.LoadAsync(); break;
                case MasterBIDashboardControl mbc: _ = mbc.LoadAsync(); break;
            }
        }

        private void NavigateToPanelType(Type panelType)
        {
            var item = _navItems.FirstOrDefault(i => i.panel.GetType() == panelType);
            if (item.panel != null) ActivateAndLoad(item);
        }
    }
}