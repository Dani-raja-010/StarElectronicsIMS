using System;
using System.Windows.Forms;
using StarElectronicsIMS.Data;
using StarElectronicsIMS.Util;

namespace StarElectronicsIMS.Tabs
{
    /// <summary>Shared screen for Customers and Suppliers (same fields, different table and totals).</summary>
    public class ContactsTab : UserControl, IRefreshable
    {
        public enum Kind { Customers, Suppliers }

        readonly Kind _kind;
        readonly string _table, _idColumn, _singular;
        readonly DataGridView _grid;
        readonly TextBox _name = Ui.Txt(240);
        readonly TextBox _contactPerson = Ui.Txt(200);
        readonly TextBox _phone = Ui.Txt(160);
        readonly TextBox _email = Ui.Txt(200);
        readonly TextBox _address = Ui.Txt(420);
        readonly TextBox _search = Ui.Txt(220);
        readonly Label _count = Ui.Lbl("");
        readonly Button _update, _delete;
        readonly Timer _searchDelay = new Timer { Interval = 300 };
        int? _selectedId;

        public ContactsTab(Kind kind)
        {
            Font = Theme.Base;
            _kind = kind;
            _table = kind.ToString();
            _idColumn = kind == Kind.Customers ? "CustomerID" : "SupplierID";
            _singular = kind == Kind.Customers ? "customer" : "supplier";
            _grid = Ui.Grid(_idColumn);

            _update = Ui.Btn("Update", (s, e) => Save(isNew: false));
            _delete = Ui.Btn("Delete", (s, e) => Delete());
            _delete.Visible = Session.IsAdmin;

            var fields = kind == Kind.Suppliers
                ? Ui.FieldGrid(2, ("Name:", _name), ("Contact person:", _contactPerson), ("Phone:", _phone), ("Email:", _email), ("Address:", _address))
                : Ui.FieldGrid(2, ("Name:", _name), ("Phone:", _phone), ("Email:", _email), ("Address:", _address));
            var editor = Ui.Group((kind == Kind.Customers ? "Customer" : "Supplier") + " details", new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                Controls =
                {
                    fields,
                    Ui.Row(Ui.Btn("Add new", (s, e) => Save(isNew: true), primary: true), _update, _delete, Ui.Btn("Clear", (s, e) => ClearEditor())),
                },
            });
            var toolbar = Ui.Row(Ui.Lbl("Search:"), _search,
                Ui.Btn("Export to Excel", (s, e) => CsvExporter.Export(_grid, _table), 130), _count);
            Ui.Stack(this, _grid, editor, toolbar);

            _searchDelay.Tick += (s, e) => { _searchDelay.Stop(); RefreshData(); };
            _search.TextChanged += (s, e) => { _searchDelay.Stop(); _searchDelay.Start(); };
            _grid.CellClick += (s, e) => { if (e.RowIndex >= 0) LoadSelected(); };
            _grid.KeyUp += (s, e) => { if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down) LoadSelected(); };
            ClearEditor();
        }

        public void RefreshData()
        {
            string q = _search.Text.Trim();
            string sql = _kind == Kind.Customers
                ? @"SELECT c.CustomerID, c.Name, c.Phone, c.Email, c.Address,
                           (SELECT COUNT(*) FROM Invoices i WHERE i.CustomerID = c.CustomerID) AS Invoices,
                           (SELECT ISNULL(SUM(i.Total), 0) FROM Invoices i WHERE i.CustomerID = c.CustomerID) AS TotalSpent,
                           c.CreatedAt AS AddedOn
                    FROM Customers c
                    WHERE c.IsActive = 1 AND (@q = '' OR c.Name LIKE @like OR c.Phone LIKE @like OR c.Email LIKE @like)
                    ORDER BY c.Name"
                : @"SELECT s.SupplierID, s.Name, s.ContactPerson, s.Phone, s.Email, s.Address,
                           (SELECT COUNT(*) FROM Products p WHERE p.SupplierID = s.SupplierID AND p.IsActive = 1) AS Products,
                           (SELECT ISNULL(SUM(pu.QuantityPurchased * pu.UnitCost), 0) FROM Purchases pu WHERE pu.SupplierID = s.SupplierID) AS TotalPurchased,
                           s.CreatedAt AS AddedOn
                    FROM Suppliers s
                    WHERE s.IsActive = 1 AND (@q = '' OR s.Name LIKE @like OR s.ContactPerson LIKE @like OR s.Phone LIKE @like OR s.Email LIKE @like)
                    ORDER BY s.Name";
            Ui.Try("load " + _table.ToLower(), () =>
            {
                var dt = Db.Query(sql, Db.P("@q", q), Db.P("@like", "%" + q + "%"));
                _grid.DataSource = dt;
                _count.Text = dt.Rows.Count + " " + _table.ToLower();
            });
            _grid.ClearSelection();
        }

        void LoadSelected()
        {
            var r = Ui.SelectedRow(_grid);
            if (r == null) return;
            _selectedId = (int)r[_idColumn];
            _name.Text = (string)r["Name"];
            _phone.Text = r["Phone"] as string ?? "";
            _email.Text = r["Email"] as string ?? "";
            _address.Text = r["Address"] as string ?? "";
            if (_kind == Kind.Suppliers) _contactPerson.Text = r["ContactPerson"] as string ?? "";
            _update.Enabled = _delete.Enabled = true;
        }

        void ClearEditor()
        {
            _selectedId = null;
            foreach (var t in new[] { _name, _contactPerson, _phone, _email, _address }) t.Clear();
            _update.Enabled = _delete.Enabled = false;
            _grid.ClearSelection();
        }

        void Save(bool isNew)
        {
            if (string.IsNullOrWhiteSpace(_name.Text)) { Ui.Warn("Enter a name."); _name.Focus(); return; }
            if (!isNew && _selectedId == null) { Ui.Info("Select a " + _singular + " in the list first."); return; }

            var ps = new SqlParams
            {
                Db.P("@name", _name.Text.Trim()),
                Db.P("@phone", Ui.NullIfEmpty(_phone.Text)),
                Db.P("@email", Ui.NullIfEmpty(_email.Text)),
                Db.P("@addr", Ui.NullIfEmpty(_address.Text)),
            };
            string sql;
            if (_kind == Kind.Customers)
                sql = isNew
                    ? "INSERT INTO Customers (Name, Phone, Email, Address) VALUES (@name, @phone, @email, @addr)"
                    : "UPDATE Customers SET Name=@name, Phone=@phone, Email=@email, Address=@addr WHERE CustomerID=@id";
            else
            {
                ps.Add(Db.P("@contact", Ui.NullIfEmpty(_contactPerson.Text)));
                sql = isNew
                    ? "INSERT INTO Suppliers (Name, ContactPerson, Phone, Email, Address) VALUES (@name, @contact, @phone, @email, @addr)"
                    : "UPDATE Suppliers SET Name=@name, ContactPerson=@contact, Phone=@phone, Email=@email, Address=@addr WHERE SupplierID=@id";
            }
            if (!isNew) ps.Add(Db.P("@id", _selectedId));

            if (Ui.Try("save the " + _singular, () => Db.Execute(sql, ps.ToArray())))
            {
                ClearEditor();
                RefreshData();
            }
        }

        void Delete()
        {
            if (_selectedId == null) { Ui.Info("Select a " + _singular + " in the list first."); return; }
            string name = _name.Text.Trim();
            string usageSql = _kind == Kind.Customers
                ? "SELECT COUNT(*) FROM Invoices WHERE CustomerID=@id"
                : "SELECT (SELECT COUNT(*) FROM Purchases WHERE SupplierID=@id) + (SELECT COUNT(*) FROM Products WHERE SupplierID=@id)";
            int used = 0;
            if (!Ui.Try("check history", () => used = Convert.ToInt32(Db.Scalar(usageSql, Db.P("@id", _selectedId))))) return;

            string sql;
            if (used > 0)
            {
                if (!Ui.Confirm("\"" + name + "\" is linked to " + used + " record(s), so it will be hidden from lists instead of deleted " +
                                "(history and reports keep the name). Continue?")) return;
                sql = "UPDATE " + _table + " SET IsActive = 0 WHERE " + _idColumn + " = @id";
            }
            else
            {
                if (!Ui.Confirm("Permanently delete " + _singular + " \"" + name + "\"?")) return;
                sql = "DELETE FROM " + _table + " WHERE " + _idColumn + " = @id";
            }
            if (Ui.Try("delete the " + _singular, () => Db.Execute(sql, Db.P("@id", _selectedId))))
            {
                ClearEditor();
                RefreshData();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _searchDelay.Dispose();
            base.Dispose(disposing);
        }
    }
}
