/* =====================================================================
   Проєкт №2 — CoursesDb (SQL Server, далі EF Core Code First у Лаб. 03)
   Контекст «Courses»: каталог курсів, викладачі, категорії, уроки.

   Порядок запуску: schema.sql -> seed.sql
   Скрипт ідемпотентний: повторний запуск не змінює вже створені об'єкти.

   Схема спроєктована під угоди EF Core, щоб у Лаб. 03 її відтворила
   початкова міграція InitialCreate без перейменувань: таблиці у множині,
   ключ Id, зовнішні ключі <Сутність>Id, назви PK_/FK_/IX_ як у EF.
   Подальші зміни структури виконуються міграціями EF Core
   (dotnet ef migrations add ...), а не ручним редагуванням цього файлу.

   Первинні ключі — BIGINT IDENTITY (Categories — INT: малий довідник):
   рядки створює лише цей сервіс, тому послідовний ключ із одного джерела
   компактніший і швидший за GUID. Інші сервіси знають курс за цим Id.
   ===================================================================== */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;   -- обов'язково для фільтрованих індексів
GO

IF DB_ID(N'CoursesDb') IS NULL
    CREATE DATABASE CoursesDb;
GO

USE CoursesDb;
GO

/* ------------------------------ Instructors -------------------------- */
IF OBJECT_ID(N'dbo.Instructors', N'U') IS NULL
CREATE TABLE dbo.Instructors (
    Id         BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Instructors PRIMARY KEY,
    Email      NVARCHAR(256)  NOT NULL CONSTRAINT UQ_Instructors_Email UNIQUE,
    FullName   NVARCHAR(200)  NOT NULL,
    Bio        NVARCHAR(2000) NULL,
    CreatedAt  DATETIME2(3)   NOT NULL CONSTRAINT DF_Instructors_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy  NVARCHAR(100)  NULL,
    UpdatedAt  DATETIME2(3)   NULL,
    UpdatedBy  NVARCHAR(100)  NULL,
    IsDeleted  BIT            NOT NULL CONSTRAINT DF_Instructors_IsDeleted DEFAULT 0,
    RowVersion ROWVERSION     NOT NULL,
    CONSTRAINT CK_Instructors_Email CHECK (Email LIKE N'_%@_%._%')
);
GO

/* ------------------------------ Categories --------------------------- */
-- Довідник: наповнюється початковими даними в seed.sql.
IF OBJECT_ID(N'dbo.Categories', N'U') IS NULL
CREATE TABLE dbo.Categories (
    Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Categories PRIMARY KEY,
    Name        NVARCHAR(100) NOT NULL CONSTRAINT UQ_Categories_Name UNIQUE,
    Slug        NVARCHAR(100) NOT NULL CONSTRAINT UQ_Categories_Slug UNIQUE,
    Description NVARCHAR(500) NULL,
    CreatedAt   DATETIME2(3)  NOT NULL CONSTRAINT DF_Categories_CreatedAt DEFAULT SYSUTCDATETIME()
);
GO

/* ---------------------------------------------------------------------
   Courses — корінь агрегату «Курс».            1:N  Instructors -> Courses
   FK на Instructors без каскаду: викладача з курсами видалити не можна.

   AverageRating і ReviewsCount — КОПІЯ ДЛЯ ЧИТАННЯ з feedback_db
   (колекція course_ratings, Проєкт №3). Власник — сервіс Feedback;
   тут значення оновлює лише обробник події CourseRatingChanged.
   FOREIGN KEY на іншу базу не створюється.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Courses', N'U') IS NULL
CREATE TABLE dbo.Courses (
    Id            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Courses PRIMARY KEY,
    InstructorId  BIGINT        NOT NULL CONSTRAINT FK_Courses_Instructors_InstructorId
                  REFERENCES dbo.Instructors(Id),
    Title         NVARCHAR(200) NOT NULL,
    Slug          NVARCHAR(200) NOT NULL CONSTRAINT UQ_Courses_Slug UNIQUE,
    Price         DECIMAL(18,2) NOT NULL CONSTRAINT CK_Courses_Price CHECK (Price >= 0),
    Level         NVARCHAR(20)  NOT NULL CONSTRAINT DF_Courses_Level DEFAULT N'Beginner'
                  CONSTRAINT CK_Courses_Level CHECK (Level IN (N'Beginner', N'Intermediate', N'Advanced')),
    Language      NVARCHAR(10)  NOT NULL CONSTRAINT DF_Courses_Language DEFAULT N'uk',
    IsPublished   BIT           NOT NULL CONSTRAINT DF_Courses_IsPublished DEFAULT 0,
    PublishedAt   DATETIME2(3)  NULL,
    AverageRating DECIMAL(3,2)  NULL                    -- копія з feedback_db.course_ratings
                  CONSTRAINT CK_Courses_AverageRating CHECK (AverageRating BETWEEN 1 AND 5),
    ReviewsCount  INT           NOT NULL CONSTRAINT DF_Courses_ReviewsCount DEFAULT 0   -- копія з feedback_db.course_ratings
                  CONSTRAINT CK_Courses_ReviewsCount CHECK (ReviewsCount >= 0),
    CreatedAt     DATETIME2(3)  NOT NULL CONSTRAINT DF_Courses_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy     NVARCHAR(100) NULL,
    UpdatedAt     DATETIME2(3)  NULL,
    UpdatedBy     NVARCHAR(100) NULL,
    IsDeleted     BIT           NOT NULL CONSTRAINT DF_Courses_IsDeleted DEFAULT 0,
    -- курс одночасно можуть редагувати викладач і модератор
    RowVersion    ROWVERSION    NOT NULL,
    -- опублікований курс обов'язково має дату публікації
    CONSTRAINT CK_Courses_PublishedAt CHECK (IsPublished = 0 OR PublishedAt IS NOT NULL)
);
GO

/* ---------------------------------------------------------------------
   CourseDetails — розширений опис курсу.       1:1  Courses <-> CourseDetails
   Зовнішній ключ CourseId є водночас первинним ключем. Великі текстові
   поля винесено з Courses, щоб списки каталогу читались швидко.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.CourseDetails', N'U') IS NULL
CREATE TABLE dbo.CourseDetails (
    CourseId       BIGINT         NOT NULL CONSTRAINT PK_CourseDetails PRIMARY KEY
                   CONSTRAINT FK_CourseDetails_Courses_CourseId
                   REFERENCES dbo.Courses(Id) ON DELETE CASCADE,
    Description    NVARCHAR(MAX)  NOT NULL,
    Requirements   NVARCHAR(2000) NULL,
    DurationHours  DECIMAL(5,1)   NOT NULL CONSTRAINT CK_CourseDetails_DurationHours CHECK (DurationHours > 0),
    HasCertificate BIT            NOT NULL CONSTRAINT DF_CourseDetails_HasCertificate DEFAULT 0,
    CreatedAt      DATETIME2(3)   NOT NULL CONSTRAINT DF_CourseDetails_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt      DATETIME2(3)   NULL
);
GO

/* ---------------------------------------------------------------------
   CourseCategories — проміжна сутність.        M:N  Courses <-> Categories
   Має ВЛАСНІ ПОЛЯ: AddedAt (коли курс додано в категорію) та IsPrimary
   (основна категорія курсу). Каскад від Courses доречний; від Categories
   каскаду немає — категорію, в якій є курси, видалити не можна.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.CourseCategories', N'U') IS NULL
CREATE TABLE dbo.CourseCategories (
    CourseId   BIGINT       NOT NULL CONSTRAINT FK_CourseCategories_Courses_CourseId
               REFERENCES dbo.Courses(Id) ON DELETE CASCADE,
    CategoryId INT          NOT NULL CONSTRAINT FK_CourseCategories_Categories_CategoryId
               REFERENCES dbo.Categories(Id),
    AddedAt    DATETIME2(3) NOT NULL CONSTRAINT DF_CourseCategories_AddedAt DEFAULT SYSUTCDATETIME(),
    IsPrimary  BIT          NOT NULL CONSTRAINT DF_CourseCategories_IsPrimary DEFAULT 0,
    CONSTRAINT PK_CourseCategories PRIMARY KEY (CourseId, CategoryId)
);
GO

/* ---------------------------------------------------------------------
   Lessons — уроки курсу.                       1:N  Courses -> Lessons
   Каскад доречний: урок не існує без курсу.
   UNIQUE (CourseId, OrderIndex): два уроки не можуть мати той самий номер.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Lessons', N'U') IS NULL
CREATE TABLE dbo.Lessons (
    Id              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Lessons PRIMARY KEY,
    CourseId        BIGINT        NOT NULL CONSTRAINT FK_Lessons_Courses_CourseId
                    REFERENCES dbo.Courses(Id) ON DELETE CASCADE,
    Title           NVARCHAR(200) NOT NULL,
    OrderIndex      INT           NOT NULL CONSTRAINT CK_Lessons_OrderIndex CHECK (OrderIndex > 0),
    DurationMinutes INT           NOT NULL CONSTRAINT CK_Lessons_DurationMinutes CHECK (DurationMinutes > 0),
    IsFreePreview   BIT           NOT NULL CONSTRAINT DF_Lessons_IsFreePreview DEFAULT 0,
    CreatedAt       DATETIME2(3)  NOT NULL CONSTRAINT DF_Lessons_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2(3)  NULL,
    CONSTRAINT UQ_Lessons_CourseId_OrderIndex UNIQUE (CourseId, OrderIndex)
);
GO

/* =====================================================================
   ІНДЕКСИ — кожен створено під конкретний запит.
   Уже є з обмежень: UQ_Courses_Slug (сторінка курсу за адресою /courses/{slug}),
   UQ_Categories_Slug (сторінка категорії), UQ_Lessons_CourseId_OrderIndex
   (уроки курсу в порядку проходження: WHERE CourseId = @Id ORDER BY OrderIndex).
   ===================================================================== */

-- Запит каталогу: «опубліковані курси обраного рівня, відсортовані за ціною»:
--   WHERE IsPublished = 1 AND IsDeleted = 0 AND Level = @Level ORDER BY Price
-- Фільтрований (чернетки й видалені не індексуються) і покриваючий для картки курсу.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_Courses_Level_Price' AND object_id = OBJECT_ID(N'dbo.Courses'))
    CREATE INDEX IX_Courses_Level_Price
        ON dbo.Courses (Level, Price)
        INCLUDE (Title, Slug, AverageRating, ReviewsCount)
        WHERE IsPublished = 1 AND IsDeleted = 0;
GO

-- Запит кабінету викладача: «усі мої курси, зокрема чернетки»:
--   WHERE InstructorId = @InstructorId AND IsDeleted = 0
-- Він же прискорює перевірку FK_Courses_Instructors_InstructorId.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_Courses_InstructorId' AND object_id = OBJECT_ID(N'dbo.Courses'))
    CREATE INDEX IX_Courses_InstructorId
        ON dbo.Courses (InstructorId)
        INCLUDE (Title, IsPublished)
        WHERE IsDeleted = 0;
GO

-- Запит пошуку: «курси, назва яких починається з введеного тексту»:
--   WHERE Title LIKE @Prefix + N'%'
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_Courses_Title' AND object_id = OBJECT_ID(N'dbo.Courses'))
    CREATE INDEX IX_Courses_Title
        ON dbo.Courses (Title);
GO

-- Запит: «курси категорії, спершу нещодавно додані»:
--   WHERE CategoryId = @CategoryId ORDER BY AddedAt DESC
-- PK починається з CourseId, тому для пошуку за категорією потрібен окремий індекс.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_CourseCategories_CategoryId' AND object_id = OBJECT_ID(N'dbo.CourseCategories'))
    CREATE INDEX IX_CourseCategories_CategoryId
        ON dbo.CourseCategories (CategoryId, AddedAt DESC);
GO

-- Запит: «основна категорія курсу» (хлібні крихти на сторінці курсу):
--   WHERE CourseId = @CourseId AND IsPrimary = 1
-- Унікальний фільтрований індекс водночас гарантує правило:
-- у курсу може бути лише одна основна категорія.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'UX_CourseCategories_Primary' AND object_id = OBJECT_ID(N'dbo.CourseCategories'))
    CREATE UNIQUE INDEX UX_CourseCategories_Primary
        ON dbo.CourseCategories (CourseId)
        INCLUDE (CategoryId)
        WHERE IsPrimary = 1;
GO

/* =====================================================================
   Найменші привілеї: роль застосунку має доступ лише до цієї бази.
   ===================================================================== */
IF DATABASE_PRINCIPAL_ID(N'courses_app') IS NULL
    CREATE ROLE courses_app;
GO

GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO courses_app;
GO

PRINT N'CoursesDb: schema is ready.';
GO
