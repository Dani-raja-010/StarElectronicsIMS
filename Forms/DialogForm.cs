using System.Drawing;
using System.Windows.Forms;
using StarElectronicsIMS.Util;

namespace StarElectronicsIMS.Forms
{
    /// <summary>Base for small auto-sized dialogs: optional banner, optional message, a field area, and OK/Cancel buttons.</summary>
    public class DialogForm : Form
    {
        protected readonly Button OkButton;
        protected readonly Button CancelBtn;

        public DialogForm(string title, string okText = "OK")
        {
            Text = title;
            Font = Theme.Base;
            Icon = Program.AppIcon;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = Color.White;

            OkButton = Ui.Btn(okText, (s, e) => { if (OnOk()) { DialogResult = DialogResult.OK; Close(); } }, 110, primary: true);
            CancelBtn = Ui.Btn("Cancel", (s, e) => { DialogResult = DialogResult.Cancel; Close(); }, 100);
            AcceptButton = OkButton;
            CancelButton = CancelBtn;
        }

        /// <summary>Lays out the dialog. Call once at the end of the subclass constructor.</summary>
        protected void Build(Control body, string header = null, Color? headerColor = null, Control banner = null)
        {
            var root = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                Location = Point.Empty,
                Padding = new Padding(0, 0, 0, 10),
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            if (banner != null)
            {
                banner.Dock = DockStyle.Fill;
                banner.Margin = Padding.Empty;
                root.Controls.Add(banner);
            }
            if (header != null)
            {
                root.Controls.Add(new Label
                {
                    Text = header,
                    AutoSize = true,
                    MaximumSize = new Size(440, 0),
                    Margin = new Padding(14, 12, 14, 0),
                    ForeColor = headerColor ?? SystemColors.ControlText,
                });
            }
            body.Dock = DockStyle.None;
            body.Margin = new Padding(10, 10, 14, 0);
            root.Controls.Add(body);

            var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Anchor = AnchorStyles.Right, Margin = new Padding(10, 10, 12, 0) };
            buttons.Controls.Add(OkButton);
            buttons.Controls.Add(CancelBtn);
            root.Controls.Add(buttons);

            Controls.Add(root);
        }

        /// <summary>Validate and save. Return true to close the dialog with OK.</summary>
        protected virtual bool OnOk() => true;
    }
}
