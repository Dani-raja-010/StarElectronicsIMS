using System;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using StarElectronicsIMS.Data;
using StarElectronicsIMS.Util;

namespace StarElectronicsIMS.Forms
{
    /// <summary>Edit the SQL Server connection (server, database, Windows or SQL login).</summary>
    public class DbSettingsForm : DialogForm
    {
        readonly TextBox _server = Ui.Txt(260);
        readonly TextBox _database = Ui.Txt(260);
        readonly CheckBox _windowsAuth = new CheckBox { Text = "Use Windows authentication", AutoSize = true, Margin = new Padding(3, 6, 3, 3) };
        readonly TextBox _sqlUser = Ui.Txt(260);
        readonly TextBox _sqlPass = Ui.Txt(260);

        public DbSettingsForm(string errorMessage = null) : base("Database settings", "Test && Save")
        {
            _sqlPass.UseSystemPasswordChar = true;
            SqlConnectionStringBuilder b;
            try { b = new SqlConnectionStringBuilder(Db.ConnectionString); }
            catch (ArgumentException) { b = new SqlConnectionStringBuilder(); }
            _server.Text = b.DataSource;
            _database.Text = string.IsNullOrEmpty(b.InitialCatalog) ? "InventoryManagerDB" : b.InitialCatalog;
            _windowsAuth.Checked = b.IntegratedSecurity;
            _sqlUser.Text = b.UserID;
            _sqlPass.Text = b.Password;
            _windowsAuth.CheckedChanged += (s, e) => _sqlUser.Enabled = _sqlPass.Enabled = !_windowsAuth.Checked;
            _sqlUser.Enabled = _sqlPass.Enabled = !_windowsAuth.Checked;

            var body = Ui.FieldGrid(1,
                ("Server:", _server),
                ("Database:", _database),
                ("", _windowsAuth),
                ("SQL username:", _sqlUser),
                ("SQL password:", _sqlPass));
            Build(body,
                (errorMessage != null ? errorMessage + "\n\n" : "") + "Example server names:  .\\MSSQLSERVER2022   or   PC-NAME\\SQLEXPRESS   or   192.168.1.10",
                errorMessage != null ? Color.DarkRed : (Color?)null);
        }

        protected override bool OnOk()
        {
            var b = new SqlConnectionStringBuilder
            {
                DataSource = _server.Text.Trim(),
                InitialCatalog = _database.Text.Trim(),
                IntegratedSecurity = _windowsAuth.Checked,
                ConnectTimeout = 10,
            };
            if (!_windowsAuth.Checked)
            {
                b.UserID = _sqlUser.Text.Trim();
                b.Password = _sqlPass.Text;
            }
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                using (var cn = new SqlConnection(b.ConnectionString)) cn.Open();
            }
            catch (SqlException ex)
            {
                Ui.Error("Connection failed.", ex);
                return false;
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
            if (!Db.SaveConnectionString(b.ConnectionString))
                Ui.Warn("Connected, but the settings could not be saved to InventoryManager.exe.config (no permission).\nThey will be used until the program is closed.");
            return true;
        }
    }
}
