namespace ResortManagementSystem.Winforms
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            try
            {
                bool keepRunning = true;
                while (keepRunning)
                {
                    using var login = new LoginForm();
                    if (login.ShowDialog() != DialogResult.OK)
                    {
                        keepRunning = false;
                        continue;
                    }

                    // A company Admin must accept the current terms before entering the app
                    if (login.LoggedInRole == UserRole.Admin &&
                        !TermsGate.EnsureAccepted(login.SelectedCompanyId, login.LoggedInDisplayName))
                    {
                        continue;   // declined, or the check failed: back to the sign-in screen
                    }

                    using var shell = new Shell(login.LoggedInRole, login.LoggedInDisplayName, login.SelectedCompanyId);
                    shell.Initialize();
                    Application.Run(shell);

                    keepRunning = shell.LoggedOut;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unhandled startup error:\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}",
                    "Startup Crash",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}