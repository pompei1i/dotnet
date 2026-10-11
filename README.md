# Платформа онлайн-курсів

Навчальний мікросервісний проєкт курсу «Технології .NET у сучасній веб-інженерії».

**Лабораторна робота №1** — проєктування баз даних для мікросервісної архітектури: три самостійні бази (дві реляційні та одна MongoDB), які разом описують предметну область.

## Предметна область

Платформа онлайн-курсів. Викладачі публікують платні курси з уроками, студенти знаходять їх у каталозі, оформлюють замовлення та оплачують його, після чого отримують доступ до навчання. Студенти залишають відгуки й оцінки та ставлять питання в обговореннях курсу, а викладачі на них відповідають.

## Контексти та бази даних

Систему поділено на три незалежні контексти (Bounded Contexts). Кожен має власну базу; кожна сутність належить рівно одному контексту.

| Проєкт | Контекст | База даних | Технологія | Власні сутності |
|---|---|---|---|---|
| №1 | **Enrollments** — замовлення та оплата | `EnrollmentsDb` (SQL Server) | ADO.NET + Dapper | `Student`, `Order`, `OrderDetails`, `OrderItem`, `OrderStatusHistory` |
| №2 | **Courses** — каталог курсів | `CoursesDb` (SQL Server) | EF Core, Code First | `Instructor`, `Course`, `CourseDetail`, `Category`, `CourseCategory`, `Lesson` |
| №3 | **Feedback** — відгуки й обговорення | `feedback_db` (MongoDB) | документна модель | `reviews`, `discussions`, `messages`, `course_ratings` |

Чому саме така технологія для кожного контексту:

- **Enrollments → SQL + збережувані процедури.** Замовлення — транзакційний процес зі строгим життєвим циклом статусів; потрібні транзакції, блокування й перевірка цілісності на рівні бази.
- **Courses → SQL + EF Core.** Багато сутностей зі зв'язками всіх типів і довідниковими даними; зручно описувати через Fluent API та змінювати міграціями.
- **Feedback → MongoDB.** Відгуки й повідомлення мають змінну структуру (фото, відео, фрагменти коду, вкладені відповіді) і читаються цілими агрегатами.

Таблиця `Courses` у `EnrollmentsDb` — не окрема сутність контексту Enrollments, а локальна копія довідника з контексту Courses: власником курсу залишається `CoursesDb`, а Enrollments цю копію лише читає (див. [політику дублювання](#політика-дублювання-даних)).

Докладно про агрегати, життєвий цикл і обґрунтування рішень — у [docs/contexts.md](docs/contexts.md).

## Межі між базами

Три бази повністю самостійні: спільних таблиць немає, **жодного `FOREIGN KEY` між базами немає**. Посилання на сутність іншого контексту — звичайний стовпець або поле з її ідентифікатором, без обмеження в СУБД; цілісність такого посилання за потреби перевіряє прикладний код.

| Де | Поле | На що посилається | FK |
|---|---|---|---|
| `EnrollmentsDb.Courses` | `Id` | `CoursesDb.Courses.Id` | немає |
| `feedback_db.reviews` | `courseId` | `CoursesDb.Courses.Id` | немає |
| `feedback_db.reviews` | `author.studentId` | `EnrollmentsDb.Students.Id` | немає |
| `feedback_db.discussions`, `messages` | `author.userId` | `EnrollmentsDb.Students.Id` або `CoursesDb.Instructors.Id` | немає |
| `feedback_db.course_ratings` | `_id` | `CoursesDb.Courses.Id` | немає |

Усі зовнішні ключі в SQL-скриптах з'єднують таблиці однієї бази. Це перевіряє запит `SELECT * FROM sys.foreign_keys` у [db/p1/checks.sql](db/p1/checks.sql) і [db/p2/checks.sql](db/p2/checks.sql).

## Політика дублювання даних

Дублювання роз'єднує сервіси: щоб показати замовлення чи відгук, не потрібен синхронний виклик до каталогу. Для кожного дубльованого поля визначено одного власника; копію змінює лише обробник подій від власника, тож подвійного запису немає. Узгодженість — eventual consistency через події (механізм — у Лаб. 08).

| Поле | Де зберігається | Джерело (власник даних) | Політика узгодженості |
|---|---|---|---|
| `OrderItems.CourseTitle`, `OrderItems.UnitPrice` | `EnrollmentsDb` | `Courses.Title`, `Courses.Price` (`CoursesDb`) | **знімок** на момент оформлення — не оновлюється |
| `Courses.Title`, `Courses.Price`, `Courses.IsAvailable` (локальна копія довідника) | `EnrollmentsDb` | `Courses.Title`, `Price`, `IsPublished` (`CoursesDb`) | оновлюється подіями `CoursePublished`, `CourseUpdated`, `CourseUnpublished` |
| `reviews.courseTitle`, `discussions.courseTitle`, `course_ratings.courseTitle` | `feedback_db` | `Courses.Title` (`CoursesDb`) | оновлюється подією `CourseUpdated` |
| `Courses.AverageRating`, `Courses.ReviewsCount` | `CoursesDb` | `course_ratings` (`feedback_db`) | оновлюється подією `CourseRatingChanged` |
| `reviews.author.displayName`, `author.displayName` студента в `discussions`, `messages`, `reviews.comments`, а також `lastMessage.authorName` і `recentReviews.authorName` | `feedback_db` | `Students.FullName` (`EnrollmentsDb`) | оновлюється подією `StudentRenamed` |
| `reviews.isVerifiedPurchase` | `feedback_db` | `Orders.Status` (`EnrollmentsDb`) | оновлюється подіями `OrderAccessGranted`, `OrderRefunded` |
| `author.displayName` викладача в `discussions`, `messages`, `reviews.comments`, а також `lastMessage.authorName` | `feedback_db` | `Instructors.FullName` (`CoursesDb`) | оновлюється подією `InstructorRenamed` |
| `discussions.lastMessage`, `course_ratings.recentReviews` | `feedback_db` | `messages`, `reviews` (та сама база) | оновлюється в тій самій операції запису, що й оригінал |

Два різні випадки:

- **Знімок** (`OrderItems.CourseTitle`, `UnitPrice`) фіксує умови покупки й свідомо не змінюється. У тестових даних курс «C# для початківців» куплено за 1000.00, а зараз він коштує 1200.00 — позиція замовлення зберігає стару ціну. Знімок робить процедура `usp_AddOrderItem`.
- **Копія для читання** (решта рядків) має наздоганяти оригінал і оновлюється подіями від власника.

Покриття пар сервісів: Enrollments ↔ Courses — рядки 1–2, Feedback ↔ Courses — рядки 3, 4, 7, Feedback ↔ Enrollments — рядки 5–6.

## ERD

ERD-діаграми обох реляційних баз — зображеннями нижче. Вихідний текст діаграм, перелік обмежень та індексів: [docs/erd-p1.md](docs/erd-p1.md), [docs/erd-p2.md](docs/erd-p2.md). Документна модель MongoDB з прикладами документів: [docs/mongodb-model.md](docs/mongodb-model.md).

### Проєкт №1 — `EnrollmentsDb`

![ERD Проєкту №1 — EnrollmentsDb: ключі та типи зв'язків](docs/erd-p1.png)

Та сама схема текстом (лише ключі та головні поля):

```mermaid
erDiagram
    Students ||--o{ Orders : "1:N"
    Orders ||--|| OrderDetails : "1:1"
    Orders ||--|{ OrderItems : "1:N"
    Courses ||--o{ OrderItems : "1:N"
    Orders ||--|{ OrderStatusHistory : "1:N"

    Students {
        bigint Id PK
        nvarchar Email UK
        nvarchar FullName
    }
    Orders {
        bigint Id PK
        bigint StudentId FK
        nvarchar Status
        decimal TotalAmount
        rowversion RowVersion
    }
    OrderDetails {
        bigint OrderId PK, FK
        nvarchar BillingName
        nvarchar PaymentMethod
    }
    Courses {
        bigint Id PK "Id з CoursesDb, без FK"
        nvarchar Title "копія"
        decimal Price "копія"
    }
    OrderItems {
        bigint OrderId PK, FK
        bigint CourseId PK, FK
        nvarchar CourseTitle "знімок"
        decimal UnitPrice "знімок"
        decimal Discount
    }
    OrderStatusHistory {
        bigint Id PK
        bigint OrderId FK
        nvarchar OldStatus
        nvarchar NewStatus
    }
```

Зв'язки: **1:1** `Orders` ↔ `OrderDetails` (FK є водночас PK), **1:N** `Students` → `Orders`, **M:N** `Orders` ↔ `Courses` через проміжну таблицю `OrderItems`.

### Проєкт №2 — `CoursesDb`

![ERD Проєкту №2 — CoursesDb: ключі та типи зв'язків](docs/erd-p2.png)

Та сама схема текстом (лише ключі та головні поля):

```mermaid
erDiagram
    Instructors ||--o{ Courses : "1:N"
    Courses ||--|| CourseDetails : "1:1"
    Courses ||--o{ Lessons : "1:N"
    Courses ||--o{ CourseCategories : "1:N"
    Categories ||--o{ CourseCategories : "1:N"

    Instructors {
        bigint Id PK
        nvarchar Email UK
        nvarchar FullName
    }
    Courses {
        bigint Id PK
        bigint InstructorId FK
        nvarchar Title
        nvarchar Slug UK
        decimal Price
        decimal AverageRating "копія з feedback_db"
    }
    CourseDetails {
        bigint CourseId PK, FK
        nvarchar Description
        decimal DurationHours
    }
    Categories {
        int Id PK
        nvarchar Name UK
        nvarchar Slug UK
    }
    CourseCategories {
        bigint CourseId PK, FK
        int CategoryId PK, FK
        datetime2 AddedAt "власне поле"
        bit IsPrimary "власне поле"
    }
    Lessons {
        bigint Id PK
        bigint CourseId FK
        nvarchar Title
        int OrderIndex
    }
```

Зв'язки: **1:1** `Courses` ↔ `CourseDetails`, **1:N** `Instructors` → `Courses` і `Courses` → `Lessons`, **M:N** `Courses` ↔ `Categories` через проміжну сутність `CourseCategories` з власними полями `AddedAt` та `IsPrimary`.

### Проєкт №3 — `feedback_db`

| Колекція | Прийом моделювання |
|---|---|
| `reviews` | гібрид: вбудовані автор і коментарі + посилання на `discussions` |
| `discussions` | гібрид: вбудований знімок останнього повідомлення + посилання на `messages` |
| `messages` | referencing: посилання на тему та батьківське повідомлення |
| `course_ratings` | embedding: розподіл оцінок і останні відгуки всередині документа |

## Структура репозиторію

```
README.md                  опис домену, розгортання, таблиця дублювання, ERD
docs/
  contexts.md              контексти, агрегати, обґрунтування рішень
  erd-p1.md, erd-p1.png    ERD, обмеження, індекси та процедури Проєкту №1
  erd-p2.md, erd-p2.png    ERD, обмеження та індекси Проєкту №2
  mongodb-model.md         колекції, приклади документів, індекси, запити
db/
  docker-compose.yml       SQL Server і MongoDB для локального запуску
  p1/                      Проєкт №1 — EnrollmentsDb
    schema.sql             таблиці, ключі, обмеження, індекси, представлення
    procedures.sql         6 збережуваних процедур
    seed.sql               тестові дані
    checks.sql             запити перевірки
  p2/                      Проєкт №2 — CoursesDb
    schema.sql             таблиці, ключі, обмеження, індекси
    seed.sql               довідникові й тестові дані
    checks.sql             запити перевірки
  p3/                      Проєкт №3 — feedback_db (MongoDB)
    collections.js         колекції з валідаторами та індекси
    seed.js                тестові документи
    queries.js             запити перевірки та aggregation pipeline
```

## Розгортання

Потрібні SQL Server 2019 або новіший і MongoDB 6 або новіша. Усі скрипти ідемпотентні: їх можна запускати повторно.

### Порядок запуску

| Крок | База | Скрипт | Що робить |
|---|---|---|---|
| 1 | `EnrollmentsDb` | `db/p1/schema.sql` | створює базу, таблиці, індекси, представлення, роль |
| 2 | `EnrollmentsDb` | `db/p1/procedures.sql` | створює 6 збережуваних процедур |
| 3 | `EnrollmentsDb` | `db/p1/seed.sql` | додає тестові дані |
| 4 | `CoursesDb` | `db/p2/schema.sql` | створює базу, таблиці, індекси, роль |
| 5 | `CoursesDb` | `db/p2/seed.sql` | додає довідникові й тестові дані |
| 6 | `feedback_db` | `db/p3/collections.js` | створює колекції з валідаторами та індекси |
| 7 | `feedback_db` | `db/p3/seed.js` | додає тестові документи |

Бази створюються самими скриптами (`CREATE DATABASE` у `schema.sql`, `getSiblingDB` у `collections.js`), окремо створювати їх не треба.

### Варіант А — Docker

Потрібен Docker Desktop. Команди виконуються з кореня репозиторію.

```powershell
docker compose -f db/docker-compose.yml up -d
```

SQL Server стартує 20–30 секунд. Далі:

```powershell
function Invoke-Sql($file) {
    docker exec lab01-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "Lab01_StrongPass2026" -C -I -b -i $file
}

# Проєкт №1
Invoke-Sql /scripts/p1/schema.sql
Invoke-Sql /scripts/p1/procedures.sql
Invoke-Sql /scripts/p1/seed.sql

# Проєкт №2
Invoke-Sql /scripts/p2/schema.sql
Invoke-Sql /scripts/p2/seed.sql

# Проєкт №3
docker exec lab01-mongo mongosh --quiet --file /scripts/p3/collections.js
docker exec lab01-mongo mongosh --quiet --file /scripts/p3/seed.js
```

Папка `db/` змонтована в контейнери як `/scripts`. Пароль `sa` призначений лише для локальної розробки; його можна змінити змінною середовища `MSSQL_SA_PASSWORD`.

### Варіант Б — власні SQL Server і MongoDB

Через `sqlcmd` (ключ `-I` вмикає `QUOTED_IDENTIFIER`, `-f 65001` читає файли як UTF-8):

```powershell
sqlcmd -S localhost -E -I -b -f 65001 -i db/p1/schema.sql
sqlcmd -S localhost -E -I -b -f 65001 -i db/p1/procedures.sql
sqlcmd -S localhost -E -I -b -f 65001 -i db/p1/seed.sql
sqlcmd -S localhost -E -I -b -f 65001 -i db/p2/schema.sql
sqlcmd -S localhost -E -I -b -f 65001 -i db/p2/seed.sql

mongosh "mongodb://localhost:27017" db/p3/collections.js
mongosh "mongodb://localhost:27017" db/p3/seed.js
```

Або в SSMS / Azure Data Studio: відкрити файли в тому самому порядку й виконати кожен цілком (F5).

### Окремий логін застосунку (найменші привілеї)

Скрипти створюють ролі `enrollments_app` і `courses_app` з правами лише у своїй базі. Логін для сервісу створюється окремо й отримує доступ тільки до однієї бази:

```sql
CREATE LOGIN enrollments_service WITH PASSWORD = '<пароль>';
USE EnrollmentsDb;
CREATE USER enrollments_service FOR LOGIN enrollments_service;
ALTER ROLE enrollments_app ADD MEMBER enrollments_service;
```

Для `CoursesDb` — так само з роллю `courses_app`.

## Перевірка

Скрипти перевірки нічого не залишають у базах.

```powershell
sqlcmd -S localhost -E -I -f 65001 -i db/p1/checks.sql
sqlcmd -S localhost -E -I -f 65001 -i db/p2/checks.sql
mongosh "mongodb://localhost:27017" db/p3/queries.js
```

| Що перевірити | Як | Очікуваний результат |
|---|---|---|
| Немає FK між базами | п. 1 у `checks.sql` обох баз (`sys.foreign_keys`) | усі FK посилаються на таблиці тієї самої бази |
| Індекси створено | п. 2 у `checks.sql` | перелік індексів із фільтрами |
| Недопустимий перехід статусу | п. 5.3 у `db/p1/checks.sql`: `Submitted → AccessGranted` | керована помилка `50003`, статус не змінився |
| Скасування після оплати | п. 5.6 у `db/p1/checks.sql` | керована помилка `50003` |
| Оптимістична конкурентність | п. 5.4 у `db/p1/checks.sql`: застарілий `RowVersion` | керована помилка `50004` |
| `CHECK` і `UNIQUE` | п. 6 у `db/p1/checks.sql`, п. 7 у `db/p2/checks.sql` | вставка відхиляється |
| Гнучка схема MongoDB | п. 2–3 у `db/p3/queries.js` | документи з різними наборами полів |
| Ідемпотентність seed | запустити `seed.sql` / `seed.js` двічі | кількість рядків не змінюється: P1 — 4 / 6 / 6 / 6 / 8 / 17, P2 — 5 / 3 / 6 / 6 / 9 / 12, P3 — 4 / 3 / 5 / 3 |

Приклад виклику процедури зі свідомо недопустимим переходом — відкрити доступ до неоплаченого замовлення:

```sql
USE EnrollmentsDb;
EXEC dbo.usp_ChangeOrderStatus @OrderId = 3, @NewStatus = N'AccessGranted';
-- Msg 50003: Status transition Submitted -> AccessGranted is not allowed
```

## Збережувані процедури Проєкту №1

| Процедура | Операція | Що робить |
|---|---|---|
| `usp_CreateOrder` | Create | створює замовлення, реквізити й перший запис журналу однією транзакцією |
| `usp_AddOrderItem` | Update | додає курс у замовлення, фіксує знімок назви й ціни, перераховує суму |
| `usp_GetOrderById` | Read | замовлення, його позиції та історія статусів |
| `usp_GetStudentOrders` | Read | замовлення студента з фільтром за статусом і пагінацією |
| `usp_ChangeOrderStatus` | Update | транзакційна зміна статусу з перевіркою допустимості переходу |
| `usp_DeleteOrder` | Delete | м'яке видалення чернетки або скасованого замовлення |

Допустимі переходи: `Submitted → AwaitingPayment → Paid → AccessGranted`; скасування (`Cancelled`) — лише до оплати; повернення (`Refunded`) — лише після оплати й до відкриття доступу.

## Що далі

Ці бази — основа наступних робіт: Лаб. 02 — тришарова архітектура на ADO.NET і Dapper поверх `EnrollmentsDb`, Лаб. 03 — EF Core поверх `CoursesDb`, Лаб. 04 — MongoDB поверх `feedback_db`.
