using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using StarElectronicsIMS.Data;
using StarElectronicsIMS.Util;

namespace StarElectronicsIMS.Tabs
{
    /// <summary>At-a-glance overview: sales KPIs, stock value, low-stock list and latest invoices.</summary>
    public class DashboardTab : UserControl, IRefreshable
    {
        readonly Label _today, _month, _third, _stock, _low;
        readonly Label _thirdCaption;
        readonly DataGridView _lowGrid = Ui.Grid("ProductID");
        readonly DataGridView _recentGrid = Ui.Grid();

        public event EventHandler LowStockClicked;

        public DashboardTab()
        {
            Font = Theme.Base;
            // Five equal-width KPI tiles that stretch with the window.
            var kpis = new TableLayoutPanel { ColumnCount = 5, RowCount = 1, Height = 96, BackColor = Color.Transparent };
            for (int i = 0; i < 5; i++) kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            kpis.ControlAdded += (s, e) =>
            {
                e.Control.Dock = DockStyle.Fill;
                e.Control.Margin = new Padding(kpis.Controls.Count == 1 ? 0 : 7, 0, kpis.Controls.Count == 5 ? 0 : 7, 0);
            };
            kpis.Controls.Add(Ui.StatCard("Today's sales", out _today, Theme.Accent));
            kpis.Controls.Add(Ui.StatCard("This month's sales", out _month));
            var third = Ui.StatCard(Session.IsAdmin ? "This month's profit" : "Invoices this month", out _third, Session.IsAdmin ? Theme.Success : (Color?)null);
            _thirdCaption = (Label)third.Controls[0];
            kpis.Controls.Add(third);
            kpis.Controls.Add(Ui.StatCard("Units in stock", out _stock));
            _stock.Font = new Font("Segoe UI Semibold", 14f);
            var lowCard = Ui.StatCard("Low-stock products", out _low, Theme.Danger);
            lowCard.Cursor = Cursors.Hand;
            foreach (Control c in new Control[] { lowCard, _low, lowCard.Controls[0] })
                c.Click += (s, e) => LowStockClicked?.Invoke(this, EventArgs.Empty);
            kpis.Controls.Add(lowCard);

            var lists = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
            lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            var lowCardList = Ui.GridCard(_lowGrid, "Needs restocking");
            var recentCard = Ui.GridCard(_recentGrid, "Latest invoices");
            lowCardList.Dock = recentCard.Dock = DockStyle.Fill;
            lowCardList.Margin = new Padding(0, 0, 8, 0);
            recentCard.Margin = new Padding(8, 0, 0, 0);
            lists.Controls.Add(lowCardList, 0, 0);
            lists.Controls.Add(recentCard, 1, 0);
            _recentGrid.CellDoubleClick += (s, e) =>
            {
                var r = Ui.SelectedRow(_recentGrid);
                if (e.RowIndex >= 0 && r != null) Receipt.Show(FindForm(), (int)r["Invoice"]);
            };

            Ui.Stack(this, lists, kpis);
        }

        public void RefreshData()
        {
            Ui.Try("load the dashboard", () =>
            {
                DateTime today = DateTime.Today, monthStart = new DateTime(today.Year, today.Month, 1);
                var t = Db.Query(@"
                    SELECT
                      (SELECT ISNULL(SUM(QuantitySold * UnitPrice), 0) FROM Sales WHERE SaleDate >= @today)
                        - (SELECT ISNULL(SUM(Discount), 0) FROM Invoices WHERE InvoiceDate >= @today) AS Today,
                      (SELECT ISNULL(SUM(QuantitySold * UnitPrice), 0) FROM Sales WHERE SaleDate >= @month)
                        - (SELECT ISNULL(SUM(Discount), 0) FROM Invoices WHERE InvoiceDate >= @month) AS Month,
                      (SELECT ISNULL(SUM(QuantitySold * ISNULL(UnitCost, 0)), 0) FROM Sales WHERE SaleDate >= @month) AS MonthCost,
                      (SELECT COUNT(*) FROM Invoices WHERE InvoiceDate >= @month) AS MonthInvoices,
                      (SELECT ISNULL(SUM(Quantity), 0) FROM Products WHERE IsActive = 1) AS Units,
                      (SELECT COUNT(*) FROM Products WHERE IsActive = 1) AS ProductCount,
                      (SELECT COUNT(*) FROM Products WHERE IsActive = 1 AND Quantity <= ReorderLevel) AS Low",
                    Db.P("@today", today), Db.P("@month", monthStart)).Rows[0];

                decimal month = (decimal)t["Month"];
                _today.Text = ((decimal)t["Today"]).ToString(Ui.MoneyFormat);
                _month.Text = month.ToString(Ui.MoneyFormat);
                if (Session.IsAdmin)
                {
                    decimal profit = month - (decimal)t["MonthCost"];
                    _third.Text = profit.ToString(Ui.MoneyFormat);
                    _third.ForeColor = profit < 0 ? Theme.Danger : Theme.Success;
                }
                else
                {
                    _third.Text = t["MonthInvoices"].ToString();
                }
                _stock.Text = Convert.ToInt64(t["Units"]).ToString("N0") + "  (" + t["ProductCount"] + " items)";
                _low.Text = t["Low"].ToString();
                _low.ForeColor = (int)t["Low"] == 0 ? Theme.Success : Theme.Danger;

                _lowGrid.DataSource = Db.Query(@"
                    SELECT ProductID, ProductName AS Product, Quantity AS InStock, ReorderLevel AS ReorderAt
                    FROM Products WHERE IsActive = 1 AND Quantity <= ReorderLevel
                    ORDER BY Quantity, ProductName");

                _recentGrid.DataSource = Db.Query(@"
                    SELECT TOP 15 i.InvoiceID AS Invoice, i.InvoiceDate AS Date,
                           COALESCE(c.Name, i.CustomerName, 'Walk-in') AS Customer, i.Total
                    FROM Invoices i LEFT JOIN Customers c ON c.CustomerID = i.CustomerID
                    ORDER BY i.InvoiceDate DESC");
            });
        }
    }
}
