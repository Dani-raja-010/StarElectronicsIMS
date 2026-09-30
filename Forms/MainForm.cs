using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using StarElectronicsIMS.Data;
using StarElectronicsIMS.Tabs;
using StarElectronicsIMS.Util;

namespace StarElectronicsIMS.Forms
{
    /// <summary>Main window: dark sidebar navigation on the left, page header and content on the right.</summary>
    public class MainForm : Form
    {
        readonly Panel _sidebar = new Panel { Dock = DockStyle.Left, Width = 230, BackColor = Theme.Sidebar };
        readonly Panel _content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background, Padding = new Padding(24, 12, 24, 20) };
        readonly Label _pageTitle = new Label { Font = Theme.PageTitle, ForeColor = Theme.Text, AutoSize = true, UseMnemonic = false, Location = new Point(22, 14) };
        readonly LinkLabel _lowStock = new LinkLabel
        {
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            LinkColor = Theme.Danger,
            ActiveLinkColor = Theme.Danger,
            LinkBehavior = LinkBehavior.HoverUnderline,
            Font = Theme.Bold,
        };
        readonly List<NavItem> _nav = new List<NavItem>();
        readonly ProductsTab _products = new ProductsTab();
        NavItem _productsNav;
        Control _current;

        /// <summary>True when the window closed because the user chose "Log out" (so the login screen is shown again).</summary>
        public bool LoggedOut { get; private set; }

        public MainForm()
        {
            var user = Session.CurrentUser;
            Text = "Inventory Manager";
            Icon = Program.AppIcon;
            Font = Theme.Base;
            BackColor = Theme.Background;
            Size = new Size(1320, 820);
            MinimumSize = new Size(1100, 680);
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.F5) { RefreshCurrent(); e.Handled = true; } };

            var dashboard = new DashboardTab();
            var sales = new SalesTab();
            var purchases = new PurchasesTab();
            sales.SaleCompleted += (s, e) => UpdateLowStock();
            purchases.StockReceived += (s, e) => UpdateLowStock();
            dashboard.LowStockClicked += (s, e) => ShowLowStock();

            // ----- header -----
            var header = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = Color.White };
            header.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, header.Height - 1, header.Width, header.Height - 1); };
            var date = new Label { Text = DateTime.Today.ToString("dddd, dd MMMM yyyy"), ForeColor = Theme.Muted, AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            header.Controls.Add(_pageTitle);
            header.Controls.Add(_lowStock);
            header.Controls.Add(date);
            header.Resize += (s, e) =>
            {
                date.Location = new Point(header.Width - date.Width - 24, 14);
                _lowStock.Location = new Point(header.Width - _lowStock.Width - 24, 36);
            };
            _lowStock.LinkClicked += (s, e) => ShowLowStock();

            // ----- sidebar -----
            var brand = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Theme.Sidebar };
            brand.Controls.Add(new Label { Text = "", Font = new Font("Segoe MDL2 Assets", 20f), ForeColor = Color.FromArgb(250, 204, 21), AutoSize = true, Location = new Point(20, 26) });
            brand.Controls.Add(new Label { Text = "Inventory Manager", Font = new Font("Segoe UI Semibold", 13f), ForeColor = Color.White, AutoSize = true, Location = new Point(56, 22) });
            brand.Controls.Add(new Label { Text = "Stock  \u2022  Sales  \u2022  Reports", Font = Theme.Small, ForeColor = Theme.SidebarText, AutoSize = true, Location = new Point(58, 48) });

            var navHost = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Sidebar, Padding = new Padding(0, 8, 0, 0) };
            AddPage(navHost, "", "Dashboard", dashboard);
            _productsNav = AddPage(navHost, "", "Products", _products);
            AddPage(navHost, "", "New Sale", sales);
            AddPage(navHost, "", "Receive Stock", purchases);
            AddPage(navHost, "", "History & Reports", new HistoryTab());
            AddPage(navHost, "", "Customers", new ContactsTab(ContactsTab.Kind.Customers));
            AddPage(navHost, "", "Suppliers", new ContactsTab(ContactsTab.Kind.Suppliers));
            if (user.IsAdmin) AddPage(navHost, "", "Users", new UsersTab());

            var account = new Panel { Dock = DockStyle.Bottom, Height = 192, BackColor = Theme.Sidebar };
            account.Paint += (s, e) => { using (var p = new Pen(Theme.SidebarHover)) e.Graphics.DrawLine(p, 16, 0, account.Width - 16, 0); };
            account.Controls.Add(new Label { Text = user.DisplayName, Font = Theme.Bold, ForeColor = Color.White, AutoSize = true, Location = new Point(20, 14) });
            account.Controls.Add(new Label { Text = user.Role, Font = Theme.Small, ForeColor = Theme.SidebarText, AutoSize = true, MaximumSize = new Size(200, 0), Location = new Point(20, 38) });
            int y = 70;
            void Action(string icon, string text, Action onClick)
            {
                var item = new NavItem(icon, text) { Location = new Point(0, y), Width = _sidebar.Width, Height = 34 };
                item.Click += (s, e) => onClick();
                account.Controls.Add(item);
                y += 34;
            }
            Action("", "Change password", () => { using (var f = UserEditForm.ForChangeOwnPassword()) { f.StartPosition = FormStartPosition.CenterParent; f.ShowDialog(this); } });
            if (user.IsAdmin) Action("", "Database settings", EditDbSettings);
            Action("", "Log out", () => { LoggedOut = true; Close(); });

            _sidebar.Controls.Add(navHost);
            _sidebar.Controls.Add(account);
            _sidebar.Controls.Add(brand);

            Controls.Add(_content);
            Controls.Add(header);
            Controls.Add(_sidebar);

            Shown += (s, e) => Select(_nav[0]);
        }

        NavItem AddPage(FlowLayoutPanel host, string icon, string title, UserControl page)
        {
            page.Dock = DockStyle.Fill;
            page.Visible = false;
            _content.Controls.Add(page);
            var item = new NavItem(icon, title) { Width = _sidebar.Width, Height = 46, Margin = Padding.Empty, Tag = page };
            item.Click += (s, e) => Select(item);
            host.Controls.Add(item);
            _nav.Add(item);
            return item;
        }

        void Select(NavItem item)
        {
            foreach (var n in _nav) n.Active = n == item;
            var page = (Control)item.Tag;
            if (_current != null && _current != page) _current.Visible = false;
            _current = page;
            _pageTitle.Text = item.Title;
            page.Visible = true;
            RefreshCurrent();
        }

        void ShowLowStock()
        {
            Select(_productsNav);
            _products.ShowLowStock();
        }

        void RefreshCurrent()
        {
            if (_current is IRefreshable r) r.RefreshData();
            UpdateLowStock();
        }

        void UpdateLowStock()
        {
            try
            {
                int n = Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM Products WHERE IsActive = 1 AND Quantity <= ReorderLevel"));
                _lowStock.Text = n == 0 ? "" : "⚠  " + n + " product(s) low on stock";
            }
            catch (SqlException)
            {
                _lowStock.Text = "Database connection problem";
            }
            _lowStock.Location = new Point(_lowStock.Parent.Width - _lowStock.Width - 24, 36);
        }


        void EditDbSettings()
        {
            using (var f = new DbSettingsForm())
            {
                f.StartPosition = FormStartPosition.CenterParent;
                if (f.ShowDialog(this) != DialogResult.OK) return;
            }
            Ui.Info("Database settings saved. Please log in again.");
            LoggedOut = true;
            Close();
        }
    }

    /// <summary>Owner-drawn sidebar entry: icon glyph + text, with hover and active (accent bar) states.</summary>
    public class NavItem : Control
    {
        static readonly Font IconFont = new Font("Segoe MDL2 Assets", 12f);
        readonly string _icon;
        bool _hover, _active;

        public string Title => Text;

        public NavItem(string icon, string text)
        {
            _icon = icon;
            Text = text;
            Font = Theme.Base;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.StandardClick, false); // Click is raised from OnMouseUp below
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location)) OnClick(e);
        }

        public bool Active
        {
            get => _active;
            set { _active = value; Invalidate(); }
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(_active ? Theme.SidebarHover : _hover ? Color.FromArgb(22, 41, 80) : Theme.Sidebar);
            if (_active)
                using (var b = new SolidBrush(Theme.Accent)) g.FillRectangle(b, 0, 6, 4, Height - 12);
            var fore = _active || _hover ? Color.White : Theme.SidebarText;
            TextRenderer.DrawText(g, _icon, IconFont, new Rectangle(20, 0, 28, Height), _active ? Color.FromArgb(147, 197, 253) : fore,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, Text, _active ? Theme.Bold : Font, new Rectangle(54, 0, Width - 60, Height), fore,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}
