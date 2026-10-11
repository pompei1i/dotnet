/* =====================================================================
   Проєкт №1 — EnrollmentsDb: тестові дані.

   ІДЕМПОТЕНТНІСТЬ: перед кожною вставкою перевіряється наявність запису
   (INSERT ... WHERE NOT EXISTS за ключем), тому повторний запуск скрипта
   не створює дублікатів і не змінює вже наявні рядки.
   Id задаються явно (IDENTITY_INSERT), щоб ключ перевірки був сталим і
   щоб ті самі Id студентів і курсів використовувались у CoursesDb та
   MongoDB (логічні посилання між базами без FOREIGN KEY).

   Запускати після schema.sql і procedures.sql.
   ===================================================================== */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

USE EnrollmentsDb;
GO

/* --------------------------- Students -------------------------------- */
SET IDENTITY_INSERT dbo.Students ON;

INSERT INTO dbo.Students (Id, Email, FullName, Phone, CreatedBy)
SELECT v.Id, v.Email, v.FullName, v.Phone, N'seed'
FROM (VALUES
    (1, N'olena.koval@example.com',      N'Олена Коваль',     N'+380501112233'),
    (2, N'maksym.tkachenko@example.com', N'Максим Ткаченко',  N'+380672223344'),
    (3, N'sofiia.lysenko@example.com',   N'Софія Лисенко',    NULL),
    (4, N'dmytro.honchar@example.com',   N'Дмитро Гончар',    N'+380933334455')
) AS v (Id, Email, FullName, Phone)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Students AS s WHERE s.Id = v.Id OR s.Email = v.Email);

SET IDENTITY_INSERT dbo.Students OFF;
GO

/* ---------------------------------------------------------------------
   Courses — локальна копія довідника з CoursesDb. Id, Title і Price
   збігаються з db/p2/seed.sql. Курс 6 ще не опублікований у Каталозі,
   тому IsAvailable = 0 і додати його в замовлення не можна.
   --------------------------------------------------------------------- */
INSERT INTO dbo.Courses (Id, Title, Price, IsAvailable, CreatedBy)
SELECT v.Id, v.Title, v.Price, v.IsAvailable, N'seed'
FROM (VALUES
    (1, N'C# для початківців',                1200.00, 1),
    (2, N'ASP.NET Core Web API',              1800.00, 1),
    (3, N'Entity Framework Core на практиці', 1500.00, 1),
    (4, N'MongoDB для .NET-розробників',      1400.00, 1),
    (5, N'Мікросервіси на .NET Aspire',       2500.00, 1),
    (6, N'Docker і Kubernetes для .NET',      2000.00, 0)
) AS v (Id, Title, Price, IsAvailable)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Courses AS c WHERE c.Id = v.Id);
GO

/* ---------------------------- Orders ---------------------------------
   По одному замовленню на кожен статус життєвого циклу.
   --------------------------------------------------------------------- */
SET IDENTITY_INSERT dbo.Orders ON;

INSERT INTO dbo.Orders (Id, StudentId, Status, OrderDate, TotalAmount, CreatedAt, CreatedBy)
SELECT v.Id, v.StudentId, v.Status, v.OrderDate, v.TotalAmount, v.OrderDate, N'seed'
FROM (VALUES
    (1, 1, N'AccessGranted',   CAST('2026-09-05T10:00:00' AS DATETIME2(3)), 2500.00),
    (2, 2, N'AwaitingPayment', CAST('2026-09-20T14:30:00' AS DATETIME2(3)), 1500.00),
    (3, 3, N'Submitted',       CAST('2026-10-01T09:15:00' AS DATETIME2(3)), 1000.00),
    (4, 1, N'Cancelled',       CAST('2026-09-25T18:40:00' AS DATETIME2(3)), 1400.00),
    (5, 4, N'Paid',            CAST('2026-10-03T12:00:00' AS DATETIME2(3)), 4000.00),
    (6, 2, N'Refunded',        CAST('2026-09-10T16:20:00' AS DATETIME2(3)), 1800.00)
) AS v (Id, StudentId, Status, OrderDate, TotalAmount)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Orders AS o WHERE o.Id = v.Id);

SET IDENTITY_INSERT dbo.Orders OFF;
GO

/* ------------------------- OrderDetails (1:1) ------------------------ */
INSERT INTO dbo.OrderDetails (OrderId, BillingName, BillingEmail, PaymentMethod, Comment, CreatedBy)
SELECT v.OrderId, v.BillingName, v.BillingEmail, v.PaymentMethod, v.Comment, N'seed'
FROM (VALUES
    (1, N'Олена Коваль',    N'olena.koval@example.com',      N'Card',         NULL),
    (2, N'Максим Ткаченко', N'maksym.tkachenko@example.com', N'BankTransfer', N'Оплата від роботодавця, потрібен рахунок'),
    (3, N'Софія Лисенко',   N'sofiia.lysenko@example.com',   N'Card',         NULL),
    (4, N'Олена Коваль',    N'olena.koval@example.com',      N'PayPal',       NULL),
    (5, N'Дмитро Гончар',   N'dmytro.honchar@example.com',   N'Card',         N'Подарунок колезі'),
    (6, N'Максим Ткаченко', N'maksym.tkachenko@example.com', N'Card',         NULL)
) AS v (OrderId, BillingName, BillingEmail, PaymentMethod, Comment)
WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderDetails AS d WHERE d.OrderId = v.OrderId);
GO

/* ---------------------------------------------------------------------
   OrderItems (M:N). CourseTitle і UnitPrice — знімок.
   Замовлення 1 демонструє сенс знімка: курс 1 куплено за 1000.00, а в
   довіднику dbo.Courses він уже коштує 1200.00 — позицію це не змінило.
   --------------------------------------------------------------------- */
INSERT INTO dbo.OrderItems (OrderId, CourseId, CourseTitle, UnitPrice, Discount, CreatedBy)
SELECT v.OrderId, v.CourseId, v.CourseTitle, v.UnitPrice, v.Discount, N'seed'
FROM (VALUES
    (1, 1, N'C# для початківців',                1000.00,   0.00),
    (1, 2, N'ASP.NET Core Web API',              1800.00, 300.00),
    (2, 3, N'Entity Framework Core на практиці', 1500.00,   0.00),
    (3, 1, N'C# для початківців',                1200.00, 200.00),
    (4, 4, N'MongoDB для .NET-розробників',      1400.00,   0.00),
    (5, 5, N'Мікросервіси на .NET Aspire',       2500.00,   0.00),
    (5, 3, N'Entity Framework Core на практиці', 1500.00,   0.00),
    (6, 2, N'ASP.NET Core Web API',              1800.00,   0.00)
) AS v (OrderId, CourseId, CourseTitle, UnitPrice, Discount)
WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderItems AS i
                  WHERE i.OrderId = v.OrderId AND i.CourseId = v.CourseId);
GO

/* ------------------------ OrderStatusHistory ------------------------- */
SET IDENTITY_INSERT dbo.OrderStatusHistory ON;

INSERT INTO dbo.OrderStatusHistory (Id, OrderId, OldStatus, NewStatus, ChangedAt, ChangedBy, Comment)
SELECT v.Id, v.OrderId, v.OldStatus, v.NewStatus, v.ChangedAt, v.ChangedBy, v.Comment
FROM (VALUES
    ( 1, 1, NULL,               N'Submitted',       CAST('2026-09-05T10:00:00' AS DATETIME2(3)), N'olena.koval@example.com',      N'Order created'),
    ( 2, 1, N'Submitted',       N'AwaitingPayment', CAST('2026-09-05T10:05:00' AS DATETIME2(3)), N'olena.koval@example.com',      NULL),
    ( 3, 1, N'AwaitingPayment', N'Paid',            CAST('2026-09-05T10:12:00' AS DATETIME2(3)), N'payment-gateway',              NULL),
    ( 4, 1, N'Paid',            N'AccessGranted',   CAST('2026-09-05T10:13:00' AS DATETIME2(3)), N'system',                       NULL),
    ( 5, 2, NULL,               N'Submitted',       CAST('2026-09-20T14:30:00' AS DATETIME2(3)), N'maksym.tkachenko@example.com', N'Order created'),
    ( 6, 2, N'Submitted',       N'AwaitingPayment', CAST('2026-09-20T14:35:00' AS DATETIME2(3)), N'maksym.tkachenko@example.com', NULL),
    ( 7, 3, NULL,               N'Submitted',       CAST('2026-10-01T09:15:00' AS DATETIME2(3)), N'sofiia.lysenko@example.com',   N'Order created'),
    ( 8, 4, NULL,               N'Submitted',       CAST('2026-09-25T18:40:00' AS DATETIME2(3)), N'olena.koval@example.com',      N'Order created'),
    ( 9, 4, N'Submitted',       N'AwaitingPayment', CAST('2026-09-25T18:42:00' AS DATETIME2(3)), N'olena.koval@example.com',      NULL),
    (10, 4, N'AwaitingPayment', N'Cancelled',       CAST('2026-09-26T08:00:00' AS DATETIME2(3)), N'olena.koval@example.com',      N'Передумала'),
    (11, 5, NULL,               N'Submitted',       CAST('2026-10-03T12:00:00' AS DATETIME2(3)), N'dmytro.honchar@example.com',   N'Order created'),
    (12, 5, N'Submitted',       N'AwaitingPayment', CAST('2026-10-03T12:03:00' AS DATETIME2(3)), N'dmytro.honchar@example.com',   NULL),
    (13, 5, N'AwaitingPayment', N'Paid',            CAST('2026-10-03T12:10:00' AS DATETIME2(3)), N'payment-gateway',              NULL),
    (14, 6, NULL,               N'Submitted',       CAST('2026-09-10T16:20:00' AS DATETIME2(3)), N'maksym.tkachenko@example.com', N'Order created'),
    (15, 6, N'Submitted',       N'AwaitingPayment', CAST('2026-09-10T16:22:00' AS DATETIME2(3)), N'maksym.tkachenko@example.com', NULL),
    (16, 6, N'AwaitingPayment', N'Paid',            CAST('2026-09-10T16:30:00' AS DATETIME2(3)), N'payment-gateway',              NULL),
    (17, 6, N'Paid',            N'Refunded',        CAST('2026-09-11T11:00:00' AS DATETIME2(3)), N'support',                      N'Курс придбано помилково')
) AS v (Id, OrderId, OldStatus, NewStatus, ChangedAt, ChangedBy, Comment)
WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderStatusHistory AS h WHERE h.Id = v.Id);

SET IDENTITY_INSERT dbo.OrderStatusHistory OFF;
GO

/* ------------------- Контроль: кількість рядків ----------------------
   Після будь-якої кількості запусків має бути 4 / 6 / 6 / 6 / 8 / 17.
   --------------------------------------------------------------------- */
SELECT 'Students' AS TableName, COUNT(*) AS RowsCount FROM dbo.Students
UNION ALL SELECT 'Courses',            COUNT(*) FROM dbo.Courses
UNION ALL SELECT 'Orders',             COUNT(*) FROM dbo.Orders
UNION ALL SELECT 'OrderDetails',       COUNT(*) FROM dbo.OrderDetails
UNION ALL SELECT 'OrderItems',         COUNT(*) FROM dbo.OrderItems
UNION ALL SELECT 'OrderStatusHistory', COUNT(*) FROM dbo.OrderStatusHistory;
GO
