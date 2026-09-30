using System.Data;
using System.Windows.Forms;
using StarElectronicsIMS.Data;
using StarElectronicsIMS.Util;

namespace StarElectronicsIMS.Forms
{
    /// <summary>Create a user, edit a user, or change a password (depending on the factory used).</summary>
    public class UserEditForm : DialogForm
    {
        enum Mode { Create, FirstAdmin, Edit, ChangeOwnPassword, ResetPassword }

        readonly Mode _mode;
        readonly int _userId;
        readonly TextBox _username = Ui.Txt(220);
        readonly TextBox _fullName = Ui.Txt(220);
        readonly ComboBox _role = Ui.Combo(220);
        readonly CheckBox _active = new CheckBox { Text = "Account is active", AutoSize = true, Checked = true, Margin = new Padding(3, 6, 3, 3) };
        readonly TextBox _currentPass = Ui.Txt(220);
        readonly TextBox _pass = Ui.Txt(220);
        readonly TextBox _confirm = Ui.Txt(220);

        public static UserEditForm ForFirstAdmin() => new UserEditForm(Mode.FirstAdmin, 0, null);
        public static UserEditForm ForCreate() => new UserEditForm(Mode.Create, 0, null);
        public static UserEditForm ForEdit(DataRow user) => new UserEditForm(Mode.Edit, (int)user["UserID"], user);
        public static UserEditForm ForResetPassword(DataRow user) => new UserEditForm(Mode.ResetPassword, (int)user["UserID"], user);
        public static UserEditForm ForChangeOwnPassword() => new UserEditForm(Mode.ChangeOwnPassword, Session.CurrentUser.UserID, null);

        UserEditForm(Mode mode, int userId, DataRow user) : base(TitleFor(mode), "Save")
        {
            _mode = mode;
            _userId = userId;
            _role.Items.AddRange(new object[] { Roles.Staff, Roles.Admin });
            _role.SelectedItem = mode == Mode.FirstAdmin ? Roles.Admin : Roles.Staff;
            foreach (var t in new[] { _currentPass, _pass, _confirm }) t.UseSystemPasswordChar = true;

            if (user != null)
            {
                _username.Text = (string)user["Username"];
                _fullName.Text = user["FullName"] as string;
                _role.SelectedItem = (string)user["Role"];
                _active.Checked = (bool)user["IsActive"];
            }

            TableLayoutPanel body;
            string header = null;
            switch (mode)
            {
                case Mode.FirstAdmin:
                case Mode.Create:
                    _role.Enabled = mode != Mode.FirstAdmin;
                    body = Ui.FieldGrid(1, ("Username:", _username), ("Full name:", _fullName), ("Role:", _role),
                        ("Password:", _pass), ("Confirm password:", _confirm));
                    header = "Admin: full access.  Staff: can sell, receive stock and manage customers/suppliers, " +
                             "but cannot change products or prices, see profit reports, or manage users.";
                    break;
                case Mode.Edit:
                    _username.ReadOnly = true;
                    body = Ui.FieldGrid(1, ("Username:", _username), ("Full name:", _fullName), ("Role:", _role), ("", _active));
                    break;
                case Mode.ResetPassword:
                    _username.ReadOnly = true;
                    body = Ui.FieldGrid(1, ("Username:", _username), ("New password:", _pass), ("Confirm password:", _confirm));
                    break;
                default: // ChangeOwnPassword
                    body = Ui.FieldGrid(1, ("Current password:", _currentPass), ("New password:", _pass), ("Confirm password:", _confirm));
                    break;
            }
            Build(body, header);
        }

        static string TitleFor(Mode m)
        {
            switch (m)
            {
                case Mode.FirstAdmin: return "Create first Admin account";
                case Mode.Create: return "Add user";
                case Mode.Edit: return "Edit user";
                case Mode.ResetPassword: return "Reset password";
                default: return "Change my password";
            }
        }

        bool ValidNewPassword()
        {
            if (_pass.Text.Length < UserRepository.MinPasswordLength)
            {
                Ui.Warn("Password must be at least " + UserRepository.MinPasswordLength + " characters.");
                return false;
            }
            if (_pass.Text != _confirm.Text)
            {
                Ui.Warn("The two passwords do not match.");
                return false;
            }
            return true;
        }

        protected override bool OnOk()
        {
            string role = (string)_role.SelectedItem;
            switch (_mode)
            {
                case Mode.FirstAdmin:
                case Mode.Create:
                    if (string.IsNullOrWhiteSpace(_username.Text)) { Ui.Warn("Enter a username."); return false; }
                    if (!ValidNewPassword()) return false;
                    bool exists = false;
                    if (!Ui.Try("check the username", () => exists = UserRepository.UsernameExists(_username.Text))) return false;
                    if (exists) { Ui.Warn("That username is already taken."); return false; }
                    return Ui.Try("create the user", () => UserRepository.Create(_username.Text, _fullName.Text, role, _pass.Text));

                case Mode.Edit:
                    bool demotingOrDisablingAdmin = role != Roles.Admin || !_active.Checked;
                    if (demotingOrDisablingAdmin)
                    {
                        if (_userId == Session.CurrentUser.UserID)
                        {
                            Ui.Warn("You cannot remove Admin rights from, or disable, your own account.");
                            return false;
                        }
                        int others = 1;
                        if (!Ui.Try("check admins", () => others = UserRepository.OtherActiveAdmins(_userId))) return false;
                        if (others == 0) { Ui.Warn("At least one active Admin account must remain."); return false; }
                    }
                    return Ui.Try("save the user", () => UserRepository.Update(_userId, _fullName.Text, role, _active.Checked));

                case Mode.ResetPassword:
                    if (!ValidNewPassword()) return false;
                    return Ui.Try("reset the password", () => UserRepository.SetPassword(_userId, _pass.Text));

                default: // ChangeOwnPassword
                    AppUser check = null;
                    if (!Ui.Try("verify your password", () => check = UserRepository.Authenticate(Session.CurrentUser.Username, _currentPass.Text))) return false;
                    if (check == null) { Ui.Warn("Your current password is not correct."); return false; }
                    if (!ValidNewPassword()) return false;
                    if (!Ui.Try("change the password", () => UserRepository.SetPassword(_userId, _pass.Text))) return false;
                    Ui.Info("Password changed.");
                    return true;
            }
        }
    }
}
