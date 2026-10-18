using System.Drawing;
using System.Net.Http.Json;
using ResortManagementSystem.Domain.Entities;
using static ResortManagementSystem.Winforms.UiTheme;

namespace ResortManagementSystem.Winforms
{
    public class TermsEditorForm : Form
    {
        private readonly HttpClient _http;
        private ComboBox cboVersions = null!;
        private TextBox txtTitle = null!, txtBody = null!;
        private Button btnPublish = null!;
        private Label lblStatus = null!;

        private class VersionItem
        {
            public TermsAndConditions Terms { get; set; } = null!;
            public override string ToString() =>
                $"Version {Terms.Version}{(Terms.IsCurrent ? "  (current)" : "")}   -   {SubscriptionFormat.LocalStamp(Terms.CreatedAt)}";
        }

        public TermsEditorForm(HttpClient http)
        {
            _http = http;
            Text = "Terms and Conditions";
            Width = 860;
            Height = 740;
            MinimumSize = new Size(700, 560);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = ContentBg;
            Font = new Font("Segoe UI", 9.5f);

            BuildUi();
            Load += async (s, e) => await LoadVersionsAsync();
        }

        private void BuildUi()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 150, BackColor = ContentBg };
            var heading = new Label { Text = "Terms and Conditions", Font = new Font("Segoe UI", 16f, FontStyle.Bold), ForeColor = TextDark, Left = 24, Top = 14, AutoSize = true };
            var info = new Label
            {
                Left = 24,
                Top = 54,
                Width = 790,
                Height = 44,
                ForeColor = TextMuted,
                UseMnemonic = false,
                Text = "Each company's Admin must accept the current version when their subscription is created or changed. Publishing creates a new version; earlier versions are kept and existing acceptances are not affected."
            };
            var lblVersion = new Label { Text = "Version", Left = 24, Top = 112, Width = 70, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = TextMuted };
            cboVersions = new ComboBox { Left = 100, Top = 108, Width = 520, DropDownStyle = ComboBoxStyle.DropDownList };
            cboVersions.SelectedIndexChanged += (s, e) => ShowSelected();
            header.Controls.AddRange(new Control[] { heading, info, lblVersion, cboVersions });

            var titleRow = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = ContentBg };
            var lblTitle = new Label { Text = "Title", Left = 24, Top = 16, Width = 70, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = TextMuted };
            txtTitle = new TextBox { Left = 100, Top = 12, Width = 700, MaxLength = 200, BorderStyle = BorderStyle.FixedSingle, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            titleRow.Controls.AddRange(new Control[] { lblTitle, txtTitle });

            var bodyHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 4, 24, 8), BackColor = ContentBg };
            txtBody = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                AcceptsReturn = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 10f)
            };
            bodyHost.Controls.Add(txtBody);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 70, BackColor = ContentBg };
            btnPublish = MakePillButton("Publish as New Version", Accent, Color.White, 24, 14, 220);
            btnPublish.Height = 40;
            var btnClose = MakePillButton("Close", CardBg, TextDark, 256, 14, 110);
            btnClose.Height = 40;
            lblStatus = new Label { Left = 384, Top = 24, Width = 440, Height = 24, ForeColor = TextMuted, Font = new Font("Segoe UI", 8.5f, FontStyle.Italic), UseMnemonic = false };
            footer.Controls.AddRange(new Control[] { btnPublish, btnClose, lblStatus });

            btnPublish.Click += async (s, e) => await PublishAsync();
            btnClose.Click += (s, e) => Close();
            CancelButton = btnClose;

            // Fill first, then Bottom, then Top (last added = top-most)
            Controls.Add(bodyHost);
            Controls.Add(footer);
            Controls.Add(titleRow);
            Controls.Add(header);
        }

        private async Task LoadVersionsAsync()
        {
            try
            {
                var all = await _http.GetFromJsonAsync<List<TermsAndConditions>>("/terms") ?? new();
                cboVersions.Items.Clear();
                foreach (var t in all) cboVersions.Items.Add(new VersionItem { Terms = t });

                var current = cboVersions.Items.Cast<VersionItem>().FirstOrDefault(v => v.Terms.IsCurrent);
                if (current is not null) cboVersions.SelectedItem = current;
                else if (cboVersions.Items.Count > 0) cboVersions.SelectedIndex = 0;

                lblStatus.Text = $"{all.Count} version(s). Edit the text and publish to create a new version.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Could not load the terms.";
                MessageBox.Show($"Could not reach the server: {ex.Message}", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowSelected()
        {
            if (cboVersions.SelectedItem is not VersionItem item) return;
            txtTitle.Text = item.Terms.Title;
            txtBody.Text = item.Terms.Body.Replace("\r\n", "\n").Replace("\n", "\r\n");
            txtBody.Select(0, 0);
        }

        private async Task PublishAsync()
        {
            var title = txtTitle.Text.Trim();
            var body = txtBody.Text.Trim();
            if (title.Length == 0) { MessageBox.Show("Enter a title."); return; }
            if (body.Length < 20) { MessageBox.Show("Enter the full terms text (at least 20 characters)."); return; }

            var confirm = MessageBox.Show(
                "Publish this text as a new version?\n\nCompanies that have already accepted keep the version they accepted. The new version is shown to Admins whenever a subscription is created or changed.",
                "Publish terms", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            btnPublish.Enabled = false;
            try
            {
                var response = await _http.PostAsJsonAsync("/terms", new { title, body });
                if (response.IsSuccessStatusCode)
                {
                    await LoadVersionsAsync();
                    lblStatus.Text = "New version published.";
                }
                else
                {
                    MessageBox.Show($"Could not publish the terms:\n{await response.Content.ReadAsStringAsync()}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not reach the server: {ex.Message}", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnPublish.Enabled = true;
            }
        }
    }
}