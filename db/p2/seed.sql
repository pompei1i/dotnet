/* =====================================================================
   Проєкт №2 — CoursesDb: початкові довідникові та тестові дані.

   ІДЕМПОТЕНТНІСТЬ: перед кожною вставкою перевіряється наявність запису
   (INSERT ... WHERE NOT EXISTS за ключем), тому повторний запуск скрипта
   не створює дублікатів і не змінює вже наявні рядки.
   Id задаються явно (IDENTITY_INSERT): ті самі Id курсів використовують
   EnrollmentsDb (локальна копія довідника) і MongoDB (courseId) —
   це логічні посилання між базами без FOREIGN KEY.

   Запускати після schema.sql.
   ===================================================================== */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

USE CoursesDb;
GO

/* ---------------- Categories — довідникові дані ---------------------- */
SET IDENTITY_INSERT dbo.Categories ON;

INSERT INTO dbo.Categories (Id, Name, Slug, Description)
SELECT v.Id, v.Name, v.Slug, v.Description
FROM (VALUES
    (1, N'Програмування', N'programming',           N'Мови програмування та основи розробки'),
    (2, N'Веб-розробка',  N'web-development',       N'Серверна та клієнтська веб-розробка'),
    (3, N'Бази даних',    N'databases',             N'Реляційні та документні СУБД, ORM'),
    (4, N'DevOps',        N'devops',                N'Контейнери, оркестрація, CI/CD'),
    (5, N'Архітектура ПЗ', N'software-architecture', N'Мікросервіси, DDD, патерни проєктування')
) AS v (Id, Name, Slug, Description)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Categories AS c
                  WHERE c.Id = v.Id OR c.Name = v.Name OR c.Slug = v.Slug);

SET IDENTITY_INSERT dbo.Categories OFF;
GO

/* ---------------------------- Instructors ---------------------------- */
SET IDENTITY_INSERT dbo.Instructors ON;

INSERT INTO dbo.Instructors (Id, Email, FullName, Bio, CreatedBy)
SELECT v.Id, v.Email, v.FullName, v.Bio, N'seed'
FROM (VALUES
    (1, N'andrii.melnyk@example.com',    N'Андрій Мельник',   N'.NET-розробник із 10-річним досвідом, автор курсів з C# та ASP.NET Core'),
    (2, N'iryna.shevchenko@example.com', N'Ірина Шевченко',   N'Архітекторка баз даних, спеціалізується на SQL Server і MongoDB'),
    (3, N'taras.bondarenko@example.com', N'Тарас Бондаренко', N'DevOps-інженер, будує мікросервісні платформи на .NET')
) AS v (Id, Email, FullName, Bio)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Instructors AS i WHERE i.Id = v.Id OR i.Email = v.Email);

SET IDENTITY_INSERT dbo.Instructors OFF;
GO

/* ---------------------------------------------------------------------
   Courses. AverageRating і ReviewsCount відповідають колекції
   course_ratings у feedback_db (db/p3/seed.js). Курс 6 — чернетка.
   --------------------------------------------------------------------- */
SET IDENTITY_INSERT dbo.Courses ON;

INSERT INTO dbo.Courses (Id, InstructorId, Title, Slug, Price, Level, IsPublished, PublishedAt,
                         AverageRating, ReviewsCount, CreatedBy)
SELECT v.Id, v.InstructorId, v.Title, v.Slug, v.Price, v.Level, v.IsPublished, v.PublishedAt,
       v.AverageRating, v.ReviewsCount, N'seed'
FROM (VALUES
    (1, 1, N'C# для початківців',                N'csharp-for-beginners',        1200.00, N'Beginner',     1, CAST('2026-08-01T09:00:00' AS DATETIME2(3)), CAST(4.50 AS DECIMAL(3,2)), 2),
    (2, 1, N'ASP.NET Core Web API',              N'aspnet-core-web-api',         1800.00, N'Intermediate', 1, CAST('2026-08-10T09:00:00' AS DATETIME2(3)), CAST(5.00 AS DECIMAL(3,2)), 1),
    (3, 2, N'Entity Framework Core на практиці', N'ef-core-in-practice',         1500.00, N'Intermediate', 1, CAST('2026-08-15T09:00:00' AS DATETIME2(3)), CAST(4.00 AS DECIMAL(3,2)), 1),
    (4, 2, N'MongoDB для .NET-розробників',      N'mongodb-for-dotnet',          1400.00, N'Intermediate', 1, CAST('2026-08-20T09:00:00' AS DATETIME2(3)), NULL,                       0),
    (5, 3, N'Мікросервіси на .NET Aspire',       N'microservices-dotnet-aspire', 2500.00, N'Advanced',     1, CAST('2026-09-01T09:00:00' AS DATETIME2(3)), NULL,                       0),
    (6, 3, N'Docker і Kubernetes для .NET',      N'docker-kubernetes-dotnet',    2000.00, N'Advanced',     0, NULL,                                        NULL,                       0)
) AS v (Id, InstructorId, Title, Slug, Price, Level, IsPublished, PublishedAt, AverageRating, ReviewsCount)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Courses AS c WHERE c.Id = v.Id OR c.Slug = v.Slug);

SET IDENTITY_INSERT dbo.Courses OFF;
GO

/* ------------------------ CourseDetails (1:1) ------------------------ */
INSERT INTO dbo.CourseDetails (CourseId, Description, Requirements, DurationHours, HasCertificate)
SELECT v.CourseId, v.Description, v.Requirements, v.DurationHours, v.HasCertificate
FROM (VALUES
    (1, N'Основи мови C#: типи, колекції, ООП, LINQ. Після курсу ви напишете консольний застосунок.', NULL,                                       24.0, 1),
    (2, N'Побудова REST API на ASP.NET Core: контролери, DI, валідація, Swagger, автентифікація.',  N'Знання C# на рівні курсу для початківців', 30.5, 1),
    (3, N'Code First, Fluent API, міграції, оптимізація запитів та відстеження змін у EF Core.',    N'C# та основи SQL',                         18.0, 1),
    (4, N'Документна модель, вбудовування й посилання, індекси та aggregation pipeline з драйвером .NET.', N'C# та базове розуміння баз даних',    14.5, 0),
    (5, N'Проєктування мікросервісів, .NET Aspire, обмін подіями, спостережуваність і розгортання.', N'Досвід з ASP.NET Core та Docker',         40.0, 1),
    (6, N'Контейнеризація .NET-застосунків і розгортання в Kubernetes. Курс у розробці.',            N'Досвід з ASP.NET Core',                   20.0, 0)
) AS v (CourseId, Description, Requirements, DurationHours, HasCertificate)
WHERE NOT EXISTS (SELECT 1 FROM dbo.CourseDetails AS d WHERE d.CourseId = v.CourseId);
GO

/* ---------------------------------------------------------------------
   CourseCategories (M:N) з власними полями AddedAt та IsPrimary.
   Курс 2 належить двом категоріям, категорія 1 містить три курси.
   --------------------------------------------------------------------- */
INSERT INTO dbo.CourseCategories (CourseId, CategoryId, AddedAt, IsPrimary)
SELECT v.CourseId, v.CategoryId, v.AddedAt, v.IsPrimary
FROM (VALUES
    (1, 1, CAST('2026-08-01T09:00:00' AS DATETIME2(3)), 1),
    (2, 2, CAST('2026-08-10T09:00:00' AS DATETIME2(3)), 1),
    (2, 1, CAST('2026-08-12T11:30:00' AS DATETIME2(3)), 0),
    (3, 3, CAST('2026-08-15T09:00:00' AS DATETIME2(3)), 1),
    (3, 1, CAST('2026-08-16T10:00:00' AS DATETIME2(3)), 0),
    (4, 3, CAST('2026-08-20T09:00:00' AS DATETIME2(3)), 1),
    (5, 5, CAST('2026-09-01T09:00:00' AS DATETIME2(3)), 1),
    (5, 4, CAST('2026-09-02T15:00:00' AS DATETIME2(3)), 0),
    (6, 4, CAST('2026-09-15T09:00:00' AS DATETIME2(3)), 1)
) AS v (CourseId, CategoryId, AddedAt, IsPrimary)
WHERE NOT EXISTS (SELECT 1 FROM dbo.CourseCategories AS cc
                  WHERE cc.CourseId = v.CourseId AND cc.CategoryId = v.CategoryId);
GO

/* ------------------------------ Lessons ------------------------------ */
SET IDENTITY_INSERT dbo.Lessons ON;

INSERT INTO dbo.Lessons (Id, CourseId, Title, OrderIndex, DurationMinutes, IsFreePreview)
SELECT v.Id, v.CourseId, v.Title, v.OrderIndex, v.DurationMinutes, v.IsFreePreview
FROM (VALUES
    ( 1, 1, N'Знайомство з .NET і першою програмою', 1, 25, 1),
    ( 2, 1, N'Типи даних і змінні',                  2, 40, 0),
    ( 3, 1, N'Класи та об''єкти',                    3, 55, 0),
    ( 4, 2, N'Мінімальний API та контролери',        1, 35, 1),
    ( 5, 2, N'Впровадження залежностей',             2, 45, 0),
    ( 6, 2, N'Валідація та обробка помилок',         3, 50, 0),
    ( 7, 3, N'DbContext і перша міграція',           1, 40, 1),
    ( 8, 3, N'Зв''язки та Fluent API',               2, 60, 0),
    ( 9, 4, N'Документи, колекції та BSON',          1, 30, 1),
    (10, 4, N'Вбудовування чи посилання',            2, 45, 0),
    (11, 5, N'Межі сервісів та обмежені контексти',  1, 50, 1),
    (12, 5, N'Оркестрація з .NET Aspire',            2, 65, 0)
) AS v (Id, CourseId, Title, OrderIndex, DurationMinutes, IsFreePreview)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Lessons AS l
                  WHERE l.Id = v.Id OR (l.CourseId = v.CourseId AND l.OrderIndex = v.OrderIndex));

SET IDENTITY_INSERT dbo.Lessons OFF;
GO

/* ------------------- Контроль: кількість рядків ----------------------
   Після будь-якої кількості запусків має бути 5 / 3 / 6 / 6 / 9 / 12.
   --------------------------------------------------------------------- */
SELECT 'Categories' AS TableName, COUNT(*) AS RowsCount FROM dbo.Categories
UNION ALL SELECT 'Instructors',      COUNT(*) FROM dbo.Instructors
UNION ALL SELECT 'Courses',          COUNT(*) FROM dbo.Courses
UNION ALL SELECT 'CourseDetails',    COUNT(*) FROM dbo.CourseDetails
UNION ALL SELECT 'CourseCategories', COUNT(*) FROM dbo.CourseCategories
UNION ALL SELECT 'Lessons',          COUNT(*) FROM dbo.Lessons;
GO
