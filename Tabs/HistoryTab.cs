using System;
using System.Data;
using System.Windows.Forms;
using StarElectronicsIMS.Data;
using StarElectronicsIMS.Util;

namespace StarElectronicsIMS.Tabs
{
    /// <summary>Invoices, sale lines and purchases for a date range and optional search, with totals. Admins also see cost and profit.</summary>
    public class HistoryTab : UserControl, IRefreshable
    {
        readonly DateTimePicker _from = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MMM-yyyy", Width = 130, Margin = new Padding(3, 4, 3, 3) };
        readonly DateTimePicker _to = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MMM-yyyy", Width = 130, Margin = new Padding(3, 4, 3, 3) };
        readonly TabControl _views = new TabControl();
        readonly DataGridView _invoices = Ui.Grid();
        readonly DataGridView _saleLines;
        readonly DataGridView _purchases = Ui.Grid("PurchaseID");
        readonly DataGridView _byProduct = Ui.Grid();
        readonly FlowLayoutPanel _summary = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = System.Drawing.Color.Transparent };
        readonly TextBox _search = Ui.Txt(220);
        readonly Timer _searchDelay = new Timer { Interval = 300 };

        public HistoryTab()
        {
            Font = Theme.Base;
            _saleLines = Session.IsAdmin ? Ui.Grid() : Ui.Grid("UnitCost", "Profit");
            _from.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            _to.Value = DateTime.Today;

            AddView("Invoices", _invoices);
            AddView("Items sold", _saleLines);
            AddView("Purchases", _purchases);
            if (Session.IsAdmin) AddView("Profit by product", _byProduct);
            _invoices.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) ShowInvoice(); };

            var filter = Ui.Row(
                Ui.Lbl("From:"), _from, Ui.Lbl("To:"), _to,
                Ui.Btn("Show", (s, e) => RefreshData(), 80, primary: true),
                Ui.Btn("Today", (s, e) => SetRange(DateTime.Today, DateTime.Today), 70),
                Ui.Btn("This month", (s, e) => SetRange(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), DateTime.Today), 95),
                Ui.Btn("This year", (s, e) => SetRange(new DateTime(DateTime.Today.Year, 1, 1), DateTime.Today), 85),
                Ui.Btn("All time", (s, e) => SetRange(new DateTime(2000, 1, 1), DateTime.Today), 80));
            var searchHint = Ui.Lbl("Invoice #, customer, product, supplier or user");
            searchHint.ForeColor = Theme.Muted;
            var searchRow = Ui.Row(Ui.Lbl("Search:"), _search, searchHint);
            var actions = Ui.Row(
                Ui.Btn("View / print invoice", (s, e) => ShowInvoice(), 160),
                Ui.Btn("Export current list to Excel", (s, e) => CsvExporter.Export(CurrentGrid, _views.SelectedTab.Text.Replace(" ", "")), 200));
            Ui.Stack(this, _views, filter, searchRow, _summary, actions);
            _views.SelectedIndexChanged += (s, e) => actions.Controls[0].Enabled = _views.SelectedIndex == 0;
            _searchDelay.Tick += (s, e) => { _searchDelay.Stop(); RefreshData(); };
            _search.TextChanged += (s, e) => { _searchDelay.Stop(); _searchDelay.Start(); };
        }

        void AddView(string title, DataGridView grid)
        {
            var page = new TabPage(title);
            page.Controls.Add(grid);
            _views.TabPages.Add(page);
        }

        DataGridView CurrentGrid => (DataGridView)_views.SelectedTab.Controls[0];

        void SetRange(DateTime from, DateTime to)
        {
            _from.Value = from;
            _to.Value = to;
            RefreshData();
        }

        public void RefreshData()
        {
            if (_from.Value.Date > _to.Value.Date) { Ui.Warn("\"From\" date is after the \"To\" date."); return; }
            DateTime from = _from.Value.Date, to = _to.Value.Date.AddDays(1); // exclusive upper bound = whole "To" day included
            string q = _search.Text.Trim();
            Ui.Try("load history", () =>
            {
                _invoices.DataSource = HistoryData.Invoices(from, to, q);
                _saleLines.DataSource = HistoryData.SaleLines(from, to, q);
                _purchases.DataSource = HistoryData.Purchases(from, to, q);
                if (Session.IsAdmin) _byProduct.DataSource = HistoryData.ProfitByProduct(from, to, q);

                var t = HistoryData.Totals(from, to, q);
                decimal gross = (decimal)t["Gross"], disc = (decimal)t["Discounts"], cost = (decimal)t["Cost"];
                decimal net = gross - disc;

                _summary.Controls.Clear();
                AddStat("Invoices", t["InvoiceCount"].ToString());
                AddStat("Items sold", t["ItemsSold"].ToString());
                AddStat("Gross sales", gross.ToString(Ui.MoneyFormat));
                AddStat("Discounts", disc.ToString(Ui.MoneyFormat));
                AddStat("Net sales", net.ToString(Ui.MoneyFormat), Theme.Accent);
                if (Session.IsAdmin)
                {
                    AddStat("Cost of goods sold", cost.ToString(Ui.MoneyFormat));
                    AddStat("Gross profit", (net - cost).ToString(Ui.MoneyFormat), net - cost < 0 ? Theme.Danger : Theme.Success);
                }
                AddStat("Stock purchased", ((decimal)t["Purchased"]).ToString(Ui.MoneyFormat));
            });
        }

        void AddStat(string label, string value, System.Drawing.Color? color = null)
        {
            var card = Ui.StatCard(label, out var v, color, 150);
            card.Height = 74;
            card.Margin = new Padding(0, 0, 10, 10);
            v.Font = Theme.CardTitle;
            v.Top = 38;
            v.Text = value;
            _summary.Controls.Add(card);
        }

        void ShowInvoice()
        {
            var r = Ui.SelectedRow(_invoices);
            if (r == null) { Ui.Info("Select an invoice first."); return; }
            Receipt.Show(FindForm(), (int)r["InvoiceID"]);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _searchDelay.Dispose();
            base.Dispose(disposing);
        }
    }
}
