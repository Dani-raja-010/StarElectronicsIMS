---
name: ims-skill
description: Use when working on the Star Electronics Inventory Manager — adding or changing screens (tabs/forms), writing SQL against StarElectronicsDB, changing the database schema, or touching stock, sales, purchases, invoices, users or roles.
---

# Star Electronics IMS

C# WinForms desktop app (.NET Framework 4.8, SDK-style `StarElectronicsIMS.csproj`, `LangVersion latest`) on SQL Server via plain ADO.NET. No ORM, no designer files — all UI is built in code.

## Layout

| Path | What lives there |
|---|---|
| `Program.cs` | Startup loop: connect → `Db.EnsureSchema()` → first-admin setup → login → `MainForm` → repeat on logout |
| `Data/Db.cs` | The only ADO.NET entry point: `Query`, `Execute`, `Scalar`, `InTransaction`, `Cmd`, `P` |
| `Data/Security.cs` | `Session`, `Roles`, `PasswordHasher` (PBKDF2-SHA256), `UserRepository` |
| `Data/HistoryData.cs` | Shared/complex report queries for the History tab |
| `Tabs/*Tab.cs` | One `UserControl` per sidebar page, registered in `MainForm` via `AddPage(...)` |
| `Forms/*.cs` | Dialogs (login, user edit, DB settings) |
| `Util/Ui.cs` | `Theme` colors/fonts and `Ui` control factories + message helpers |
| `Sql/se.sql` | Original v1 schema (Products, Purchases, Sales) — reference only, not run by the app |
| `Sql/upgrade_v2.sql` | v2 upgrade, embedded in the exe and run automatically by `Db.EnsureSchema()` |

## Database

Database `StarElectronicsDB`; connection string `InventoryManagerDB` in `App.config` (Windows auth by default, editable in-app via *Database settings*). Always check `Sql/se.sql` + `Sql/upgrade_v2.sql` for exact column names before writing SQL.

- **Products** `ProductID, ProductName, Category, Quantity, PurchasePrice, SalePrice, IsActive, ReorderLevel, SupplierID`
- **Sales** (invoice lines) `SaleID, ProductID, QuantitySold, SaleDate, InvoiceID, UnitPrice, UnitCost, UserID`
- **Purchases** `PurchaseID, ProductID, QuantityPurchased, PurchaseDate, SupplierID, UnitCost, UserID`
- **Invoices** `InvoiceID, CustomerID, CustomerName, InvoiceDate, SubTotal, Discount, Total, UserID, Notes`
- **Customers / Suppliers** `ID, Name, (ContactPerson), Phone, Email, Address, IsActive, CreatedAt`
- **Users** `UserID, Username, PasswordHash, PasswordSalt, FullName, Role ('Admin'|'Staff'), IsActive, CreatedAt`
- **SchemaInfo** `Version, AppliedAt`

Note the asymmetric names: `QuantitySold` vs `QuantityPurchased`, `SaleDate` vs `PurchaseDate`.

## Rules

### SQL
- Go through `Db` only. Never `new SqlConnection` elsewhere.
- Always parameterize with `Db.P("@name", value)` (it maps `null` → `DBNull`). Never concatenate user input into SQL. The only concatenation allowed is of fixed table/column names chosen in code (see `ContactsTab`).
- Anything that changes stock plus writes history must be one `Db.InTransaction((cn, tx) => ...)` using `Db.Cmd(cn, tx, ...)`, e.g. `UPDATE Products SET Quantity = Quantity + @q` together with the `INSERT INTO Purchases`.
- Record who did it: set `UserID = Session.UserID` on Sales, Purchases and Invoices.
- Store `UnitPrice`/`UnitCost` on each Sales/Purchases row at the time of the transaction. Reports use these, not the current product price.
- Lists and pickers show `WHERE IsActive = 1`; reports and history include inactive rows.
- Date ranges: `>= @from AND < @to` (exclusive upper bound).

### Deleting
Never hard-delete a row that has history. Count linked Sales/Purchases/Invoices first; if any exist, archive with `IsActive = 0`, otherwise `DELETE` after `Ui.Confirm(...)`. Follow `ProductsTab` / `ContactsTab`.

### Schema changes
- Don't edit `se.sql`; it's the historical v1 baseline.
- Add a new `Sql/upgrade_vN.sql` that's idempotent (`IF OBJECT_ID(...) IS NULL`, `IF COL_LENGTH(...) IS NULL`), only adds, and ends by inserting `N` into `SchemaInfo`.
- Embed it in the `.csproj` (`<EmbeddedResource ... LogicalName=...>`), bump `Db.SchemaVersion`, and make `EnsureSchema()` run every missing version in order. It currently loads only `upgrade_v2.sql`.
- Batches are split on `GO` lines and run in one transaction.

### Where code goes
- Simple, screen-specific CRUD: inline SQL in the tab via `Db.Query/Execute/Scalar` is the house style.
- Logic that's reused, security-sensitive, or large (multi-join report SQL, auth, passwords): a static class in `Data/`, like `HistoryData` and `UserRepository`.
- Never put password hashing or role checks in raw SQL in a tab; use `UserRepository` / `Session.IsAdmin`.

### UI
- New page: a `UserControl` implementing `IRefreshable` (`RefreshData()` reloads from DB when the page is shown), added in `MainForm` with `AddPage(navHost, icon, title, page)`. Wrap admin-only pages in `if (user.IsAdmin)`.
- Build controls with `Ui.*` factories (`Btn`, `Txt`, `Num`, `Combo`, `Grid`, `FieldGrid`, `Row`, `Group`, `Stack`, `StatCard`) and `Theme` colors/fonts. Don't hardcode colors or fonts.
- Wrap every DB call triggered by the UI in `Ui.Try("verb phrase", () => ...)`; it shows the error and returns `false`. Use `Ui.Confirm` before destructive or stock-changing actions, `Ui.Info/Warn` for messages.
- Money is `decimal`, shown with `Ui.MoneyFormat` (`N2`).
- Staff can't change prices or manage users; hide those controls when `!Session.IsAdmin`.
- Raise an event (e.g. `PurchasesTab.StockReceived`) when other pages need to refresh after a change.

### Style
Private `readonly` fields prefixed `_`, controls created in field initializers, expression-bodied members for one-liners, `/// <summary>` on public classes and non-obvious methods. Match the surrounding file.

## Build and run
`dotnet build -c Release` (or open in Visual Studio). Output `bin/Release/net48/InventoryManager.exe`. Needs a reachable SQL Server; on first run against an empty v1 DB the app upgrades it and asks for the first Admin account.
