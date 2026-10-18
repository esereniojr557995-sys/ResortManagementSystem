using System.Drawing;
using static ResortManagementSystem.Winforms.UiTheme;

namespace ResortManagementSystem.Winforms
{
    // Small dialog shown before a promotion is emailed: lets the user adjust the subject and add a personal note
    public class EmailComposeForm : Form
    {
        private readonly TextBox txtSubject, txtMessage;
        private readonly Label lblError;

        public string Subject => txtSubject.Text.Trim();
        public string Message => txtMessage.Text.Trim();

        public EmailComposeForm(string promoCode, string defaultSubject, int recipients, int alreadySent)
        {
            Text = "Email customers";
            ClientSize = new Size(560, 470);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = CardBg;
            Font = new Font("Segoe UI", 9.5f);

            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(28, 22, 28, 18) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            void Row(Control c, SizeType type = SizeType.AutoSize, float h = 0)
            {
                t.RowStyles.Add(new RowStyle(type, h));
                t.Controls.Add(c, 0, t.RowCount++);
            }
            t.RowCount = 0;

            var title = new Label { Text = $"Email promotion {promoCode}", Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = TextDark, AutoSize = true, Margin = new Padding(0, 0, 0, 4), UseMnemonic = false };
            var info = new Label
            {
                Text = $"This will be sent to {recipients} customer(s)." +
                       (alreadySent > 0 ? $"  {alreadySent} guest(s) who already got this promotion are skipped." : ""),
                ForeColor = TextMuted,
                AutoSize = false,
                Height = 40,
                Dock = DockStyle.Fill,
                UseMnemonic = false
            };

            var lblSubject = new Label { Text = "Subject (leave as is or change it)", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = TextMuted, AutoSize = true, Margin = new Padding(0, 6, 0, 4), UseMnemonic = false };
            txtSubject = new TextBox { Dock = DockStyle.Fill, Text = defaultSubject, BorderStyle = BorderStyle.FixedSingle, MaxLength = 150 };

            var lblMessage = new Label { Text = "Personal note (optional)", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = TextMuted, AutoSize = true, Margin = new Padding(0, 12, 0, 4), UseMnemonic = false };
            txtMessage = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle, MaxLength = 1000, PlaceholderText = "e.g. Thank you for staying with us. Here is a little something for your next visit." };

            lblError = new Label { AutoSize = false, Height = 24, Dock = DockStyle.Fill, ForeColor = Danger, Font = new Font("Segoe UI", 9f, FontStyle.Bold), UseMnemonic = false };

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Height = 48 };
            var btnSend = MakePillButton("Send emails", Accent, Color.White, 0, 0, 150);
            var btnCancel = MakePillButton("Cancel", CardBg, TextMuted, 0, 0, 110);
            buttons.Controls.Add(btnSend);
            buttons.Controls.Add(btnCancel);

            Row(title);
            Row(info, SizeType.Absolute, 48);
            Row(lblSubject);
            Row(txtSubject);
            Row(lblMessage);
            Row(txtMessage, SizeType.Percent, 100f);
            Row(lblError, SizeType.Absolute, 28);
            Row(buttons, SizeType.Absolute, 52);
            Controls.Add(t);

            btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;
            btnSend.Click += (s, e) =>
            {
                if (Subject.Length < 3) { lblError.Text = "⚠  The subject must be at least 3 characters."; txtSubject.Focus(); return; }
                DialogResult = DialogResult.OK;
            };
            AcceptButton = btnSend;
            CancelButton = btnCancel;
        }
    }
}