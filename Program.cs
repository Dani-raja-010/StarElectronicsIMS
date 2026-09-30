using System;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using StarElectronicsIMS.Data;
using StarElectronicsIMS.Forms;
using StarElectronicsIMS.Util;

namespace StarElectronicsIMS
{
    static class Program
    {
        static Icon _icon;
        public static Icon AppIcon => _icon ?? (_icon = new Icon(typeof(Program).Assembly.GetManifestResourceStream("app.ico")));

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) => Ui.Error("An unexpected error occurred.", e.Exception);

            // Connect -> upgrade -> log in -> main window -> (log out / database changed) -> again ...
            while (true)
            {
                if (!Connect()) return;

                try
                {
                    Db.EnsureSchema();
                }
                catch (Exception ex)
                {
                    Ui.Error("The database could not be upgraded to the new version. No changes were made.", ex);
                    return;
                }

                if (!UserRepository.AnyUsers())
                {
                    Ui.Info("Welcome to Inventory Manager 2.0.\n\nNo user accounts exist yet. Please create the first Admin account.");
                    using (var f = UserEditForm.ForFirstAdmin())
                        if (f.ShowDialog() != DialogResult.OK) return;
                }

                using (var login = new LoginForm())
                    if (login.ShowDialog() != DialogResult.OK) return;

                var main = new MainForm();
                Application.Run(main);
                if (!main.LoggedOut) return;
                Session.CurrentUser = null;
            }
        }

        /// <summary>Tries the configured connection; on failure lets the user fix the server settings.</summary>
        static bool Connect()
        {
            while (true)
            {
                try
                {
                    Cursor.Current = Cursors.WaitCursor;
                    Db.TestConnection();
                    return true;
                }
                catch (SqlException ex)
                {
                    Cursor.Current = Cursors.Default;
                    using (var f = new DbSettingsForm("Could not connect to the database:\n" + ex.Message))
                        if (f.ShowDialog() != DialogResult.OK) return false;
                }
            }
        }
    }
}
