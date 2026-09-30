/* Star Electronics IMS - database upgrade to schema version 2
   Adds users/roles, suppliers, customers, invoices and price history.
   Only ADDs tables/columns; existing data is kept. Safe to run more than once.
   The application runs this automatically on startup if needed. */

IF OBJECT_ID('dbo.SchemaInfo') IS NULL
    CREATE TABLE dbo.SchemaInfo (Version int NOT NULL, AppliedAt datetime NOT NULL DEFAULT GETDATE());
GO

/* ---------- Users ---------- */
IF OBJECT_ID('dbo.Users') IS NULL
CREATE TABLE dbo.Users (
    UserID       int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Username     nvarchar(50)  NOT NULL CONSTRAINT UQ_Users_Username UNIQUE,
    PasswordHash varbinary(64) NOT NULL,
    PasswordSalt varbinary(32) NOT NULL,
    FullName     nvarchar(100) NULL,
    Role         nvarchar(20)  NOT NULL CONSTRAINT CK_Users_Role CHECK (Role IN ('Admin','Staff')),
    IsActive     bit           NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT 1,
    CreatedAt    datetime      NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT GETDATE()
);
GO

/* ---------- Suppliers ---------- */
IF OBJECT_ID('dbo.Suppliers') IS NULL
CREATE TABLE dbo.Suppliers (
    SupplierID    int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Name          nvarchar(150) NOT NULL,
    ContactPerson nvarchar(100) NULL,
    Phone         nvarchar(30)  NULL,
    Email         nvarchar(100) NULL,
    Address       nvarchar(250) NULL,
    IsActive      bit           NOT NULL CONSTRAINT DF_Suppliers_IsActive DEFAULT 1,
    CreatedAt     datetime      NOT NULL CONSTRAINT DF_Suppliers_CreatedAt DEFAULT GETDATE()
);
GO

/* ---------- Customers ---------- */
IF OBJECT_ID('dbo.Customers') IS NULL
CREATE TABLE dbo.Customers (
    CustomerID int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Name       nvarchar(150) NOT NULL,
    Phone      nvarchar(30)  NULL,
    Email      nvarchar(100) NULL,
    Address    nvarchar(250) NULL,
    IsActive   bit           NOT NULL CONSTRAINT DF_Customers_IsActive DEFAULT 1,
    CreatedAt  datetime      NOT NULL CONSTRAINT DF_Customers_CreatedAt DEFAULT GETDATE()
);
GO

/* ---------- Invoices (one per sale transaction; lines live in Sales) ---------- */
IF OBJECT_ID('dbo.Invoices') IS NULL
CREATE TABLE dbo.Invoices (
    InvoiceID    int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CustomerID   int           NULL CONSTRAINT FK_Invoices_Customers REFERENCES dbo.Customers(CustomerID),
    CustomerName nvarchar(150) NULL,
    InvoiceDate  datetime      NOT NULL CONSTRAINT DF_Invoices_Date DEFAULT GETDATE(),
    SubTotal     decimal(14,2) NOT NULL,
    Discount     decimal(14,2) NOT NULL CONSTRAINT DF_Invoices_Discount DEFAULT 0,
    Total        decimal(14,2) NOT NULL,
    UserID       int           NULL CONSTRAINT FK_Invoices_Users REFERENCES dbo.Users(UserID),
    Notes        nvarchar(250) NULL
);
GO

/* ---------- New columns on existing tables ---------- */
IF COL_LENGTH('dbo.Products', 'IsActive') IS NULL
    ALTER TABLE dbo.Products ADD IsActive bit NOT NULL CONSTRAINT DF_Products_IsActive DEFAULT 1;
IF COL_LENGTH('dbo.Products', 'ReorderLevel') IS NULL
    ALTER TABLE dbo.Products ADD ReorderLevel int NOT NULL CONSTRAINT DF_Products_ReorderLevel DEFAULT 5;
IF COL_LENGTH('dbo.Products', 'SupplierID') IS NULL
    ALTER TABLE dbo.Products ADD SupplierID int NULL CONSTRAINT FK_Products_Suppliers REFERENCES dbo.Suppliers(SupplierID);

IF COL_LENGTH('dbo.Sales', 'InvoiceID') IS NULL
    ALTER TABLE dbo.Sales ADD InvoiceID int NULL CONSTRAINT FK_Sales_Invoices REFERENCES dbo.Invoices(InvoiceID);
IF COL_LENGTH('dbo.Sales', 'UnitPrice') IS NULL
    ALTER TABLE dbo.Sales ADD UnitPrice decimal(10,2) NULL;
IF COL_LENGTH('dbo.Sales', 'UnitCost') IS NULL
    ALTER TABLE dbo.Sales ADD UnitCost decimal(10,2) NULL;
IF COL_LENGTH('dbo.Sales', 'UserID') IS NULL
    ALTER TABLE dbo.Sales ADD UserID int NULL CONSTRAINT FK_Sales_Users REFERENCES dbo.Users(UserID);

IF COL_LENGTH('dbo.Purchases', 'SupplierID') IS NULL
    ALTER TABLE dbo.Purchases ADD SupplierID int NULL CONSTRAINT FK_Purchases_Suppliers REFERENCES dbo.Suppliers(SupplierID);
IF COL_LENGTH('dbo.Purchases', 'UnitCost') IS NULL
    ALTER TABLE dbo.Purchases ADD UnitCost decimal(10,2) NULL;
IF COL_LENGTH('dbo.Purchases', 'UserID') IS NULL
    ALTER TABLE dbo.Purchases ADD UserID int NULL CONSTRAINT FK_Purchases_Users REFERENCES dbo.Users(UserID);
GO

/* ---------- Backfill prices on old history rows from current product prices ---------- */
UPDATE s SET s.UnitPrice = ISNULL(s.UnitPrice, p.SalePrice), s.UnitCost = ISNULL(s.UnitCost, p.PurchasePrice)
FROM dbo.Sales s JOIN dbo.Products p ON p.ProductID = s.ProductID
WHERE s.UnitPrice IS NULL OR s.UnitCost IS NULL;

UPDATE pu SET pu.UnitCost = p.PurchasePrice
FROM dbo.Purchases pu JOIN dbo.Products p ON p.ProductID = pu.ProductID
WHERE pu.UnitCost IS NULL;
GO

/* ---------- Indexes for history/report queries ---------- */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Sales_SaleDate')
    CREATE INDEX IX_Sales_SaleDate ON dbo.Sales(SaleDate);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Sales_InvoiceID')
    CREATE INDEX IX_Sales_InvoiceID ON dbo.Sales(InvoiceID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Purchases_PurchaseDate')
    CREATE INDEX IX_Purchases_PurchaseDate ON dbo.Purchases(PurchaseDate);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Invoices_InvoiceDate')
    CREATE INDEX IX_Invoices_InvoiceDate ON dbo.Invoices(InvoiceDate);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaInfo WHERE Version = 2)
    INSERT INTO dbo.SchemaInfo (Version) VALUES (2);
GO
