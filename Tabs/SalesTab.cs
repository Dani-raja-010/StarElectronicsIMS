using System;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using StarElectronicsIMS.Data;
using StarElectronicsIMS.Util;

namespace StarElectronicsIMS.Tabs
{
    /// <summary>Point-of-sale screen: build a cart of several products, then save it as one invoice.</summary>
    public class SalesTab : UserControl, IRefreshable
    {
        readonly DataGridView _products = Ui.Grid("ProductID");
        readonly DataGridView _cart = Ui.Grid("ProductID", "InStock");
        readonly DataTable _cartTable = new DataTable();
        readonly TextBox _search = Ui.Txt(220);
        readonly NumericUpDown _addQty = Ui.Num(1, 100_000, width: 70);
        readonly ComboBox _customer = Ui.Combo(280, editable: true);
        readonly NumericUpDown _discount = Ui.Num(0, 99_999_999, 2);
        readonly TextBox _notes = Ui.Txt(280);
        readonly Label _subtotal = Ui.Lbl("0.00", bold: true);
        readonly Label _total = new Label { Text = "0.00", AutoSize = true, Font = new System.Drawing.Font("Segoe UI Semibold", 22f), ForeColor = Theme.Accent, Margin = new Padding(8, 0, 3, 6) };
        readonly Timer _searchDelay = new Timer { Interval = 300 };

        public event EventHandler SaleCompleted;

        public SalesTab()
        {
            Font = Theme.Base;
            _addQty.Value = 1;

            _cartTable.Columns.Add("ProductID", typeof(int));
            _cartTable.Columns.Add("Product", typeof(string));
            _cartTable.Columns.Add("InStock", typeof(int));
            _cartTable.Columns.Add("Qty", typeof(int));
            _cartTable.Columns.Add("UnitPrice", typeof(decimal));
            _cartTable.Columns.Add("LineTotal", typeof(decimal), "Qty * UnitPrice");
            _cart.ReadOnly = false;
            _cart.DataSource = _cartTable;
            _cart.DataBindingComplete += (s, e) =>
            {
                foreach (DataGridViewColumn c in _cart.Columns) c.ReadOnly = true;
                _cart.Columns["Qty"].ReadOnly = false;
                _cart.Columns["UnitPrice"].ReadOnly = !Session.IsAdmin; // only admins may change prices at the till
                _cart.Columns["UnitPrice"].DefaultCellStyle.Format = "0.00"; // no thousands separator while editing
            };
            _cart.CellValidating += Cart_CellValidating;
            _cart.CellEndEdit += (s, e) => UpdateTotals();
            _cart.DataError += (s, e) => { Ui.Warn("Please enter a valid number."); e.Cancel = true; };
            _discount.ValueChanged += (s, e) => UpdateTotals();

            // Left: product picker
            var left = new Panel();
            _products.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) AddToCart(); };
            _products.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = true; AddToCart(); } };
            var picker = Ui.Group("1. Choose products", new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                Controls =
                {
                    Ui.Row(Ui.Lbl("Search:"), _search),
                    Ui.Row(Ui.Lbl("Qty:"), _addQty, Ui.Btn("Add to cart  ›", (s, e) => AddToCart(), 130, primary: true)),
                    Ui.Lbl("Tip: double-click a product or press Enter to add it."),
                },
            });
            Ui.Stack(left, _products, picker);

            // Right: cart + checkout
            var right = new Panel { BackColor = Theme.Background };
            var checkout = Ui.Group("3. Checkout", new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                Controls =
                {
                    Ui.FieldGrid(1, ("Customer:", _customer), ("Notes:", _notes), ("Subtotal:", _subtotal), ("Discount:", _discount)),
                    Ui.Row(Ui.Lbl("TOTAL", bold: true), _total),
                    Ui.Row(
                        Ui.Btn("✔  Complete sale", (s, e) => CompleteSale(), 170, primary: true),
                        Ui.Btn("Remove item", (s, e) => RemoveSelected()),
                        Ui.Btn("Clear cart", (s, e) => { if (_cartTable.Rows.Count == 0 || Ui.Confirm("Remove all items from the cart?")) ClearCart(); }, danger: true)),
                },
            });
            checkout.Dock = DockStyle.Bottom;
            var cartCard = Ui.GridCard(_cart, Session.IsAdmin ? "2. Cart   (edit Qty or Unit Price in the list)" : "2. Cart   (edit Qty in the list)");
            cartCard.Dock = DockStyle.Fill;
            right.Controls.Add(cartCard);
            right.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 12 });
            right.Controls.Add(checkout);

            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterWidth = 16, BackColor = Theme.Background };
            left.Dock = right.Dock = DockStyle.Fill;
            split.Panel1.Controls.Add(left);
            split.Panel2.Controls.Add(right);
            Controls.Add(split);
            BackColor = Theme.Background;
            Load += (s, e) => split.SplitterDistance = (int)(Width * 0.46);

            _searchDelay.Tick += (s, e) => { _searchDelay.Stop(); LoadProducts(); };
            _search.TextChanged += (s, e) => { _searchDelay.Stop(); _searchDelay.Start(); };
            _search.KeyDown += (s, e) => { if (e.KeyCode == Keys.Down) { _products.Focus(); e.Handled = true; } };
        }

        public void RefreshData()
        {
            Ui.Try("load customers", () =>
            {
                string typed = _customer.Text;
                Ui.BindCombo(_customer, Db.Query("SELECT CustomerID, Name FROM Customers WHERE IsActive=1 ORDER BY Name"), "Name", "CustomerID", "Walk-in customer");
                if (!string.IsNullOrEmpty(typed)) _customer.Text = typed;
            });
            LoadProducts();
        }

        void LoadProducts()
        {
            string q = _search.Text.Trim();
            Ui.Try("load products", () =>
            {
                _products.DataSource = Db.Query(@"
                    SELECT ProductID, ProductName, Category, Quantity AS InStock, SalePrice
                    FROM Products
                    WHERE IsActive = 1 AND (@q = '' OR ProductName LIKE @like OR Category LIKE @like)
                    ORDER BY ProductName", Db.P("@q", q), Db.P("@like", "%" + q + "%"));
            });
        }

        void AddToCart()
        {
            var p = Ui.SelectedRow(_products);
            if (p == null) { Ui.Info("Select a product first."); return; }
            int id = (int)p["ProductID"], inStock = (int)p["InStock"], qty = (int)_addQty.Value;
            var existing = _cartTable.Rows.Cast<DataRow>().FirstOrDefault(r => (int)r["ProductID"] == id);
            int already = existing == null ? 0 : (int)existing["Qty"];
            if (already + qty > inStock)
            {
                Ui.Warn("Not enough stock for \"" + p["ProductName"] + "\".\nIn stock: " + inStock + (already > 0 ? ", already in cart: " + already : ""));
                return;
            }
            if (existing != null)
                existing["Qty"] = already + qty;
            else
                _cartTable.Rows.Add(id, p["ProductName"], inStock, qty, p["SalePrice"] == DBNull.Value ? 0m : p["SalePrice"]);
            _addQty.Value = 1;
            UpdateTotals();
        }

        void Cart_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            string col = _cart.Columns[e.ColumnIndex].Name;
            if (!_cart.IsCurrentCellInEditMode) return;
            var row = ((DataRowView)_cart.Rows[e.RowIndex].DataBoundItem).Row;
            if (col == "Qty")
            {
                if (!int.TryParse(Convert.ToString(e.FormattedValue), out int q) || q < 1)
                {
                    Ui.Warn("Quantity must be a whole number of 1 or more.");
                    e.Cancel = true;
                }
                else if (q > (int)row["InStock"])
                {
                    Ui.Warn("Only " + row["InStock"] + " in stock.");
                    e.Cancel = true;
                }
            }
            else if (col == "UnitPrice")
            {
                if (!decimal.TryParse(Convert.ToString(e.FormattedValue), out decimal price) || price < 0)
                {
                    Ui.Warn("Enter a valid price.");
                    e.Cancel = true;
                }
            }
        }

        decimal Subtotal => _cartTable.Rows.Cast<DataRow>().Sum(r => (decimal)r["LineTotal"]);

        void UpdateTotals()
        {
            decimal sub = Subtotal;
            _subtotal.Text = sub.ToString(Ui.MoneyFormat);
            _total.Text = Math.Max(0, sub - _discount.Value).ToString(Ui.MoneyFormat);
        }

        void RemoveSelected()
        {
            var r = Ui.SelectedRow(_cart);
            if (r == null) return;
            r.Delete();
            _cartTable.AcceptChanges();
            UpdateTotals();
        }

        void ClearCart()
        {
            _cartTable.Rows.Clear();
            _discount.Value = 0;
            _notes.Clear();
            if (_customer.Items.Count > 0) _customer.SelectedIndex = 0;
            UpdateTotals();
        }

        void CompleteSale()
        {
            _cart.EndEdit();
            if (_cartTable.Rows.Count == 0) { Ui.Warn("The cart is empty. Add at least one product."); return; }
            decimal sub = Subtotal, discount = _discount.Value;
            if (discount > sub) { Ui.Warn("The discount can't be larger than the subtotal."); return; }

            // A customer picked from the list is linked by ID; any other typed name is stored as text.
            int? customerId = _customer.SelectedIndex > 0 ? Ui.ComboId(_customer) : null;
            string customerName = customerId == null && _customer.Text.Trim() != "Walk-in customer" ? Ui.NullIfEmpty(_customer.Text) : null;

            int invoiceId = 0;
            var lines = _cartTable.Rows.Cast<DataRow>().ToList();
            bool ok = Ui.Try("complete the sale", () => invoiceId = Db.InTransaction((cn, tx) =>
            {
                int id;
                using (var cmd = Db.Cmd(cn, tx,
                    @"INSERT INTO Invoices (CustomerID, CustomerName, SubTotal, Discount, Total, UserID, Notes)
                      OUTPUT INSERTED.InvoiceID VALUES (@cid, @cname, @sub, @disc, @total, @uid, @notes)",
                    Db.P("@cid", customerId), Db.P("@cname", customerName), Db.P("@sub", sub), Db.P("@disc", discount),
                    Db.P("@total", sub - discount), Db.P("@uid", Session.UserID), Db.P("@notes", Ui.NullIfEmpty(_notes.Text))))
                    id = Convert.ToInt32(cmd.ExecuteScalar());

                foreach (var line in lines)
                {
                    // Only deduct if enough stock is still there (another till may have sold it meanwhile).
                    using (var upd = Db.Cmd(cn, tx,
                        "UPDATE Products SET Quantity = Quantity - @q WHERE ProductID = @pid AND IsActive = 1 AND Quantity >= @q",
                        Db.P("@pid", line["ProductID"]), Db.P("@q", line["Qty"])))
                        if (upd.ExecuteNonQuery() == 0)
                            throw new InvalidOperationException("Not enough stock left for \"" + line["Product"] + "\". Nothing was saved.");
                    using (var ins = Db.Cmd(cn, tx,
                        @"INSERT INTO Sales (ProductID, QuantitySold, InvoiceID, UnitPrice, UnitCost, UserID)
                          SELECT @pid, @q, @inv, @price, PurchasePrice, @uid FROM Products WHERE ProductID = @pid",
                        Db.P("@pid", line["ProductID"]), Db.P("@q", line["Qty"]), Db.P("@inv", id),
                        Db.P("@price", line["UnitPrice"]), Db.P("@uid", Session.UserID)))
                        ins.ExecuteNonQuery();
                }
                return id;
            }));

            LoadProducts(); // stock levels changed (or changed elsewhere, if the sale failed)
            if (!ok) return;

            ClearCart();
            SaleCompleted?.Invoke(this, EventArgs.Empty);
            if (Ui.Confirm("Sale saved as Invoice #" + invoiceId + "  (total " + (sub - discount).ToString(Ui.MoneyFormat) + ").\n\nShow / print the receipt?"))
                Receipt.Show(FindForm(), invoiceId);
            _search.Focus();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _searchDelay.Dispose();
            base.Dispose(disposing);
        }
    }
}
