/* =====================================================================
   Проєкт №1 — EnrollmentsDb: запити перевірки (CRUD, пошук, пагінація,
   транзакційний сценарій, обмеження). Скрипт нічого не залишає в базі:
   тестове замовлення в кінці фізично видаляється.
   ===================================================================== */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

USE EnrollmentsDb;
GO

PRINT N'--- 1. Зовнішні ключі: усі посилаються на таблиці ЦІЄЇ бази ---';
SELECT fk.name AS ForeignKey,
       OBJECT_NAME(fk.parent_object_id)     AS FromTable,
       OBJECT_NAME(fk.referenced_object_id) AS ToTable,
       fk.delete_referential_action_desc    AS OnDelete
FROM sys.foreign_keys AS fk
ORDER BY FromTable, ForeignKey;
GO

PRINT N'--- 2. Індекси ---';
SELECT OBJECT_NAME(i.object_id) AS TableName, i.name AS IndexName,
       i.type_desc, i.is_unique, i.has_filter, i.filter_definition
FROM sys.indexes AS i
JOIN sys.tables AS t ON t.object_id = i.object_id
WHERE i.name IS NOT NULL
ORDER BY TableName, IndexName;
GO

PRINT N'--- 3. Пошук і пагінація: замовлення студента 1, сторінка 1 по 1 запису ---';
EXEC dbo.usp_GetStudentOrders @StudentId = 1, @PageNumber = 1, @PageSize = 1;
GO

PRINT N'--- 4. Читання: замовлення 1 з позиціями та історією ---';
EXEC dbo.usp_GetOrderById @OrderId = 1;
GO

PRINT N'--- 5. Транзакційний сценарій на тестовому замовленні ---';
DECLARE @OrderId BIGINT;

EXEC dbo.usp_CreateOrder
     @StudentId = 3, @BillingName = N'Перевірка', @BillingEmail = N'check@example.com',
     @CreatedBy = N'checks.sql', @OrderId = @OrderId OUTPUT;
PRINT CONCAT(N'Створено замовлення ', @OrderId);

-- 5.1 порожнє замовлення не можна відправити на оплату
BEGIN TRY
    EXEC dbo.usp_ChangeOrderStatus @OrderId = @OrderId, @NewStatus = N'AwaitingPayment';
    PRINT N'ПОМИЛКА ПЕРЕВІРКИ: порожнє замовлення пішло на оплату';
END TRY
BEGIN CATCH
    PRINT CONCAT(N'OK, відмова ', ERROR_NUMBER(), N': ', ERROR_MESSAGE());
END CATCH

-- 5.2 неопублікований курс (Id = 6) додати не можна
BEGIN TRY
    EXEC dbo.usp_AddOrderItem @OrderId = @OrderId, @CourseId = 6;
    PRINT N'ПОМИЛКА ПЕРЕВІРКИ: недоступний курс додано';
END TRY
BEGIN CATCH
    PRINT CONCAT(N'OK, відмова ', ERROR_NUMBER(), N': ', ERROR_MESSAGE());
END CATCH

EXEC dbo.usp_AddOrderItem @OrderId = @OrderId, @CourseId = 4, @Discount = 100;

-- 5.3 НЕДОПУСТИМИЙ ПЕРЕХІД: відкрити доступ до неоплаченого замовлення
BEGIN TRY
    EXEC dbo.usp_ChangeOrderStatus @OrderId = @OrderId, @NewStatus = N'AccessGranted';
    PRINT N'ПОМИЛКА ПЕРЕВІРКИ: доступ відкрито без оплати';
END TRY
BEGIN CATCH
    PRINT CONCAT(N'OK, відмова ', ERROR_NUMBER(), N': ', ERROR_MESSAGE());
END CATCH

-- статус після відмови не змінився
SELECT Id, Status, TotalAmount FROM dbo.Orders WHERE Id = @OrderId;

-- 5.4 оптимістична конкурентність: застарілий RowVersion відхиляється
DECLARE @StaleVersion BINARY(8) = (SELECT RowVersion FROM dbo.Orders WHERE Id = @OrderId);
EXEC dbo.usp_ChangeOrderStatus @OrderId = @OrderId, @NewStatus = N'AwaitingPayment', @ChangedBy = N'checks.sql';
BEGIN TRY
    EXEC dbo.usp_ChangeOrderStatus @OrderId = @OrderId, @NewStatus = N'Paid',
         @ExpectedRowVersion = @StaleVersion;
    PRINT N'ПОМИЛКА ПЕРЕВІРКИ: застарілу версію прийнято';
END TRY
BEGIN CATCH
    PRINT CONCAT(N'OK, відмова ', ERROR_NUMBER(), N': ', ERROR_MESSAGE());
END CATCH

-- 5.5 допустимий шлях до кінця
EXEC dbo.usp_ChangeOrderStatus @OrderId = @OrderId, @NewStatus = N'Paid',          @ChangedBy = N'checks.sql';
EXEC dbo.usp_ChangeOrderStatus @OrderId = @OrderId, @NewStatus = N'AccessGranted', @ChangedBy = N'checks.sql';

-- 5.6 скасувати після оплати не можна; видалити оплачене замовлення теж
BEGIN TRY
    EXEC dbo.usp_ChangeOrderStatus @OrderId = @OrderId, @NewStatus = N'Cancelled';
    PRINT N'ПОМИЛКА ПЕРЕВІРКИ: оплачене замовлення скасовано';
END TRY
BEGIN CATCH
    PRINT CONCAT(N'OK, відмова ', ERROR_NUMBER(), N': ', ERROR_MESSAGE());
END CATCH

BEGIN TRY
    EXEC dbo.usp_DeleteOrder @OrderId = @OrderId;
    PRINT N'ПОМИЛКА ПЕРЕВІРКИ: оплачене замовлення видалено';
END TRY
BEGIN CATCH
    PRINT CONCAT(N'OK, відмова ', ERROR_NUMBER(), N': ', ERROR_MESSAGE());
END CATCH

SELECT OldStatus, NewStatus, ChangedBy FROM dbo.OrderStatusHistory WHERE OrderId = @OrderId ORDER BY Id;

-- прибирання: каскад видаляє OrderDetails, OrderItems і OrderStatusHistory
DELETE FROM dbo.Orders WHERE Id = @OrderId;
PRINT N'Тестове замовлення видалено разом із залежними рядками (ON DELETE CASCADE).';
GO

PRINT N'--- 6. Обмеження CHECK і UNIQUE спрацьовують ---';
BEGIN TRY
    INSERT INTO dbo.Courses (Id, Title, Price) VALUES (999, N'Bad price', -1);
    PRINT N'ПОМИЛКА ПЕРЕВІРКИ: від''ємну ціну прийнято';
END TRY
BEGIN CATCH
    PRINT CONCAT(N'OK, CHECK: ', ERROR_MESSAGE());
END CATCH

BEGIN TRY
    INSERT INTO dbo.Students (Email, FullName) VALUES (N'olena.koval@example.com', N'Дублікат');
    PRINT N'ПОМИЛКА ПЕРЕВІРКИ: дубль Email прийнято';
END TRY
BEGIN CATCH
    PRINT CONCAT(N'OK, UNIQUE: ', ERROR_MESSAGE());
END CATCH
GO

PRINT N'--- 7. Представлення для читання ---';
SELECT * FROM dbo.vw_OrderSummary ORDER BY OrderId;
GO
