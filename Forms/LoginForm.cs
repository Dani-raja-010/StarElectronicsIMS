using System;
using System.Drawing;
using System.Windows.Forms;
using StarElectronicsIMS.Data;
using StarElectronicsIMS.Util;

namespace StarElectronicsIMS.Forms
{
    public class LoginForm : DialogForm
    {
        readonly TextBox _user = Ui.Txt(220);
        readonly TextBox _pass = Ui.Txt(220);
        int _failures;

        public LoginForm() : base("Inventory Manager - Log in", "Log in")
        {
            _pass.UseSystemPasswordChar = true;
            var body = Ui.FieldGrid(1, ("Username:", _user), ("Password:", _pass));
            var banner = new Label
            {
                Text = "\u2605  Inventory Manager",
                Font = Theme.Big,
                ForeColor = Color.White,
                BackColor = Theme.Sidebar,
                Height = 84,
                TextAlign = ContentAlignment.MiddleCenter,
            };
            Build(body, banner: banner);
            Shown += (s, e) => _user.Focus();
        }

        protected override bool OnOk()
        {
            if (string.IsNullOrWhiteSpace(_user.Text) || _pass.Text.Length == 0)
            {
                Ui.Warn("Enter your username and password.");
                return false;
            }
            AppUser user = null;
            if (!Ui.Try("log in", () => user = UserRepository.Authenticate(_user.Text, _pass.Text))) return false;
            if (user == null)
            {
                _failures++;
                Ui.Warn("Wrong username or password, or the account is disabled.");
                _pass.Clear();
                _pass.Focus();
                if (_failures >= 5) { DialogResult = DialogResult.Cancel; Close(); }
                return false;
            }
            Session.CurrentUser = user;
            return true;
        }
    }
}
