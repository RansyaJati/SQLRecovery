USE SqlRecTest;
GO

IF OBJECT_ID('dbo.Customers','U') IS NOT NULL DROP TABLE dbo.Customers;
IF OBJECT_ID('dbo.OrderLines','U') IS NOT NULL DROP TABLE dbo.OrderLines;
IF OBJECT_ID('dbo.AuditLog','U') IS NOT NULL DROP TABLE dbo.AuditLog;
GO

CREATE TABLE dbo.Customers
(
    CustomerId   int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
    FullName     nvarchar(120) NOT NULL,
    Email        varchar(200) NULL,
    Phone        nvarchar(30) NULL,
    SignupDate   datetime2(3) NOT NULL,
    Balance      decimal(12,2) NOT NULL,
    IsActive     bit NOT NULL,
    ExternalId   uniqueidentifier NOT NULL,
    Notes        nvarchar(max) NULL
);
GO

CREATE TABLE dbo.OrderLines
(
    OrderLineId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderLines PRIMARY KEY,
    CustomerId  int NOT NULL,
    Sku         char(12) NOT NULL,
    Quantity    smallint NOT NULL,
    UnitPrice   money NOT NULL,
    OrderedOn   date NOT NULL,
    Discount    real NULL
);
GO

CREATE TABLE dbo.AuditLog
(
    LogId      int IDENTITY(1,1) NOT NULL,
    OccurredAt datetime NOT NULL,
    Message    varchar(255) NOT NULL,
    Payload    varbinary(64) NULL
);
GO

;WITH n AS (SELECT TOP (250) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i
            FROM sys.all_objects a CROSS JOIN sys.all_objects b)
INSERT INTO dbo.Customers (FullName, Email, Phone, SignupDate, Balance, IsActive, ExternalId, Notes)
SELECT
    N'Customer ' + CAST(i AS nvarchar(10)),
    CASE WHEN i % 7 = 0 THEN NULL ELSE 'customer' + CAST(i AS varchar(10)) + '@example.com' END,
    CASE WHEN i % 5 = 0 THEN NULL ELSE N'+62-812-' + RIGHT('0000000' + CAST(i AS varchar(10)), 7) END,
    DATEADD(day, -i, CAST('2026-10-31T08:30:00' AS datetime2(3))),
    CAST(i * 1234.56 AS decimal(12,2)),
    CASE WHEN i % 2 = 0 THEN 1 ELSE 0 END,
    NEWID(),
    CASE WHEN i % 11 = 0 THEN N'Long note — ünïcode ✓ for customer ' + CAST(i AS nvarchar(10)) ELSE NULL END
FROM n;
GO

;WITH n AS (SELECT TOP (500) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i
            FROM sys.all_objects a CROSS JOIN sys.all_objects b)
INSERT INTO dbo.OrderLines (CustomerId, Sku, Quantity, UnitPrice, OrderedOn, Discount)
SELECT
    (i % 250) + 1,
    'SKU-' + RIGHT('00000000' + CAST(i AS varchar(10)), 8),
    CAST((i % 9) + 1 AS smallint),
    CAST((i % 500) + 10.25 AS money),
    DATEADD(day, -(i % 400), CAST('2026-10-31' AS date)),
    CASE WHEN i % 4 = 0 THEN CAST(0.05 AS real) ELSE NULL END
FROM n;
GO

;WITH n AS (SELECT TOP (80) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i
            FROM sys.all_objects a CROSS JOIN sys.all_objects b)
INSERT INTO dbo.AuditLog (OccurredAt, Message, Payload)
SELECT
    DATEADD(minute, -i, CAST('2026-10-31T17:00:00' AS datetime)),
    'Audit event #' + CAST(i AS varchar(10)),
    CASE WHEN i % 3 = 0 THEN 0xDEADBEEF ELSE NULL END
FROM n;
GO

SELECT 'Customers' AS T, COUNT(*) AS Rows FROM dbo.Customers
UNION ALL SELECT 'OrderLines', COUNT(*) FROM dbo.OrderLines
UNION ALL SELECT 'AuditLog', COUNT(*) FROM dbo.AuditLog;
GO
