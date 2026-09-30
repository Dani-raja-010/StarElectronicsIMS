using System;
using System.Data;
using System.Windows.Forms;
using StarElectronicsIMS.Data;
using StarElectronicsIMS.Util;

namespace StarElectronicsIMS.Tabs
{
    /// <summary>Receive stock from a supplier: increases product quantity and records the purchase.</summary>
    public class PurchasesTab : UserControl, IRefreshable
    {
        readonly ComboBox _product = Ui.Combo(300, editable: true);
        readonly ComboBox _supplier = Ui.Combo(220);
        readonly NumericUpDown _qty = Ui.Num(1, 10_000_000);
        readonly NumericUpDown _unitCost = Ui.Num(0, 99_999_999, 2);
        readonly CheckBox _updateCost = new CheckBox { Text = "Also set this as the product's purchase price", AutoSize = true, Checked = true, Margin = new Padding(3, 7, 3, 3) };
        readonly Label _info = Ui.Lbl("");
        readonly Label _lineTotal = Ui.Lbl("0.00", bold: true);
        readonly DataGridView _recent = Ui.Grid("PurchaseID");
        DataTable _productTable;

        public event EventHandler StockReceived;

        public PurchasesTab()
        {
            Font = Theme.Base;
            _updateCost.Visible = Session.IsAdmin; // staff can't change product prices
            _updateCost.Checked = Session.IsAdmin;

            var form = Ui.Group("Receive stock", new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                Controls =
                {
                    Ui.FieldGrid(2,
                        ("Product:", _product), ("Supplier:", _supplier),
                        ("Quantity received:", _qty), ("Unit cost:", _unitCost),
                        ("", _info), ("Line total:", _lineTotal)),
                    Ui.Row(Ui.Btn("Record purchase", (s, e) => Record(), 150, primary: true), _updateCost),
                },
            });
            var listBar = Ui.Row(Ui.Lbl("Recent purchases (last 200)", bold: true),
                Ui.Btn("Export to Excel", (s, e) => CsvExporter.Export(_recent, "Purchases"), 130));
            Ui.Stack(this, _recent, form, listBar);

            _product.SelectedIndexChanged += (s, e) => OnProductChanged();
            _qty.ValueChanged += (s, e) => UpdateLineTotal();
            _unitCost.ValueChanged += (s, e) => UpdateLineTotal();
        }

        public void RefreshData()
        {
            Ui.Try("load purchase screen", () =>
            {
                var keepProduct = _product.SelectedValue;
                _productTable = Db.Query("SELECT ProductID, ProductName, Quantity, PurchasePrice, SupplierID FROM Products WHERE IsActive=1 ORDER BY ProductName");
                Ui.BindCombo(_product, _productTable, "ProductName", "ProductID");
                if (keepProduct != null) _product.SelectedValue = keepProduct;

                var keepSupplier = _supplier.SelectedValue;
                Ui.BindCombo(_supplier, Db.Query("SELECT SupplierID, Name FROM Suppliers WHERE IsActive=1 ORDER BY Name"), "Name", "SupplierID", "(none)");
                Ui.SelectComboId(_supplier, keepSupplier);

                _recent.DataSource = Db.Query(@"
                    SELECT TOP 200 pu.PurchaseID, pu.PurchaseDate, p.ProductName, s.Name AS Supplier,
                           pu.QuantityPurchased AS Qty, pu.UnitCost, pu.QuantityPurchased * pu.UnitCost AS TotalCost,
                           COALESCE(u.FullName, u.Username) AS RecordedBy
                    FROM Purchases pu
                    JOIN Products p ON p.ProductID = pu.ProductID
                    LEFT JOIN Suppliers s ON s.SupplierID = pu.SupplierID
                    LEFT JOIN Users u ON u.UserID = pu.UserID
                    ORDER BY pu.PurchaseDate DESC, pu.PurchaseID DESC");
            });
            OnProductChanged();
        }

        DataRow SelectedProduct => (_product.SelectedItem as DataRowView)?.Row;

        void OnProductChanged()
        {
            var p = SelectedProduct;
            if (p == null) { _info.Text = ""; return; }
            _info.Text = "Currently in stock: " + p["Quantity"] + "    Current purchase price: " +
                         (p["PurchasePrice"] == DBNull.Value ? "-" : ((decimal)p["PurchasePrice"]).ToString(Ui.MoneyFormat));
            Ui.SetMoney(_unitCost, p["PurchasePrice"]);
            if (p["SupplierID"] != DBNull.Value) Ui.SelectComboId(_supplier, p["SupplierID"]);
            UpdateLineTotal();
        }

        void UpdateLineTotal() => _lineTotal.Text = (_qty.Value * _unitCost.Value).ToString(Ui.MoneyFormat);

        void Record()
        {
            var p = SelectedProduct;
            if (p == null || _product.Text != (string)p["ProductName"]) { Ui.Warn("Choose a product from the list."); return; }
            int productId = (int)p["ProductID"], qty = (int)_qty.Value;
            decimal cost = _unitCost.Value;
            int? supplierId = Ui.ComboId(_supplier);
            bool setCost = _updateCost.Visible && _updateCost.Checked;

            if (!Ui.Confirm("Add " + qty + " x \"" + p["ProductName"] + "\" to stock at " + cost.ToString(Ui.MoneyFormat) + " each?")) return;

            bool ok = Ui.Try("record the purchase", () => Db.InTransaction((cn, tx) =>
            {
                using (var upd = Db.Cmd(cn, tx,
                    "UPDATE Products SET Quantity = Quantity + @q, PurchasePrice = CASE WHEN @set = 1 THEN @c ELSE PurchasePrice END WHERE ProductID = @id",
                    Db.P("@q", qty), Db.P("@set", setCost), Db.P("@c", cost), Db.P("@id", productId)))
                    upd.ExecuteNonQuery();
                using (var ins = Db.Cmd(cn, tx,
                    "INSERT INTO Purchases (ProductID, QuantityPurchased, SupplierID, UnitCost, UserID) VALUES (@id, @q, @sup, @c, @uid)",
                    Db.P("@id", productId), Db.P("@q", qty), Db.P("@sup", supplierId), Db.P("@c", cost), Db.P("@uid", Session.UserID)))
                    ins.ExecuteNonQuery();
                return 0;
            }));
            if (!ok) return;

            _qty.Value = 1;
            RefreshData();
            StockReceived?.Invoke(this, EventArgs.Empty);
            Ui.Info("Stock updated. \"" + p["ProductName"] + "\" now has " + ((int)p["Quantity"] + qty) + " in stock.");
        }
    }
}
