using System.Windows.Forms;
using StarElectronicsIMS.Data;
using StarElectronicsIMS.Forms;
using StarElectronicsIMS.Util;

namespace StarElectronicsIMS.Tabs
{
    /// <summary>Admin-only user management.</summary>
    public class UsersTab : UserControl, IRefreshable
    {
        readonly DataGridView _grid = Ui.Grid();

        public UsersTab()
        {
            Font = Theme.Base;
            var toolbar = Ui.Row(
                Ui.Btn("Add user", (s, e) => Open(UserEditForm.ForCreate()), primary: true),
                Ui.Btn("Edit user", (s, e) => WithSelected(r => Open(UserEditForm.ForEdit(r)))),
                Ui.Btn("Reset password", (s, e) => WithSelected(r => Open(UserEditForm.ForResetPassword(r))), 130),
                Ui.Lbl("Disabled users can't log in. Their past sales keep their name."));
            _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) WithSelected(r => Open(UserEditForm.ForEdit(r))); };
            Ui.Stack(this, _grid, toolbar);
        }

        public void RefreshData()
        {
            Ui.Try("load users", () => _grid.DataSource = Db.Query(
                "SELECT UserID, Username, FullName, Role, IsActive, CreatedAt FROM Users ORDER BY IsActive DESC, Username"));
        }

        void WithSelected(System.Action<System.Data.DataRow> action)
        {
            var r = Ui.SelectedRow(_grid);
            if (r == null) { Ui.Info("Select a user first."); return; }
            action(r);
        }

        void Open(Form f)
        {
            using (f)
            {
                f.StartPosition = FormStartPosition.CenterParent;
                if (f.ShowDialog(FindForm()) == DialogResult.OK) RefreshData();
            }
        }
    }
}
