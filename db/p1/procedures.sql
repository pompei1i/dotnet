/* =====================================================================
   Проєкт №1 — EnrollmentsDb: збережувані процедури (6 шт.)

     usp_CreateOrder        C   створення замовлення з реквізитами      (транзакція)
     usp_AddOrderItem       U   додавання курсу + знімок назви й ціни   (транзакція)
     usp_GetOrderById       R   замовлення, позиції та історія статусів
     usp_GetStudentOrders   R   замовлення студента з пагінацією
     usp_ChangeOrderStatus  U   транзакційна зміна статусу з перевіркою переходу
     usp_DeleteOrder        D   м'яке видалення замовлення

   Керовані помилки (THROW), які перехоплює DAL у Лаб. 02:
     50001 Order not found              50006 Course not available
     50002 Student not found            50007 Course already in the order
     50003 Status transition not allowed 50008 Invalid discount
     50004 Concurrency conflict         50009 Empty order cannot be paid
     50005 Order is not editable        50010 Order cannot be deleted

   Запускати після schema.sql. CREATE OR ALTER — повторний запуск безпечний.
   ===================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;   -- процедури змінюють таблиці з фільтрованими індексами
GO

USE EnrollmentsDb;
GO

/* ---------------------------------------------------------------------
   usp_CreateOrder — створює замовлення (Orders), реквізити (OrderDetails)
   і перший запис журналу статусів однією транзакцією: або все, або нічого.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_CreateOrder
    @StudentId     BIGINT,
    @BillingName   NVARCHAR(200),
    @BillingEmail  NVARCHAR(256),
    @PaymentMethod NVARCHAR(30)   = N'Card',
    @Comment       NVARCHAR(1000) = NULL,
    @CreatedBy     NVARCHAR(100)  = NULL,
    @OrderId       BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;                          -- будь-яка помилка відкочує транзакцію

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS (SELECT 1 FROM dbo.Students WHERE Id = @StudentId AND IsDeleted = 0)
            THROW 50002, N'Student not found', 1;

        INSERT INTO dbo.Orders (StudentId, CreatedBy)
        VALUES (@StudentId, @CreatedBy);         -- Status = 'Submitted' за DEFAULT

        SET @OrderId = SCOPE_IDENTITY();

        INSERT INTO dbo.OrderDetails (OrderId, BillingName, BillingEmail, PaymentMethod, Comment, CreatedBy)
        VALUES (@OrderId, @BillingName, @BillingEmail, @PaymentMethod, @Comment, @CreatedBy);

        INSERT INTO dbo.OrderStatusHistory (OrderId, OldStatus, NewStatus, ChangedBy, Comment)
        VALUES (@OrderId, NULL, N'Submitted', @CreatedBy, N'Order created');

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* ---------------------------------------------------------------------
   usp_AddOrderItem — додає курс у замовлення.
   Саме тут робиться ЗНІМОК із таблиці політики дублювання: CourseTitle і
   UnitPrice копіюються з локального довідника dbo.Courses і більше не
   змінюються. Додавати курси можна лише до замовлення у статусі Submitted.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_AddOrderItem
    @OrderId   BIGINT,
    @CourseId  BIGINT,
    @Discount  DECIMAL(18,2) = 0,
    @CreatedBy NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- UPDLOCK тримає рядок замовлення до кінця транзакції: статус не зміниться між перевіркою і вставкою
        DECLARE @Status NVARCHAR(30) =
            (SELECT Status FROM dbo.Orders WITH (UPDLOCK, ROWLOCK)
             WHERE Id = @OrderId AND IsDeleted = 0);

        IF @Status IS NULL
            THROW 50001, N'Order not found', 1;

        IF @Status <> N'Submitted'
            THROW 50005, N'Courses can be added only to an order in status Submitted', 1;

        DECLARE @Title NVARCHAR(200), @Price DECIMAL(18,2);

        SELECT @Title = Title, @Price = Price
        FROM dbo.Courses
        WHERE Id = @CourseId AND IsAvailable = 1 AND IsDeleted = 0;

        IF @Title IS NULL
            THROW 50006, N'Course not found or not available for purchase', 1;

        IF EXISTS (SELECT 1 FROM dbo.OrderItems WHERE OrderId = @OrderId AND CourseId = @CourseId)
            THROW 50007, N'Course is already in the order', 1;

        IF @Discount < 0 OR @Discount > @Price
            THROW 50008, N'Discount must be between 0 and the course price', 1;

        INSERT INTO dbo.OrderItems (OrderId, CourseId, CourseTitle, UnitPrice, Discount, CreatedBy)
        VALUES (@OrderId, @CourseId, @Title, @Price, @Discount, @CreatedBy);

        UPDATE dbo.Orders
        SET TotalAmount = (SELECT SUM(UnitPrice - Discount) FROM dbo.OrderItems WHERE OrderId = @OrderId),
            UpdatedAt   = SYSUTCDATETIME(),
            UpdatedBy   = @CreatedBy
        WHERE Id = @OrderId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* ---------------------------------------------------------------------
   usp_GetOrderById — повертає три набори: замовлення з реквізитами,
   його позиції та історію статусів (у Лаб. 02 читається через
   Dapper QueryMultiple). RowVersion віддається клієнту для
   оптимістичної конкурентності.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_GetOrderById
    @OrderId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT o.Id, o.StudentId, s.FullName AS StudentName, s.Email AS StudentEmail,
           o.Status, o.OrderDate, o.TotalAmount,
           d.BillingName, d.BillingEmail, d.PaymentMethod, d.Comment,
           o.CreatedAt, o.UpdatedAt, o.RowVersion
    FROM dbo.Orders AS o
    JOIN dbo.Students AS s ON s.Id = o.StudentId
    LEFT JOIN dbo.OrderDetails AS d ON d.OrderId = o.Id
    WHERE o.Id = @OrderId AND o.IsDeleted = 0;

    SELECT i.OrderId, i.CourseId, i.CourseTitle, i.UnitPrice, i.Discount,
           i.UnitPrice - i.Discount AS LineTotal
    FROM dbo.OrderItems AS i
    JOIN dbo.Orders AS o ON o.Id = i.OrderId
    WHERE i.OrderId = @OrderId AND o.IsDeleted = 0
    ORDER BY i.CourseTitle;

    SELECT h.Id, h.OldStatus, h.NewStatus, h.ChangedAt, h.ChangedBy, h.Comment
    FROM dbo.OrderStatusHistory AS h
    JOIN dbo.Orders AS o ON o.Id = h.OrderId
    WHERE h.OrderId = @OrderId AND o.IsDeleted = 0
    ORDER BY h.ChangedAt, h.Id;
END
GO

/* ---------------------------------------------------------------------
   usp_GetStudentOrders — «мої замовлення» з фільтром за статусом і
   пагінацією. Працює по індексу IX_Orders_StudentId_Status.
   TotalCount повертається в кожному рядку — для побудови пейджера.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_GetStudentOrders
    @StudentId  BIGINT,
    @Status     NVARCHAR(30) = NULL,
    @PageNumber INT = 1,
    @PageSize   INT = 20
AS
BEGIN
    SET NOCOUNT ON;

    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize < 1 OR @PageSize > 100 SET @PageSize = 20;

    SELECT o.Id, o.Status, o.OrderDate, o.TotalAmount,
           COUNT(*) OVER () AS TotalCount
    FROM dbo.Orders AS o
    WHERE o.StudentId = @StudentId
      AND o.IsDeleted = 0
      AND (@Status IS NULL OR o.Status = @Status)
    ORDER BY o.OrderDate DESC, o.Id DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END
GO

/* ---------------------------------------------------------------------
   usp_ChangeOrderStatus — ТРАНЗАКЦІЙНА ЗМІНА СТАТУСУ.

   Допустимі переходи:
     Submitted       -> AwaitingPayment | Cancelled
     AwaitingPayment -> Paid            | Cancelled
     Paid            -> AccessGranted   | Refunded
     AccessGranted, Cancelled, Refunded — кінцеві статуси.

   Скасувати можна лише до оплати; надати доступ до курсів — лише після
   оплати. Будь-який інший перехід (наприклад Submitted -> AccessGranted,
   «відкрити доступ до неоплаченого замовлення») завершується керованою
   помилкою 50003, і дані не змінюються.

   Працює в явній транзакції: зміна статусу і запис у журнал — атомарні.
   @ExpectedRowVersion — необов'язковий токен оптимістичної конкурентності:
   якщо замовлення змінили після того, як клієнт його прочитав, — помилка 50004.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_ChangeOrderStatus
    @OrderId            BIGINT,
    @NewStatus          NVARCHAR(30),
    @ChangedBy          NVARCHAR(100) = NULL,
    @Comment            NVARCHAR(500) = NULL,
    @ExpectedRowVersion BINARY(8)     = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;                          -- будь-яка помилка відкочує транзакцію

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @CurrentStatus NVARCHAR(30), @CurrentRowVersion BINARY(8);

        -- UPDLOCK блокує рядок до кінця транзакції: два паралельні виклики
        -- не зможуть одночасно прочитати старий статус і обидва його змінити
        SELECT @CurrentStatus = Status, @CurrentRowVersion = RowVersion
        FROM dbo.Orders WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @OrderId AND IsDeleted = 0;

        IF @CurrentStatus IS NULL
            THROW 50001, N'Order not found', 1;

        IF @ExpectedRowVersion IS NOT NULL AND @ExpectedRowVersion <> @CurrentRowVersion
            THROW 50004, N'Order was changed by another user. Reload it and try again', 1;

        -- матриця допустимих переходів
        IF NOT EXISTS (
            SELECT 1
            FROM (VALUES
                (N'Submitted',       N'AwaitingPayment'),
                (N'Submitted',       N'Cancelled'),
                (N'AwaitingPayment', N'Paid'),
                (N'AwaitingPayment', N'Cancelled'),
                (N'Paid',            N'AccessGranted'),
                (N'Paid',            N'Refunded')
            ) AS t (FromStatus, ToStatus)
            WHERE t.FromStatus = @CurrentStatus AND t.ToStatus = @NewStatus)
        BEGIN
            DECLARE @Message NVARCHAR(400) =
                CONCAT(N'Status transition ', @CurrentStatus, N' -> ', @NewStatus, N' is not allowed');
            THROW 50003, @Message, 1;
        END

        IF @NewStatus = N'AwaitingPayment'
           AND NOT EXISTS (SELECT 1 FROM dbo.OrderItems WHERE OrderId = @OrderId)
            THROW 50009, N'An empty order cannot be sent for payment', 1;

        UPDATE dbo.Orders
        SET Status    = @NewStatus,
            UpdatedAt = SYSUTCDATETIME(),
            UpdatedBy = @ChangedBy
        WHERE Id = @OrderId;

        INSERT INTO dbo.OrderStatusHistory (OrderId, OldStatus, NewStatus, ChangedBy, Comment)
        VALUES (@OrderId, @CurrentStatus, @NewStatus, @ChangedBy, @Comment);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* ---------------------------------------------------------------------
   usp_DeleteOrder — м'яке видалення (IsDeleted = 1), рядок залишається в
   базі для аудиту. Дозволено лише для чернетки (Submitted) або скасованого
   замовлення: оплачене чи повернене замовлення — фінансовий документ.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_DeleteOrder
    @OrderId   BIGINT,
    @DeletedBy NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Status NVARCHAR(30) =
            (SELECT Status FROM dbo.Orders WITH (UPDLOCK, ROWLOCK)
             WHERE Id = @OrderId AND IsDeleted = 0);

        IF @Status IS NULL
            THROW 50001, N'Order not found', 1;

        IF @Status NOT IN (N'Submitted', N'Cancelled')
            THROW 50010, N'Only a submitted or cancelled order can be deleted', 1;

        UPDATE dbo.Orders
        SET IsDeleted = 1,
            UpdatedAt = SYSUTCDATETIME(),
            UpdatedBy = @DeletedBy
        WHERE Id = @OrderId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

PRINT N'EnrollmentsDb: procedures are ready.';
GO
