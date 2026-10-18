using System.Drawing;
using System.Net.Http.Json;

namespace ResortManagementSystem.Winforms
{
    public class ChangePasswordForm : Form
    {
        private readonly HttpClient _http;
        private readonly TextBox txtEmail, txtCurrent, txtNew, txtConfirm;
        private readonly Label lblMsg;
        private readonly Button btnSave;

        public ChangePasswordForm(HttpClient http, string email)
        {
            _http = http;
            Text = "Change Password";
            Width = 440; Height = 400;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            Font = new Font("Segoe UI", 9.5f);

            int y = 20;
            TextBox Add(string label, bool pwd, string text = "")
            {
                Controls.Add(new Label { Text = label, Left = 24, Top = y, AutoSize = true });
                var tb = new TextBox { Left = 24, Top = y + 22, Width = 370, UseSystemPasswordChar = pwd, Text = text };
                Controls.Add(tb);
                y += 64;
                return tb;
            }

            txtEmail = Add("Email", false, email);
            txtCurrent = Add("Current password", true);
            txtNew = Add("New password (min 8 characters)", true);
            txtConfirm = Add("Confirm new password", true);

            lblMsg = new Label { Left = 24, Top = y - 6, Width = 370, Height = 20, ForeColor = Color.Firebrick };
            btnSave = new Button { Text = "Change Password", Left = 24, Top = y + 18, Width = 370, Height = 34 };
            btnSave.Click += async (s, e) => await SaveAsync();
            Controls.Add(lblMsg);
            Controls.Add(btnSave);
            AcceptButton = btnSave;
        }

        private async Task SaveAsync()
        {
            lblMsg.ForeColor = Color.Firebrick;
            if (txtNew.Text.Length < 8) { lblMsg.Text = "New password must be at least 8 characters."; return; }
            if (txtNew.Text != txtConfirm.Text) { lblMsg.Text = "New passwords do not match."; return; }

            btnSave.Enabled = false;
            try
            {
                using var response = await _http.PostAsJsonAsync("/auth/change-password", new
                {
                    email = txtEmail.Text.Trim(),
                    currentPassword = txtCurrent.Text,
                    newPassword = txtNew.Text
                });

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Password changed. Use it the next time you sign in.");
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    lblMsg.Text = "Email or current password is incorrect.";
                else
                    lblMsg.Text = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                lblMsg.Text = "Failed: " + ex.Message;
            }
            finally
            {
                if (!IsDisposed) btnSave.Enabled = true;
            }
        }
    }
}