using System.Drawing;
using static ResortManagementSystem.Winforms.UiTheme;

namespace ResortManagementSystem.Winforms
{
    public class ComingSoonControl : UserControl
    {
        public ComingSoonControl(string moduleName, string description)
        {
            Dock = DockStyle.Fill;
            BackColor = ContentBg;

            var title = new Label { Text = moduleName, Font = new Font("Segoe UI", 18f, FontStyle.Bold), ForeColor = TextDark, Left = 36, Top = 28, AutoSize = true };
            var body = new Label { Text = description, Font = new Font("Segoe UI", 10f), ForeColor = TextMuted, Left = 36, Top = 68, Width = 600, AutoSize = true };

            Controls.AddRange(new Control[] { title, body });
        }
    }
}