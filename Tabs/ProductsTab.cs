using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using StarElectronicsIMS.Data;
using StarElectronicsIMS.Util;

namespace StarElectronicsIMS.Tabs
{
    /// <summary>Product list with search / low-stock filter. Admins can add, edit, archive and restore products.</summary>
    public class ProductsTab : UserControl, IRefreshable
    {
        readonly DataGridView _grid = Ui.Grid("SupplierID", "IsActive");
        readonly TextBox _name = Ui.Txt(260);
        readonly ComboBox _category = Ui.Combo(170, editable: true);
        readonly ComboBox _supplier = Ui.Combo(200);
        readonly NumericUpDown _qty = Ui.Num(0, 10_000_000);
        readonly NumericUpDown _reorder = Ui.Num(0, 1_000_000);
        readonly NumericUpDown _purchasePrice = Ui.Num(0, 99_999_999, 2);
        readonly NumericUpDown _salePrice = Ui.Num(0, 99_999_999, 2);
        readonly TextBox _search = Ui.Txt(220);
        readonly CheckBox _lowOnly = Ui.Check("Low stock only");
        readonly CheckBox _showArchived = Ui.Check("Show archived");
        readonly Label _count = Ui.Lbl("");
        readonly Button _update, _delete;
        readonly Timer _searchDelay = new Timer { Interval = 300 };
        int? _selectedId;

        public ProductsTab()
        {
            Font = Theme.Base;
            _reorder.Value = 5;
            _update = Ui.Btn("Update", (s, e) => UpdateProduct());
            _delete = Ui.Btn("Archive", (s, e) => DeleteOrRestore(), danger: true);

            var editor = Ui.Group("Product details", new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                Controls =
                {
                    Ui.FieldGrid(3,
                        ("Name:", _name), ("Category:", _category), ("Supplier:", _supplier),
                        ("Quantity:", _qty), ("Purchase price:", _purchasePrice), ("Sale price:", _salePrice),
                        ("Reorder at:", _reorder)),
                    Ui.Row(Ui.Btn("Add new", (s, e) => AddProduct(), primary: true), _update, _delete, Ui.Btn("Clear", (s, e) => ClearEditor())),
                },
            });
            editor.Visible = Session.IsAdmin;

            var toolbar = Ui.Row(Ui.Lbl("Search:"), _search, _lowOnly, _showArchived,
                Ui.Btn("Export to Excel", (s, e) => CsvExporter.Export(_grid, "Products"), 130), _count);
            _showArchived.Visible = Session.IsAdmin;

            Ui.Stack(this, _grid, editor, toolbar);

            _searchDelay.Tick += (s, e) => { _searchDelay.Stop(); LoadGrid(); };
            _search.TextChanged += (s, e) => { _searchDelay.Stop(); _searchDelay.Start(); };
            _lowOnly.CheckedChanged += (s, e) => LoadGrid();
            _showArchived.CheckedChanged += (s, e) => LoadGrid();
            // React to the user picking a row (not to the automatic selection the grid makes when data is re-bound).
            _grid.CellClick += (s, e) => { if (e.RowIndex >= 0) LoadSelected(); };
            _grid.KeyUp += (s, e) => { if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down || e.KeyCode == Keys.PageUp || e.KeyCode == Keys.PageDown) LoadSelected(); };
            _grid.CellFormatting += Grid_CellFormatting;
            ClearEditor();
        }

        public void RefreshData()
        {
            Ui.Try("load categories and suppliers", () =>
            {
                string typed = _category.Text;
                _category.Items.Clear();
                foreach (DataRow r in Db.Query("SELECT DISTINCT Category FROM Products WHERE Category IS NOT NULL AND Category <> '' ORDER BY Category").Rows)
                    _category.Items.Add(r[0]);
                _category.Text = typed;

                var selectedSupplier = _supplier.SelectedValue;
                Ui.BindCombo(_supplier, Db.Query("SELECT SupplierID, Name FROM Suppliers WHERE IsActive=1 ORDER BY Name"), "Name", "SupplierID", "(none)");
                Ui.SelectComboId(_supplier, selectedSupplier);
            });
            LoadGrid();
        }

        /// <summary>Called from the status bar's low-stock link.</summary>
        public void ShowLowStock()
        {
            _search.Clear();
            _lowOnly.Checked = true;
            LoadGrid();
        }

        void LoadGrid()
        {
            int? keep = _selectedId;
            string q = _search.Text.Trim();
            Ui.Try("load products", () =>
            {
                var dt = Db.Query(@"
                    SELECT p.ProductID, p.ProductName, p.Category, p.Quantity, p.ReorderLevel,
                           p.PurchasePrice, p.SalePrice, p.SalePrice - p.PurchasePrice AS ProfitPerUnit,
                           s.Name AS Supplier, p.SupplierID, p.IsActive
                    FROM Products p
                    LEFT JOIN Suppliers s ON s.SupplierID = p.SupplierID
                    WHERE (@all = 1 OR p.IsActive = 1)
                      AND (@q = '' OR p.ProductName LIKE @like OR p.Category LIKE @like OR s.Name LIKE @like)
                      AND (@low = 0 OR p.Quantity <= p.ReorderLevel)
                    ORDER BY p.ProductName",
                    Db.P("@all", _showArchived.Checked), Db.P("@q", q), Db.P("@like", "%" + q + "%"), Db.P("@low", _lowOnly.Checked));
                _grid.DataSource = dt;
                if (!Session.IsAdmin && _grid.Columns.Contains("ProfitPerUnit")) _grid.Columns["ProfitPerUnit"].Visible = false;
                _count.Text = dt.Rows.Count + " product(s)";
            });
            SelectById(keep);
        }

        /// <summary>Re-selects the product being edited after a reload, or clears the editor if it is no longer listed.</summary>
        void SelectById(int? id)
        {
            if (id != null)
                foreach (DataGridViewRow row in _grid.Rows)
                    if ((int)row.Cells["ProductID"].Value == id)
                    {
                        _grid.CurrentCell = row.Cells["ProductName"];
                        LoadSelected();
                        return;
                    }
            if (id != null) ClearEditor();
            _grid.ClearSelection();
        }

        void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (!(_grid.Rows[e.RowIndex].DataBoundItem is DataRowView v)) return;
            if (!(bool)v["IsActive"])
            {
                e.CellStyle.BackColor = Theme.Inactive;
                e.CellStyle.ForeColor = Color.Gray;
            }
            else if ((int)v["Quantity"] <= (int)v["ReorderLevel"])
            {
                e.CellStyle.BackColor = Theme.LowStock;
            }
        }

        void LoadSelected()
        {
            var r = Ui.SelectedRow(_grid);
            if (r == null) return;
            _selectedId = (int)r["ProductID"];
            _name.Text = (string)r["ProductName"];
            _category.Text = r["Category"] as string ?? "";
            _qty.Value = (int)r["Quantity"];
            _reorder.Value = (int)r["ReorderLevel"];
            Ui.SetMoney(_purchasePrice, r["PurchasePrice"]);
            Ui.SetMoney(_salePrice, r["SalePrice"]);
            Ui.SelectComboId(_supplier, r["SupplierID"]);
            _update.Enabled = _delete.Enabled = true;
            _delete.Text = (bool)r["IsActive"] ? "Archive" : "Restore";
        }

        void ClearEditor()
        {
            _selectedId = null;
            _name.Clear();
            _category.Text = "";
            _qty.Value = 0;
            _reorder.Value = 5;
            _purchasePrice.Value = _salePrice.Value = 0;
            if (_supplier.Items.Count > 0) _supplier.SelectedIndex = 0;
            _update.Enabled = _delete.Enabled = false;
            _delete.Text = "Archive";
            _grid.ClearSelection();
        }

        bool ValidateInputs()
        {
            if (string.IsNullOrWhiteSpace(_name.Text)) { Ui.Warn("Enter a product name."); _name.Focus(); return false; }
            if (_salePrice.Value < _purchasePrice.Value &&
                !Ui.Confirm("The sale price is lower than the purchase price, so every sale will make a loss.\n\nSave anyway?"))
                return false;
            return true;
        }

        SqlParams Params() => new SqlParams
        {
            Db.P("@name", _name.Text.Trim()),
            Db.P("@cat", Ui.NullIfEmpty(_category.Text)),
            Db.P("@qty", (int)_qty.Value),
            Db.P("@reorder", (int)_reorder.Value),
            Db.P("@pp", _purchasePrice.Value),
            Db.P("@sp", _salePrice.Value),
            Db.P("@sup", Ui.ComboId(_supplier)),
        };

        void AddProduct()
        {
            if (!ValidateInputs()) return;
            int dupes = 0;
            if (!Ui.Try("check for duplicates", () => dupes = Convert.ToInt32(Db.Scalar(
                "SELECT COUNT(*) FROM Products WHERE ProductName = @n AND IsActive = 1", Db.P("@n", _name.Text.Trim()))))) return;
            if (dupes > 0 && !Ui.Confirm("A product named \"" + _name.Text.Trim() + "\" already exists.\n\nAdd another one anyway?")) return;

            int newId = 0;
            if (!Ui.Try("add the product", () => newId = Convert.ToInt32(Db.Scalar(
                @"INSERT INTO Products (ProductName, Category, Quantity, ReorderLevel, PurchasePrice, SalePrice, SupplierID)
                  OUTPUT INSERTED.ProductID VALUES (@name, @cat, @qty, @reorder, @pp, @sp, @sup)", Params().ToArray())))) return;
            _selectedId = newId;
            RefreshData();
        }

        void UpdateProduct()
        {
            if (_selectedId == null) { Ui.Info("Select a product in the list first."); return; }
            if (!ValidateInputs()) return;
            var ps = Params();
            ps.Add(Db.P("@id", _selectedId));
            if (Ui.Try("update the product", () => Db.Execute(
                @"UPDATE Products SET ProductName=@name, Category=@cat, Quantity=@qty, ReorderLevel=@reorder,
                         PurchasePrice=@pp, SalePrice=@sp, SupplierID=@sup
                  WHERE ProductID=@id", ps.ToArray())))
                RefreshData();
        }

        void DeleteOrRestore()
        {
            var r = Ui.SelectedRow(_grid);
            if (_selectedId == null || r == null || (int)r["ProductID"] != _selectedId) { Ui.Info("Select a product in the list first."); return; }
            int id = (int)r["ProductID"];
            string name = (string)r["ProductName"];

            if (!(bool)r["IsActive"])
            {
                if (Ui.Try("restore the product", () => Db.Execute("UPDATE Products SET IsActive=1 WHERE ProductID=@id", Db.P("@id", id))))
                    LoadGrid();
                return;
            }

            int history = 0;
            if (!Ui.Try("check product history", () => history = Convert.ToInt32(Db.Scalar(
                "SELECT (SELECT COUNT(*) FROM Sales WHERE ProductID=@id) + (SELECT COUNT(*) FROM Purchases WHERE ProductID=@id)",
                Db.P("@id", id))))) return;

            if (history > 0)
            {
                if (!Ui.Confirm("\"" + name + "\" has " + history + " sale/purchase record(s), so it can't be deleted without losing history.\n\n" +
                                "It will be ARCHIVED instead: hidden from lists and sales, but kept in reports. Continue?")) return;
                if (Ui.Try("archive the product", () => Db.Execute("UPDATE Products SET IsActive=0 WHERE ProductID=@id", Db.P("@id", id))))
                {
                    ClearEditor();
                    LoadGrid();
                }
            }
            else
            {
                if (!Ui.Confirm("Permanently delete \"" + name + "\"?")) return;
                if (Ui.Try("delete the product", () => Db.Execute("DELETE FROM Products WHERE ProductID=@id", Db.P("@id", id))))
                {
                    ClearEditor();
                    LoadGrid();
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _searchDelay.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Small list alias so parameter sets can be built with collection initializers.</summary>
    public class SqlParams : System.Collections.Generic.List<System.Data.SqlClient.SqlParameter> { }
}
