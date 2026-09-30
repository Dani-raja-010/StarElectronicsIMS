using System;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using StarElectronicsIMS.Data;

namespace StarElectronicsIMS.Util
{
    /// <summary>Loads an invoice from the database and shows a print preview with a Print button.</summary>
    public static class Receipt
    {
        public const string ShopName = "INVENTORY MANAGER";

        public static void Show(IWin32Window owner, int invoiceId)
        {
            DataRow head = null;
            DataTable lines = null;
            if (!Ui.Try("load the invoice", () =>
            {
                var h = Db.Query(@"SELECT i.InvoiceID, i.InvoiceDate, i.SubTotal, i.Discount, i.Total, i.Notes,
                                          COALESCE(c.Name, i.CustomerName, 'Walk-in customer') AS Customer, c.Phone,
                                          COALESCE(u.FullName, u.Username) AS Cashier
                                   FROM Invoices i
                                   LEFT JOIN Customers c ON c.CustomerID = i.CustomerID
                                   LEFT JOIN Users u ON u.UserID = i.UserID
                                   WHERE i.InvoiceID = @id", Db.P("@id", invoiceId));
                if (h.Rows.Count == 0) throw new InvalidOperationException("Invoice #" + invoiceId + " not found.");
                head = h.Rows[0];
                lines = Db.Query(@"SELECT p.ProductName, s.QuantitySold, s.UnitPrice
                                   FROM Sales s JOIN Products p ON p.ProductID = s.ProductID
                                   WHERE s.InvoiceID = @id ORDER BY s.SaleID", Db.P("@id", invoiceId));
            })) return;

            var doc = new PrintDocument { DocumentName = "Invoice " + invoiceId };
            doc.PrintPage += (s, e) => Draw(e, head, lines);

            using (var form = new Form
            {
                Text = "Invoice #" + invoiceId,
                Size = new Size(700, 850),
                StartPosition = FormStartPosition.CenterParent,
                Icon = Program.AppIcon,
                Font = Theme.Base,
            })
            {
                var preview = new PrintPreviewControl { Dock = DockStyle.Fill, Document = doc, Zoom = 0.9, UseAntiAlias = true };
                var print = Ui.Btn("Print...", (s, e) =>
                {
                    using (var dlg = new PrintDialog { Document = doc, UseEXDialog = true })
                        if (dlg.ShowDialog(form) == DialogResult.OK)
                            Ui.Try("print", doc.Print);
                }, 110, primary: true);
                var close = Ui.Btn("Close", (s, e) => form.Close());
                var hint = Ui.Lbl("Tip: choose \"Microsoft Print to PDF\" to save the invoice as a PDF.");
                var bar = Ui.Row(print, close, hint);
                bar.Dock = DockStyle.Bottom;
                form.Controls.Add(preview);
                form.Controls.Add(bar);
                form.ShowDialog(owner);
            }
        }

        static void Draw(PrintPageEventArgs e, DataRow head, DataTable lines)
        {
            var g = e.Graphics;
            var m = e.MarginBounds;
            float x = m.Left, right = m.Right, y = m.Top;
            using (var title = new Font("Segoe UI", 18, FontStyle.Bold))
            using (var normal = new Font("Segoe UI", 10))
            using (var bold = new Font("Segoe UI", 10, FontStyle.Bold))
            using (var big = new Font("Segoe UI", 13, FontStyle.Bold))
            using (var pen = new Pen(Color.Gray, 1))
            {
                var center = new StringFormat { Alignment = StringAlignment.Center };
                var alignRight = new StringFormat { Alignment = StringAlignment.Far };
                float width = right - x;

                g.DrawString(ShopName, title, Brushes.Black, new RectangleF(x, y, width, 40), center);
                y += 40;
                g.DrawString("SALES INVOICE", bold, Brushes.Black, new RectangleF(x, y, width, 20), center);
                y += 35;

                g.DrawString("Invoice #: " + head["InvoiceID"], normal, Brushes.Black, x, y);
                g.DrawString("Date: " + ((DateTime)head["InvoiceDate"]).ToString("dd-MMM-yyyy hh:mm tt"), normal, Brushes.Black, new RectangleF(x, y, width, 20), alignRight);
                y += 20;
                g.DrawString("Customer: " + head["Customer"] + (head["Phone"] is string ph && ph.Length > 0 ? "  (" + ph + ")" : ""), normal, Brushes.Black, x, y);
                g.DrawString("Cashier: " + (head["Cashier"] as string ?? "-"), normal, Brushes.Black, new RectangleF(x, y, width, 20), alignRight);
                y += 30;

                // Columns: Item | Qty | Price | Amount
                float cQty = x + width * 0.55f, cPrice = x + width * 0.70f, cAmt = right;
                g.DrawLine(pen, x, y, right, y); y += 4;
                g.DrawString("Item", bold, Brushes.Black, x, y);
                g.DrawString("Qty", bold, Brushes.Black, new RectangleF(x, y, cQty + 40 - x, 20), alignRight);
                g.DrawString("Price", bold, Brushes.Black, new RectangleF(x, y, cPrice + 90 - x, 20), alignRight);
                g.DrawString("Amount", bold, Brushes.Black, new RectangleF(x, y, cAmt - x, 20), alignRight);
                y += 22;
                g.DrawLine(pen, x, y, right, y); y += 4;

                foreach (DataRow r in lines.Rows)
                {
                    int qty = (int)r["QuantitySold"];
                    decimal price = r["UnitPrice"] == DBNull.Value ? 0 : (decimal)r["UnitPrice"];
                    g.DrawString((string)r["ProductName"], normal, Brushes.Black, new RectangleF(x, y, cQty - x - 10, 20));
                    g.DrawString(qty.ToString(), normal, Brushes.Black, new RectangleF(x, y, cQty + 40 - x, 20), alignRight);
                    g.DrawString(price.ToString(Ui.MoneyFormat), normal, Brushes.Black, new RectangleF(x, y, cPrice + 90 - x, 20), alignRight);
                    g.DrawString((qty * price).ToString(Ui.MoneyFormat), normal, Brushes.Black, new RectangleF(x, y, cAmt - x, 20), alignRight);
                    y += 22;
                }
                g.DrawLine(pen, x, y, right, y); y += 8;

                void Total(string label, object value, Font f)
                {
                    g.DrawString(label, f, Brushes.Black, new RectangleF(x, y, cPrice + 90 - x, 24), alignRight);
                    g.DrawString(((decimal)value).ToString(Ui.MoneyFormat), f, Brushes.Black, new RectangleF(x, y, cAmt - x, 24), alignRight);
                    y += f.Height + 6;
                }
                Total("Subtotal:", head["SubTotal"], normal);
                if ((decimal)head["Discount"] != 0) Total("Discount:", head["Discount"], normal);
                Total("TOTAL:", head["Total"], big);

                if (head["Notes"] is string notes && notes.Length > 0)
                {
                    y += 10;
                    g.DrawString("Notes: " + notes, normal, Brushes.Black, new RectangleF(x, y, width, 40));
                    y += 40;
                }
                y += 20;
                g.DrawString("Thank you for shopping with us!", bold, Brushes.Black, new RectangleF(x, y, width, 20), center);
            }
        }
    }
}
