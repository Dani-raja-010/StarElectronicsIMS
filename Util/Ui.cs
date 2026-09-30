using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace StarElectronicsIMS.Util
{
    /// <summary>Tabs implement this so they reload when shown or after data changes elsewhere.</summary>
    public interface IRefreshable
    {
        void RefreshData();
    }

    public static class Theme
    {
        // Palette
        public static readonly Color Sidebar = Color.FromArgb(15, 32, 67);
        public static readonly Color SidebarHover = Color.FromArgb(28, 49, 92);
        public static readonly Color SidebarText = Color.FromArgb(191, 203, 222);
        public static readonly Color Accent = Color.FromArgb(37, 99, 235);
        public static readonly Color AccentHover = Color.FromArgb(29, 78, 216);
        public static readonly Color AccentLight = Color.FromArgb(219, 234, 254);
        public static readonly Color Background = Color.FromArgb(243, 246, 251);
        public static readonly Color Card = Color.White;
        public static readonly Color Border = Color.FromArgb(226, 232, 240);
        public static readonly Color Text = Color.FromArgb(30, 41, 59);
        public static readonly Color Muted = Color.FromArgb(100, 116, 139);
        public static readonly Color Success = Color.FromArgb(22, 163, 74);
        public static readonly Color Danger = Color.FromArgb(220, 38, 38);
        public static readonly Color Warning = Color.FromArgb(234, 88, 12);
        public static readonly Color LowStock = Color.FromArgb(254, 242, 242);
        public static readonly Color Inactive = Color.FromArgb(241, 245, 249);

        // Older names used across the tabs
        public static readonly Color Primary = Accent;
        public static readonly Color PrimaryDark = Color.FromArgb(30, 64, 175);
        public static readonly Color Header = Color.FromArgb(248, 250, 252);

        // Fonts
        public static readonly Font Base = new Font("Segoe UI", 10f);
        public static readonly Font Bold = new Font("Segoe UI Semibold", 10f);
        public static readonly Font Small = new Font("Segoe UI", 8.5f);
        public static readonly Font CardTitle = new Font("Segoe UI Semibold", 11.5f);
        public static readonly Font Big = new Font("Segoe UI Semibold", 17f);
        public static readonly Font PageTitle = new Font("Segoe UI Light", 20f);
        public static readonly Font Icon = new Font("Segoe MDL2 Assets", 13f);
    }

    /// <summary>White panel with a thin border, used to group content on the grey background.</summary>
    public class Card : Panel
    {
        public Card()
        {
            BackColor = Theme.Card;
            Padding = new Padding(16, 12, 16, 12);
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(Theme.Border))
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }

    public static class Ui
    {
        public const string MoneyFormat = "N2";

        public static void Info(string msg) => MessageBox.Show(msg, "Inventory Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
        public static void Warn(string msg) => MessageBox.Show(msg, "Inventory Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        public static void Error(string msg, Exception ex = null) =>
            MessageBox.Show(ex == null ? msg : msg + "\n\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        public static bool Confirm(string msg) =>
            MessageBox.Show(msg, "Please confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

        /// <summary>Runs an action and shows any exception in a message box. Returns true on success.</summary>
        public static bool Try(string what, Action action)
        {
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                action();
                return true;
            }
            catch (Exception ex)
            {
                Error("Could not " + what + ".", ex);
                return false;
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
        }

        public static Label Lbl(string text, bool bold = false) => new Label
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 8, 6, 3),
            Font = bold ? Theme.Bold : Theme.Base,
            ForeColor = bold ? Theme.Text : Theme.Muted,
            BackColor = Color.Transparent,
        };

        public static Button Btn(string text, EventHandler click, int width = 110, bool primary = false, bool danger = false)
        {
            var b = new Button
            {
                Text = text,
                Width = width,
                Height = 34,
                Margin = new Padding(3, 3, 6, 3),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = primary ? Theme.Bold : Theme.Base,
            };
            if (primary)
            {
                b.BackColor = Theme.Accent;
                b.ForeColor = Color.White;
                b.FlatAppearance.BorderSize = 0;
                b.FlatAppearance.MouseOverBackColor = Theme.AccentHover;
                b.FlatAppearance.MouseDownBackColor = Theme.PrimaryDark;
            }
            else
            {
                b.BackColor = Color.White;
                b.ForeColor = danger ? Theme.Danger : Theme.Text;
                b.FlatAppearance.BorderColor = danger ? Color.FromArgb(252, 165, 165) : Theme.Border;
                b.FlatAppearance.MouseOverBackColor = danger ? Theme.LowStock : Theme.Header;
                b.FlatAppearance.MouseDownBackColor = Theme.Border;
            }
            // Keep disabled buttons readable but clearly inactive.
            b.EnabledChanged += (s, e) => b.BackColor = !b.Enabled ? Theme.Inactive : primary ? Theme.Accent : Color.White;
            if (click != null) b.Click += click;
            return b;
        }

        public static TextBox Txt(int width = 200) => new TextBox
        {
            Width = width,
            Margin = new Padding(3, 5, 12, 5),
            BorderStyle = BorderStyle.FixedSingle,
            ForeColor = Theme.Text,
        };

        public static NumericUpDown Num(decimal min, decimal max, int decimals = 0, int width = 130) => new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            DecimalPlaces = decimals,
            ThousandsSeparator = true,
            Width = width,
            Margin = new Padding(3, 5, 12, 5),
            TextAlign = HorizontalAlignment.Right,
            BorderStyle = BorderStyle.FixedSingle,
            ForeColor = Theme.Text,
        };

        public static ComboBox Combo(int width = 200, bool editable = false)
        {
            var c = new ComboBox
            {
                Width = width,
                Margin = new Padding(3, 5, 12, 5),
                DropDownStyle = editable ? ComboBoxStyle.DropDown : ComboBoxStyle.DropDownList,
                ForeColor = Theme.Text,
            };
            if (editable)
            {
                c.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                c.AutoCompleteSource = AutoCompleteSource.ListItems;
            }
            return c;
        }

        public static CheckBox Check(string text, bool isChecked = false) => new CheckBox
        {
            Text = text,
            AutoSize = true,
            Checked = isChecked,
            Margin = new Padding(12, 9, 3, 3),
            ForeColor = Theme.Text,
            Cursor = Cursors.Hand,
        };

        public static FlowLayoutPanel Row(params Control[] controls)
        {
            var p = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                Dock = DockStyle.Top,
                Padding = Padding.Empty,
                BackColor = Color.Transparent,
            };
            p.Controls.AddRange(controls);
            return p;
        }

        /// <summary>Two-column "label: control" grid, laid out left to right in <paramref name="pairsPerRow"/> pairs.</summary>
        public static TableLayoutPanel FieldGrid(int pairsPerRow, params (string label, Control control)[] fields)
        {
            var t = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                ColumnCount = pairsPerRow * 2,
                Padding = Padding.Empty,
                BackColor = Color.Transparent,
            };
            for (int i = 0; i < pairsPerRow * 2; i++) t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            for (int i = 0; i < fields.Length; i++)
            {
                int col = (i % pairsPerRow) * 2, row = i / pairsPerRow;
                var label = Lbl(fields[i].label);
                if (col > 0) label.Margin = new Padding(18, 8, 6, 3);
                t.Controls.Add(label, col, row);
                t.Controls.Add(fields[i].control, col + 1, row);
            }
            return t;
        }

        /// <summary>
        /// Docks <paramref name="topToBottom"/> at the top in the given order, with a gap between them,
        /// and <paramref name="fill"/> in the remaining space. A grid used as fill is placed on a card.
        /// </summary>
        public static void Stack(Control parent, Control fill, params Control[] topToBottom)
        {
            parent.BackColor = Theme.Background;
            // WinForms docks the last-added control first, so add the fill control first and the top controls bottom-up.
            if (fill != null)
            {
                if (fill is DataGridView grid) fill = GridCard(grid);
                fill.Dock = DockStyle.Fill;
                parent.Controls.Add(fill);
            }
            for (int i = topToBottom.Length - 1; i >= 0; i--)
            {
                parent.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 12, BackColor = Color.Transparent });
                topToBottom[i].Dock = DockStyle.Top;
                parent.Controls.Add(topToBottom[i]);
            }
        }

        /// <summary>A titled, auto-sized card around a block of fields/buttons.</summary>
        public static Control Group(string title, Control content)
        {
            var card = new Card { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            var inner = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, Dock = DockStyle.Fill, BackColor = Color.Transparent };
            inner.Controls.Add(new Label { Text = title, Font = Theme.CardTitle, ForeColor = Theme.Text, AutoSize = true, Margin = new Padding(0, 0, 0, 8) });
            content.Dock = DockStyle.None;
            content.BackColor = Color.Transparent;
            inner.Controls.Add(content);
            card.Controls.Add(inner);
            return card;
        }

        /// <summary>Puts a grid on a card (with an optional title) so it matches the rest of the page.</summary>
        public static Card GridCard(DataGridView grid, string title = null)
        {
            var card = new Card { Padding = new Padding(1) };
            grid.Dock = DockStyle.Fill;
            card.Controls.Add(grid);
            if (title != null)
                card.Controls.Add(new Label
                {
                    Text = title,
                    Font = Theme.CardTitle,
                    ForeColor = Theme.Text,
                    Dock = DockStyle.Top,
                    Height = 42,
                    Padding = new Padding(14, 0, 0, 0),
                    TextAlign = ContentAlignment.MiddleLeft,
                });
            return card;
        }

        /// <summary>A read-only, styled grid that auto-formats money/date columns and hides the given columns after every bind.</summary>
        public static DataGridView Grid(params string[] hiddenColumns)
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Theme.Border,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EnableHeadersVisualStyles = false,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 40,
            };
            g.RowTemplate.Height = 32;
            g.ColumnHeadersDefaultCellStyle.BackColor = Theme.Header;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Muted;
            g.ColumnHeadersDefaultCellStyle.Font = Theme.Bold;
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.Header;
            g.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            g.DefaultCellStyle.ForeColor = Theme.Text;
            g.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
            g.DefaultCellStyle.SelectionBackColor = Theme.AccentLight;
            g.DefaultCellStyle.SelectionForeColor = Theme.Text;
            g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 251, 253);
            var hidden = new HashSet<string>(hiddenColumns, StringComparer.OrdinalIgnoreCase);
            g.DataBindingComplete += (s, e) => FormatColumns(g, hidden);
            return g;
        }

        static void FormatColumns(DataGridView g, HashSet<string> hidden)
        {
            foreach (DataGridViewColumn c in g.Columns)
            {
                if (hidden.Contains(c.Name)) { c.Visible = false; continue; }
                c.HeaderText = SplitCamel(c.HeaderText);
                c.MinimumWidth = 60;
                if (c.ValueType == typeof(decimal) || c.ValueType == typeof(double))
                {
                    c.DefaultCellStyle.Format = MoneyFormat;
                    c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    c.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
                }
                else if (c.ValueType == typeof(int) || c.ValueType == typeof(long))
                {
                    c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    c.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
                    c.FillWeight = 60;
                }
                else if (c.ValueType == typeof(DateTime))
                {
                    c.DefaultCellStyle.Format = "dd-MMM-yyyy HH:mm";
                }
                else if (c.ValueType == typeof(bool))
                {
                    c.FillWeight = 50;
                }
            }
        }

        /// <summary>"PurchasePrice" -> "Purchase Price", "ProductID" -> "Product ID".</summary>
        static string SplitCamel(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Contains(" ")) return s;
            var chars = new List<char>();
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i]) && (char.IsLower(s[i - 1]) || (i + 1 < s.Length && char.IsLower(s[i + 1]) && char.IsUpper(s[i - 1]))))
                    chars.Add(' ');
                chars.Add(s[i]);
            }
            return new string(chars.ToArray());
        }

        /// <summary>A small KPI tile: muted caption above a large value.</summary>
        public static Card StatCard(string caption, out Label value, Color? accent = null, int width = 210)
        {
            var card = new Card { Width = width, Height = 92, Margin = new Padding(0, 0, 14, 0), Padding = new Padding(18, 14, 12, 10) };
            value = new Label { Text = "-", Font = Theme.Big, ForeColor = accent ?? Theme.Text, AutoSize = true, Location = new Point(16, 40) };
            card.Controls.Add(new Label { Text = caption.ToUpperInvariant(), Font = Theme.Small, ForeColor = Theme.Muted, AutoSize = true, Location = new Point(18, 16) });
            card.Controls.Add(value);
            return card;
        }

        public static DataRow SelectedRow(DataGridView g) =>
            (g.CurrentRow?.DataBoundItem as DataRowView)?.Row;

        /// <summary>Binds a combo to a table, optionally with a leading "(none)" item whose value is DBNull.</summary>
        public static void BindCombo(ComboBox c, DataTable dt, string display, string value, string noneText = null)
        {
            if (noneText != null)
            {
                var r = dt.NewRow();
                r[display] = noneText;
                r[value] = DBNull.Value;
                dt.Rows.InsertAt(r, 0);
            }
            c.DataSource = null;
            c.DisplayMember = display;
            c.ValueMember = value;
            c.DataSource = dt;
        }

        public static int? ComboId(ComboBox c) =>
            c.SelectedValue == null || c.SelectedValue == DBNull.Value ? (int?)null : Convert.ToInt32(c.SelectedValue);

        public static void SelectComboId(ComboBox c, object id)
        {
            if (id == null || id == DBNull.Value) { if (c.Items.Count > 0) c.SelectedIndex = 0; return; }
            c.SelectedValue = id;
        }

        public static string NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        public static void SetMoney(NumericUpDown n, object value) =>
            n.Value = value == null || value == DBNull.Value ? 0 : Math.Min(n.Maximum, Math.Max(n.Minimum, Convert.ToDecimal(value)));

        public static IEnumerable<Control> Descendants(Control root) =>
            root.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));
    }
}
