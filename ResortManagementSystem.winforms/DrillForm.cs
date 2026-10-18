using System.Drawing;
using static ResortManagementSystem.Winforms.UiTheme;

namespace ResortManagementSystem.Winforms
{
    public class DrilldownForm : Form
    {
        public DrilldownForm(string title, DataGridView grid)
        {
            Text = title;
            Width = 900;
            Height = 500;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = ContentBg;

            var header = new Label { Text = title, Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = TextDark, Dock = DockStyle.Top, Height = 44, Padding = new Padding(16, 12, 0, 0) };
            grid.Dock = DockStyle.Fill;

            Controls.Add(grid);
            Controls.Add(header);
        }
    }
}