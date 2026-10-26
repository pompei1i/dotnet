IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026153619_InitialCreate'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Slug] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [CreatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026153619_InitialCreate'
)
BEGIN
    CREATE TABLE [Instructors] (
        [Id] bigint NOT NULL IDENTITY,
        [Email] nvarchar(256) NOT NULL,
        [FullName] nvarchar(200) NOT NULL,
        [Bio] nvarchar(2000) NULL,
        [CreatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2(3) NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Instructors] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Instructors_Email] CHECK ([Email] LIKE N'_%@_%._%')
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026153619_InitialCreate'
)
BEGIN
    CREATE TABLE [Courses] (
        [Id] bigint NOT NULL IDENTITY,
        [InstructorId] bigint NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Slug] nvarchar(200) NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [Level] nvarchar(20) NOT NULL,
        [Language] nvarchar(10) NOT NULL,
        [IsPublished] bit NOT NULL,
        [PublishedAt] datetime2(3) NULL,
        [AverageRating] decimal(3,2) NULL,
        [ReviewsCount] int NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2(3) NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Courses] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Courses_AverageRating] CHECK ([AverageRating] BETWEEN 1 AND 5),
        CONSTRAINT [CK_Courses_Level] CHECK ([Level] IN (N'Beginner', N'Intermediate', N'Advanced')),
        CONSTRAINT [CK_Courses_Price] CHECK ([Price] >= 0),
        CONSTRAINT [CK_Courses_PublishedAt] CHECK ([IsPublished] = 0 OR [PublishedAt] IS NOT NULL),
        CONSTRAINT [CK_Courses_ReviewsCount] CHECK ([ReviewsCount] >= 0),
        CONSTRAINT [FK_Courses_Instructors_InstructorId] FOREIGN KEY ([InstructorId]) REFERENCES [Instructors] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026153619_InitialCreate'
)
BEGIN
    CREATE TABLE [CourseCategories] (
        [CourseId] bigint NOT NULL,
        [CategoryId] int NOT NULL,
        [AddedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [IsPrimary] bit NOT NULL,
        CONSTRAINT [PK_CourseCategories] PRIMARY KEY ([CourseId], [CategoryId]),
        CONSTRAINT [FK_CourseCategories_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CourseCategories_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026153619_InitialCreate'
)
BEGIN
    CREATE TABLE [CourseDetails] (
        [CourseId] bigint NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [Requirements] nvarchar(2000) NULL,
        [DurationHours] decimal(5,1) NOT NULL,
        [HasCertificate] bit NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2(3) NULL,
        CONSTRAINT [PK_CourseDetails] PRIMARY KEY ([CourseId]),
        CONSTRAINT [CK_CourseDetails_DurationHours] CHECK ([DurationHours] > 0),
        CONSTRAINT [FK_CourseDetails_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026153619_InitialCreate'
)
BEGIN
    CREATE TABLE [Lessons] (
        [Id] bigint NOT NULL IDENTITY,
        [CourseId] bigint NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [OrderIndex] int NOT NULL,
        [DurationMinutes] int NOT NULL,
        [IsFreePreview] bit NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2(3) NULL,
        CONSTRAINT [PK_Lessons] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Lessons_DurationMinutes] CHECK ([DurationMinutes] > 0),
        CONSTRAINT [CK_Lessons_OrderIndex] CHECK ([OrderIndex] > 0),
        CONSTRAINT [FK_Lessons_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026153619_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'Description', N'Name', N'Slug') AND [object_id] = OBJECT_ID(N'[Categories]'))
        SET IDENTITY_INSERT [Categories] ON;
    EXEC(N'INSERT INTO [Categories] ([Id], [CreatedAt], [Description], [Name], [Slug])
    VALUES (1, ''2026-08-01T00:00:00.000Z'', N''Мови програмування та основи розробки'', N''Програмування'', N''programming''),
    (2, ''2026-08-01T00:00:00.000Z'', N''Серверна та клієнтська веб-розробка'', N''Веб-розробка'', N''web-development''),
    (3, ''2026-08-01T00:00:00.000Z'', N''Реляційні та документні СУБД, ORM'', N''Бази даних'', N''databases''),
    (4, ''2026-08-01T00:00:00.000Z'', N''Контейнери, оркестрація, CI/CD'', N''DevOps'', N''devops''),
    (5, ''2026-08-01T00:00:00.000Z'', N''Мікросервіси, DDD, патерни проєктування'', N''Архітектура ПЗ'', N''software-architecture'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'Description', N'Name', N'Slug') AND [object_id] = OBJECT_ID(N'[Categories]'))
        SET IDENTITY_INSERT [Categories] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026153619_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Categories_Name] ON [Categories] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026153619_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Categories_Slug] ON [Categories] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026153619_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CourseCategories_CategoryId] ON [CourseCategories] ([CategoryId], [AddedAt] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026153619_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_CourseCategories_Primary] ON [CourseCategories] ([CourseId]) INCLUDE ([CategoryId]) WHERE [IsPrimary] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026153619_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Courses_InstructorId] ON [Courses] ([InstructorId]) INCLUDE ([Title], [IsPublished]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026153619_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Courses_Level_Price] ON [Courses] ([Level], [Price]) INCLUDE ([Title], [Slug], [AverageRating], [ReviewsCount]) WHERE [IsPublished] = 1 AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026153619_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Courses_Title] ON [Courses] ([Title]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026153619_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Courses_Slug] ON [Courses] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026153619_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Instructors_Email] ON [Instructors] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026153619_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Lessons_CourseId_OrderIndex] ON [Lessons] ([CourseId], [OrderIndex]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026153619_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261026153619_InitialCreate', N'10.0.14');
END;

COMMIT;
GO

