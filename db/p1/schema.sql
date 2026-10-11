/* =====================================================================
   Проєкт №1 — EnrollmentsDb (SQL Server, далі ADO.NET + Dapper у Лаб. 02)
   Контекст «Enrollments»: замовлення студентів на курси та їх оплата.

   Порядок запуску: schema.sql -> procedures.sql -> seed.sql
   Скрипт ідемпотентний: повторний запуск не змінює вже створені об'єкти.

   Первинні ключі — BIGINT IDENTITY: ідентифікатори генерує одне джерело
   (ця база), вставка послідовна, кластерний індекс не фрагментується і
   ключ удвічі компактніший за GUID. GUID знадобився б, якби Id
   створювали клієнти чи кілька сервісів незалежно один від одного.
   ===================================================================== */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;   -- обов'язково для фільтрованих індексів
GO

IF DB_ID(N'EnrollmentsDb') IS NULL
    CREATE DATABASE EnrollmentsDb;
GO

USE EnrollmentsDb;
GO

/* ---------------------------------------------------------------------
   Students — власник даних про студента-покупця (аналог Buyer в eShop).
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Students', N'U') IS NULL
CREATE TABLE dbo.Students (
    Id          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Students PRIMARY KEY,
    Email       NVARCHAR(256) NOT NULL CONSTRAINT UQ_Students_Email UNIQUE,
    FullName    NVARCHAR(200) NOT NULL,
    Phone       NVARCHAR(20)  NULL,
    -- аудит і м'яке видалення
    CreatedAt   DATETIME2(3)  NOT NULL CONSTRAINT DF_Students_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy   NVARCHAR(100) NULL,
    UpdatedAt   DATETIME2(3)  NULL,
    UpdatedBy   NVARCHAR(100) NULL,
    IsDeleted   BIT           NOT NULL CONSTRAINT DF_Students_IsDeleted DEFAULT 0,
    -- оптимістична конкурентність: профіль можуть одночасно редагувати студент і адміністратор
    RowVersion  ROWVERSION    NOT NULL,
    CONSTRAINT CK_Students_Email CHECK (Email LIKE N'_%@_%._%')
);
GO

/* ---------------------------------------------------------------------
   Courses — ЛОКАЛЬНА КОПІЯ довідника курсів із CoursesDb (Проєкт №2).
   Власник даних — сервіс Courses. Цю таблицю змінює лише обробник подій
   CoursePublished / CourseUpdated / CourseUnpublished (Лаб. 08); сам
   сервіс Enrollments її тільки читає, тому подвійного запису немає.
   Id — БЕЗ IDENTITY: це той самий Id, що в CoursesDb.dbo.Courses.
   FOREIGN KEY на іншу базу не створюється.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Courses', N'U') IS NULL
CREATE TABLE dbo.Courses (
    Id          BIGINT        NOT NULL CONSTRAINT PK_Courses PRIMARY KEY,
    Title       NVARCHAR(200) NOT NULL,                 -- копія Courses.Title з CoursesDb
    Price       DECIMAL(18,2) NOT NULL                  -- копія Courses.Price з CoursesDb
                CONSTRAINT CK_Courses_Price CHECK (Price >= 0),
    IsAvailable BIT           NOT NULL CONSTRAINT DF_Courses_IsAvailable DEFAULT 1,  -- копія Courses.IsPublished
    SyncedAt    DATETIME2(3)  NOT NULL CONSTRAINT DF_Courses_SyncedAt DEFAULT SYSUTCDATETIME(),  -- час останньої події з Каталогу
    CreatedAt   DATETIME2(3)  NOT NULL CONSTRAINT DF_Courses_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy   NVARCHAR(100) NULL,
    UpdatedAt   DATETIME2(3)  NULL,
    UpdatedBy   NVARCHAR(100) NULL,
    IsDeleted   BIT           NOT NULL CONSTRAINT DF_Courses_IsDeleted DEFAULT 0,
    -- дві події про один курс можуть оброблятися паралельно
    RowVersion  ROWVERSION    NOT NULL
);
GO

/* ---------------------------------------------------------------------
   Orders — корінь агрегату «Замовлення».          1:N  Students -> Orders
   FK на Students без каскаду (NO ACTION): студента з історією замовлень
   фізично видалити не можна, для цього є м'яке видалення.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Orders', N'U') IS NULL
CREATE TABLE dbo.Orders (
    Id          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Orders PRIMARY KEY,
    StudentId   BIGINT        NOT NULL CONSTRAINT FK_Orders_Students REFERENCES dbo.Students(Id),
    Status      NVARCHAR(30)  NOT NULL CONSTRAINT DF_Orders_Status DEFAULT N'Submitted'
                CONSTRAINT CK_Orders_Status CHECK (Status IN (
                    N'Submitted', N'AwaitingPayment', N'Paid',
                    N'AccessGranted', N'Cancelled', N'Refunded')),
    OrderDate   DATETIME2(3)  NOT NULL CONSTRAINT DF_Orders_OrderDate DEFAULT SYSUTCDATETIME(),
    TotalAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Orders_TotalAmount DEFAULT 0
                CONSTRAINT CK_Orders_TotalAmount CHECK (TotalAmount >= 0),
    CreatedAt   DATETIME2(3)  NOT NULL CONSTRAINT DF_Orders_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy   NVARCHAR(100) NULL,
    UpdatedAt   DATETIME2(3)  NULL,
    UpdatedBy   NVARCHAR(100) NULL,
    IsDeleted   BIT           NOT NULL CONSTRAINT DF_Orders_IsDeleted DEFAULT 0,
    -- статус одночасно можуть міняти платіжний обробник і студент (скасування)
    RowVersion  ROWVERSION    NOT NULL
);
GO

/* ---------------------------------------------------------------------
   OrderDetails — платіжні реквізити замовлення.   1:1  Orders <-> OrderDetails
   Зовнішній ключ OrderId є водночас первинним ключем.
   Каскад доречний: реквізити не існують без замовлення.
   М'якого видалення немає — рядок живе й «видаляється» разом із коренем агрегату.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.OrderDetails', N'U') IS NULL
CREATE TABLE dbo.OrderDetails (
    OrderId       BIGINT        NOT NULL CONSTRAINT PK_OrderDetails PRIMARY KEY
                  CONSTRAINT FK_OrderDetails_Orders REFERENCES dbo.Orders(Id) ON DELETE CASCADE,
    BillingName   NVARCHAR(200) NOT NULL,
    BillingEmail  NVARCHAR(256) NOT NULL,
    PaymentMethod NVARCHAR(30)  NOT NULL CONSTRAINT DF_OrderDetails_PaymentMethod DEFAULT N'Card'
                  CONSTRAINT CK_OrderDetails_PaymentMethod
                  CHECK (PaymentMethod IN (N'Card', N'PayPal', N'BankTransfer')),
    Comment       NVARCHAR(1000) NULL,
    CreatedAt     DATETIME2(3)  NOT NULL CONSTRAINT DF_OrderDetails_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy     NVARCHAR(100) NULL,
    UpdatedAt     DATETIME2(3)  NULL,
    UpdatedBy     NVARCHAR(100) NULL
);
GO

/* ---------------------------------------------------------------------
   OrderItems — позиції замовлення.                M:N  Orders <-> Courses
   Проміжна таблиця з двома FK і власними полями.
   CourseTitle та UnitPrice — ЗНІМОК на момент оформлення: копіюються з
   dbo.Courses процедурою usp_AddOrderItem і надалі НЕ оновлюються, навіть
   якщо курс перейменують чи змінять ціну (як OrderItem в eShop).
   Каскад від Orders доречний (позиція не існує без замовлення); від
   Courses каскаду немає — курс, який уже купували, видаляти не можна.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.OrderItems', N'U') IS NULL
CREATE TABLE dbo.OrderItems (
    OrderId     BIGINT        NOT NULL CONSTRAINT FK_OrderItems_Orders
                REFERENCES dbo.Orders(Id) ON DELETE CASCADE,
    CourseId    BIGINT        NOT NULL CONSTRAINT FK_OrderItems_Courses
                REFERENCES dbo.Courses(Id),
    CourseTitle NVARCHAR(200) NOT NULL,                 -- знімок Courses.Title
    UnitPrice   DECIMAL(18,2) NOT NULL                  -- знімок Courses.Price
                CONSTRAINT CK_OrderItems_UnitPrice CHECK (UnitPrice >= 0),
    Discount    DECIMAL(18,2) NOT NULL CONSTRAINT DF_OrderItems_Discount DEFAULT 0,
    CreatedAt   DATETIME2(3)  NOT NULL CONSTRAINT DF_OrderItems_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy   NVARCHAR(100) NULL,
    UpdatedAt   DATETIME2(3)  NULL,
    UpdatedBy   NVARCHAR(100) NULL,
    -- складений PK: один курс входить у замовлення лише один раз
    CONSTRAINT PK_OrderItems PRIMARY KEY (OrderId, CourseId),
    CONSTRAINT CK_OrderItems_Discount CHECK (Discount >= 0 AND Discount <= UnitPrice)
);
GO

/* ---------------------------------------------------------------------
   OrderStatusHistory — журнал зміни статусів.     1:N  Orders -> OrderStatusHistory
   Таблиця лише дописується (append-only), тому UpdatedAt/IsDeleted і
   RowVersion тут не потрібні: паралельного редагування рядка не буває.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.OrderStatusHistory', N'U') IS NULL
CREATE TABLE dbo.OrderStatusHistory (
    Id        BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderStatusHistory PRIMARY KEY,
    OrderId   BIGINT        NOT NULL CONSTRAINT FK_OrderStatusHistory_Orders
              REFERENCES dbo.Orders(Id) ON DELETE CASCADE,
    OldStatus NVARCHAR(30)  NULL,                       -- NULL для запису про створення
    NewStatus NVARCHAR(30)  NOT NULL,
    ChangedAt DATETIME2(3)  NOT NULL CONSTRAINT DF_OrderStatusHistory_ChangedAt DEFAULT SYSUTCDATETIME(),
    ChangedBy NVARCHAR(100) NULL,
    Comment   NVARCHAR(500) NULL
);
GO

/* =====================================================================
   ІНДЕКСИ — кожен створено під конкретний запит.
   Пошук студента за Email уже обслуговує унікальний індекс UQ_Students_Email,
   позиції замовлення — кластерний PK_OrderItems (OrderId, CourseId).
   dbo.Courses читається лише за первинним ключем, тому додаткових
   індексів на ній свідомо немає.
   ===================================================================== */

-- Запит: «мої замовлення» з необов'язковим фільтром за статусом (usp_GetStudentOrders):
--   WHERE StudentId = @StudentId AND IsDeleted = 0 [AND Status = @Status]
-- Покриваючий (INCLUDE) і фільтрований (лише не видалені) — читання без звернення до таблиці.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_Orders_StudentId_Status' AND object_id = OBJECT_ID(N'dbo.Orders'))
    CREATE INDEX IX_Orders_StudentId_Status
        ON dbo.Orders (StudentId, Status)
        INCLUDE (OrderDate, TotalAmount)
        WHERE IsDeleted = 0;
GO

-- Запит фонового завдання: «неоплачені замовлення, старші за N годин» (нагадування / автоскасування):
--   WHERE Status = N'AwaitingPayment' AND IsDeleted = 0 AND OrderDate < @Cutoff
-- Фільтрований: містить лише замовлення, що чекають оплати, тому залишається малим.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_Orders_AwaitingPayment' AND object_id = OBJECT_ID(N'dbo.Orders'))
    CREATE INDEX IX_Orders_AwaitingPayment
        ON dbo.Orders (OrderDate)
        INCLUDE (StudentId, TotalAmount)
        WHERE Status = N'AwaitingPayment' AND IsDeleted = 0;
GO

-- Запит: «у яких замовленнях є цей курс / скільки на ньому зароблено»:
--   SELECT COUNT(*), SUM(UnitPrice - Discount) FROM OrderItems WHERE CourseId = @CourseId
-- PK починається з OrderId, тому для пошуку за CourseId потрібен окремий індекс;
-- він же прискорює перевірку FK_OrderItems_Courses.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_OrderItems_CourseId' AND object_id = OBJECT_ID(N'dbo.OrderItems'))
    CREATE INDEX IX_OrderItems_CourseId
        ON dbo.OrderItems (CourseId)
        INCLUDE (UnitPrice, Discount);
GO

-- Запит: «історія статусів замовлення у хронологічному порядку» (usp_GetOrderById):
--   WHERE OrderId = @OrderId ORDER BY ChangedAt
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_OrderStatusHistory_OrderId_ChangedAt'
                 AND object_id = OBJECT_ID(N'dbo.OrderStatusHistory'))
    CREATE INDEX IX_OrderStatusHistory_OrderId_ChangedAt
        ON dbo.OrderStatusHistory (OrderId, ChangedAt)
        INCLUDE (OldStatus, NewStatus, ChangedBy);
GO

/* =====================================================================
   Денормалізоване представлення для читання: список замовлень для
   адмін-панелі без JOIN-ів у прикладному коді.
   ===================================================================== */
CREATE OR ALTER VIEW dbo.vw_OrderSummary
AS
SELECT
    o.Id            AS OrderId,
    o.Status,
    o.OrderDate,
    o.TotalAmount,
    s.Id            AS StudentId,
    s.FullName      AS StudentName,
    s.Email         AS StudentEmail,
    d.PaymentMethod,
    (SELECT COUNT(*) FROM dbo.OrderItems AS i WHERE i.OrderId = o.Id) AS CoursesCount
FROM dbo.Orders AS o
JOIN dbo.Students AS s ON s.Id = o.StudentId
LEFT JOIN dbo.OrderDetails AS d ON d.OrderId = o.Id
WHERE o.IsDeleted = 0;
GO

/* =====================================================================
   Найменші привілеї: роль застосунку має доступ лише до цієї бази.
   Логін створюється окремо (див. README), тут — лише роль і права.
   ===================================================================== */
IF DATABASE_PRINCIPAL_ID(N'enrollments_app') IS NULL
    CREATE ROLE enrollments_app;
GO

GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::dbo TO enrollments_app;
GO

PRINT N'EnrollmentsDb: schema is ready.';
GO
