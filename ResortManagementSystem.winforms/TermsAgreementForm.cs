using System.Net.Http.Json;

namespace ResortManagementSystem.Winforms
{
    /// <summary>Shown after login: the company Admin must accept the current terms to continue.</summary>
    public class TermsAgreementForm : Form
    {
        private readonly HttpClient _http;
        private readonly int _companyId;
        private readonly string _acceptedBy;
        private readonly AgreementStatusDto _status;

        private readonly CheckBox _agree = new() { Text = "I have read and accept these terms", AutoSize = true, Dock = DockStyle.Left };
        private readonly Button _accept = new() { Text = "Accept", Width = 110, Height = 32, Enabled = false };
        private readonly Button _decline = new() { Text = "Decline", Width = 110, Height = 32, DialogResult = DialogResult.Cancel };

        public TermsAgreementForm(HttpClient http, int companyId, string acceptedBy, AgreementStatusDto status)
        {
            _http = http;
            _companyId = companyId;
            _acceptedBy = acceptedBy;
            _status = status;

            Text = "Subscription Terms and Conditions";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(720, 600);
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            CancelButton = _decline;

            var sub = status.Subscription;
            var header = new Label
            {
                Dock = DockStyle.Top,
                Height = 70,
                Padding = new Padding(8),
                Text = $"{status.Terms.Title} (version {status.Terms.Version})\r\n" +
                       $"Plan: {sub.Tier}, {sub.Months} month(s), total {sub.TotalPrice:N2}, " +
                       $"{sub.StartDate:MMM d, yyyy} to {sub.EndDate:MMM d, yyyy}",
                Font = new Font(Font, FontStyle.Bold)
            };

            var body = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Text = status.Terms.Body.Replace("\n", "\r\n"),
                BackColor = SystemColors.Window
            };

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 56, Padding = new Padding(8) };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 250, FlowDirection = FlowDirection.RightToLeft };
            buttons.Controls.Add(_decline);
            buttons.Controls.Add(_accept);
            bottom.Controls.Add(_agree);
            bottom.Controls.Add(buttons);

            Controls.Add(body);
            Controls.Add(header);
            Controls.Add(bottom);

            _agree.CheckedChanged += (_, _) => _accept.Enabled = _agree.Checked;
            _accept.Click += async (_, _) => await AcceptAsync();
        }

        private async Task AcceptAsync()
        {
            _accept.Enabled = false;
            try
            {
                var payload = new
                {
                    subscriptionId = _status.Subscription.SubscriptionId,
                    termsId = _status.Terms.TermsId,
                    acceptedBy = _acceptedBy
                };
                var resp = await _http.PostAsJsonAsync($"/companies/{_companyId}/agreements", payload);
                if (!resp.IsSuccessStatusCode)
                {
                    var msg = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show($"Could not record your acceptance:\n{msg}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    _accept.Enabled = _agree.Checked;
                    return;
                }
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not reach the server:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                _accept.Enabled = _agree.Checked;
            }
        }
    }
}