/* =====================================================================
   Міграція 001 (Лаб. 08) — EnrollmentsDb
   Таблиця оброблених інтеграційних подій для ідемпотентних споживачів.

   RabbitMQ гарантує доставку «щонайменше один раз»: те саме повідомлення може прийти
   повторно (наприклад, з'єднання обірвалося після обробки, але до ack). Обробник у ТІЙ САМІЙ
   транзакції, що й бізнес-зміни, вставляє EventId; порушення PRIMARY KEY (помилка 2627)
   означає «подію вже оброблено» — транзакція відкочується, дані вдруге не змінюються.

   Скрипт ідемпотентний; AppHost виконує його після schema.sql, procedures.sql і seed.sql.
   ===================================================================== */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

USE EnrollmentsDb;
GO

IF OBJECT_ID(N'dbo.ProcessedEvents', N'U') IS NULL
    CREATE TABLE dbo.ProcessedEvents (
        EventId     UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProcessedEvents PRIMARY KEY,
        EventType   NVARCHAR(200)    NOT NULL,
        Consumer    NVARCHAR(200)    NOT NULL,                -- черга, яка обробила подію
        OccurredAt  DATETIME2(3)     NOT NULL,                -- коли подія сталася у власника
        ProcessedAt DATETIME2(3)     NOT NULL CONSTRAINT DF_ProcessedEvents_ProcessedAt DEFAULT SYSUTCDATETIME()
    );
GO

-- Для прибирання старих записів (подію старшу за кілька днів RabbitMQ уже не передоставить)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProcessedEvents_ProcessedAt' AND object_id = OBJECT_ID(N'dbo.ProcessedEvents'))
    CREATE INDEX IX_ProcessedEvents_ProcessedAt ON dbo.ProcessedEvents (ProcessedAt);
GO
