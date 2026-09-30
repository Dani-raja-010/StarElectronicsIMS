using System;
using System.Data;
using System.Data.SqlClient;

namespace StarElectronicsIMS.Data
{
    /// <summary>
    /// Queries behind the History &amp; Reports page. Every method takes a date range (<paramref name="to"/> exclusive)
    /// and an optional search text; an empty search returns everything in the range.
    /// </summary>
    /// <remarks>
    /// Search matching:
    ///  - an invoice matches on its number, customer, cashier or notes, or when any of its lines matches;
    ///  - a sale line matches on its product, category or seller, or when its invoice matches;
    ///  - a purchase matches on its number, product, category, supplier or the user who recorded it.
    /// </remarks>
    public static class HistoryData
    {
        // Invoice-level fields. Expects aliases i = Invoices, c = Customers, iu = Users (cashier).
        const string InvoiceMatch = @"
            CAST(i.InvoiceID AS nvarchar(12)) = @num
            OR COALESCE(c.Name, i.CustomerName, 'Walk-in') LIKE @like
            OR iu.FullName LIKE @like OR iu.Username LIKE @like
            OR i.Notes LIKE @like";

        // Line-level fields. Expects aliases pr = Products, su = Users (seller).
        const string LineMatch = @"
            pr.ProductName LIKE @like OR pr.Category LIKE @like
            OR su.FullName LIKE @like OR su.Username LIKE @like";

        // Filtered invoice rows (i, c, iu available to the SELECT).
        const string InvoicesFrom = @"
            FROM Invoices i
            LEFT JOIN Customers c ON c.CustomerID = i.CustomerID
            LEFT JOIN Users iu ON iu.UserID = i.UserID
            WHERE i.InvoiceDate >= @from AND i.InvoiceDate < @to
              AND (@q = '' OR " + InvoiceMatch + @"
                   OR EXISTS (SELECT 1 FROM Sales s
                              JOIN Products pr ON pr.ProductID = s.ProductID
                              LEFT JOIN Users su ON su.UserID = s.UserID
                              WHERE s.InvoiceID = i.InvoiceID AND (" + LineMatch + ")))";

        // Filtered sale lines (s, pr, su, i, c, iu available to the SELECT).
        const string SalesFrom = @"
            FROM Sales s
            JOIN Products pr ON pr.ProductID = s.ProductID
            LEFT JOIN Users su ON su.UserID = s.UserID
            LEFT JOIN Invoices i ON i.InvoiceID = s.InvoiceID
            LEFT JOIN Customers c ON c.CustomerID = i.CustomerID
            LEFT JOIN Users iu ON iu.UserID = i.UserID
            WHERE s.SaleDate >= @from AND s.SaleDate < @to
              AND (@q = '' OR " + LineMatch + " OR (i.InvoiceID IS NOT NULL AND (" + InvoiceMatch + ")))";

        // Filtered purchases (pu, pr, sup, u available to the SELECT).
        const string PurchasesFrom = @"
            FROM Purchases pu
            JOIN Products pr ON pr.ProductID = pu.ProductID
            LEFT JOIN Suppliers sup ON sup.SupplierID = pu.SupplierID
            LEFT JOIN Users u ON u.UserID = pu.UserID
            WHERE pu.PurchaseDate >= @from AND pu.PurchaseDate < @to
              AND (@q = '' OR CAST(pu.PurchaseID AS nvarchar(12)) = @num
                   OR pr.ProductName LIKE @like OR pr.Category LIKE @like OR sup.Name LIKE @like
                   OR u.FullName LIKE @like OR u.Username LIKE @like)";

        public static DataTable Invoices(DateTime from, DateTime to, string search) => Db.Query(@"
            SELECT i.InvoiceID, i.InvoiceDate, COALESCE(c.Name, i.CustomerName, 'Walk-in') AS Customer,
                   (SELECT SUM(x.QuantitySold) FROM Sales x WHERE x.InvoiceID = i.InvoiceID) AS Items,
                   i.SubTotal, i.Discount, i.Total, COALESCE(iu.FullName, iu.Username) AS Cashier, i.Notes
            " + InvoicesFrom + @"
            ORDER BY i.InvoiceDate DESC", Params(from, to, search));

        public static DataTable SaleLines(DateTime from, DateTime to, string search) => Db.Query(@"
            SELECT s.SaleDate, s.InvoiceID, pr.ProductName, pr.Category, s.QuantitySold AS Qty,
                   s.UnitPrice, s.QuantitySold * s.UnitPrice AS LineTotal,
                   s.UnitCost, s.QuantitySold * (s.UnitPrice - ISNULL(s.UnitCost, 0)) AS Profit,
                   COALESCE(su.FullName, su.Username) AS SoldBy
            " + SalesFrom + @"
            ORDER BY s.SaleDate DESC, s.SaleID DESC", Params(from, to, search));

        public static DataTable Purchases(DateTime from, DateTime to, string search) => Db.Query(@"
            SELECT pu.PurchaseID, pu.PurchaseDate, pr.ProductName, sup.Name AS Supplier,
                   pu.QuantityPurchased AS Qty, pu.UnitCost, pu.QuantityPurchased * pu.UnitCost AS TotalCost,
                   COALESCE(u.FullName, u.Username) AS RecordedBy
            " + PurchasesFrom + @"
            ORDER BY pu.PurchaseDate DESC, pu.PurchaseID DESC", Params(from, to, search));

        public static DataTable ProfitByProduct(DateTime from, DateTime to, string search) => Db.Query(@"
            SELECT pr.ProductName, pr.Category, SUM(s.QuantitySold) AS QtySold,
                   SUM(s.QuantitySold * s.UnitPrice) AS Revenue,
                   SUM(s.QuantitySold * ISNULL(s.UnitCost, 0)) AS Cost,
                   SUM(s.QuantitySold * (s.UnitPrice - ISNULL(s.UnitCost, 0))) AS Profit
            " + SalesFrom + @"
            GROUP BY pr.ProductID, pr.ProductName, pr.Category
            ORDER BY Revenue DESC", Params(from, to, search));

        /// <summary>One row: InvoiceCount, ItemsSold, Gross, Discounts, Cost, Purchased — over the same filtered rows as the lists.</summary>
        public static DataRow Totals(DateTime from, DateTime to, string search) => Db.Query(@"
            SELECT
              (SELECT COUNT(*) " + InvoicesFrom + @") AS InvoiceCount,
              (SELECT ISNULL(SUM(i.Discount), 0) " + InvoicesFrom + @") AS Discounts,
              (SELECT ISNULL(SUM(s.QuantitySold), 0) " + SalesFrom + @") AS ItemsSold,
              (SELECT ISNULL(SUM(s.QuantitySold * s.UnitPrice), 0) " + SalesFrom + @") AS Gross,
              (SELECT ISNULL(SUM(s.QuantitySold * ISNULL(s.UnitCost, 0)), 0) " + SalesFrom + @") AS Cost,
              (SELECT ISNULL(SUM(pu.QuantityPurchased * pu.UnitCost), 0) " + PurchasesFrom + @") AS Purchased",
            Params(from, to, search)).Rows[0];

        // A SqlParameter can belong to only one command, so each query gets a fresh set.
        static SqlParameter[] Params(DateTime from, DateTime to, string search)
        {
            string q = (search ?? "").Trim();
            return new[]
            {
                Db.P("@from", from),
                Db.P("@to", to),
                Db.P("@q", q),
                Db.P("@like", "%" + q + "%"),
                Db.P("@num", q.TrimStart('#').Trim()), // lets "#42" or "42" find invoice / purchase 42
            };
        }
    }
}
