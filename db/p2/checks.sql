/* =====================================================================
   Проєкт №2 — CoursesDb: запити перевірки (зв'язки, пошук, пагінація,
   обмеження). Скрипт нічого не змінює в базі.
   ===================================================================== */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

USE CoursesDb;
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

PRINT N'--- 3. Каталог: опубліковані курси рівня Intermediate за ціною, сторінка 1 по 2 ---';
SELECT c.Id, c.Title, c.Slug, c.Price, c.AverageRating, c.ReviewsCount
FROM dbo.Courses AS c
WHERE c.IsPublished = 1 AND c.IsDeleted = 0 AND c.Level = N'Intermediate'
ORDER BY c.Price
OFFSET 0 ROWS FETCH NEXT 2 ROWS ONLY;
GO

PRINT N'--- 4. 1:N і 1:1: курс із викладачем та деталями ---';
SELECT c.Title, i.FullName AS Instructor, d.DurationHours, d.HasCertificate
FROM dbo.Courses AS c
JOIN dbo.Instructors AS i ON i.Id = c.InstructorId
JOIN dbo.CourseDetails AS d ON d.CourseId = c.Id
WHERE c.Slug = N'aspnet-core-web-api';
GO

PRINT N'--- 5. M:N з власними полями: курси категорії «Програмування», новіші перші ---';
SELECT c.Title, cc.AddedAt, cc.IsPrimary
FROM dbo.CourseCategories AS cc
JOIN dbo.Courses AS c ON c.Id = cc.CourseId
WHERE cc.CategoryId = 1
ORDER BY cc.AddedAt DESC;
GO

PRINT N'--- 6. Пошук за початком назви ---';
SELECT Id, Title FROM dbo.Courses WHERE Title LIKE N'M%' ORDER BY Title;
GO

PRINT N'--- 7. Обмеження CHECK і UNIQUE спрацьовують ---';
BEGIN TRY
    INSERT INTO dbo.Courses (InstructorId, Title, Slug, Price) VALUES (1, N'Bad price', N'bad-price', -10);
    PRINT N'ПОМИЛКА ПЕРЕВІРКИ: від''ємну ціну прийнято';
END TRY
BEGIN CATCH
    PRINT CONCAT(N'OK, CHECK: ', ERROR_MESSAGE());
END CATCH

BEGIN TRY
    INSERT INTO dbo.Courses (InstructorId, Title, Slug, Price) VALUES (1, N'Duplicate slug', N'csharp-for-beginners', 100);
    PRINT N'ПОМИЛКА ПЕРЕВІРКИ: дубль Slug прийнято';
END TRY
BEGIN CATCH
    PRINT CONCAT(N'OK, UNIQUE: ', ERROR_MESSAGE());
END CATCH

BEGIN TRY
    -- у курсу 2 основна категорія вже є (CategoryId = 2)
    UPDATE dbo.CourseCategories SET IsPrimary = 1 WHERE CourseId = 2 AND CategoryId = 1;
    PRINT N'ПОМИЛКА ПЕРЕВІРКИ: друга основна категорія прийнята';
END TRY
BEGIN CATCH
    PRINT CONCAT(N'OK, UNIQUE filtered index: ', ERROR_MESSAGE());
END CATCH

BEGIN TRY
    -- опублікований курс без дати публікації
    UPDATE dbo.Courses SET IsPublished = 1 WHERE Id = 6;
    PRINT N'ПОМИЛКА ПЕРЕВІРКИ: курс опубліковано без PublishedAt';
END TRY
BEGIN CATCH
    PRINT CONCAT(N'OK, CHECK: ', ERROR_MESSAGE());
END CATCH
GO
