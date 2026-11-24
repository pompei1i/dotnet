// AppHost — лише оркестрація під час розробки: які ресурси запустити, як їх зв'язати й у якому
// порядку стартувати. Бізнес-логіки тут немає, а сервіси про AppHost не знають (посилання — лише звідси).
var builder = DistributedApplication.CreateBuilder(args);

// ---------- Бази даних (контейнери Docker) ----------

// Пароль sa — параметр-секрет: значення лежить у user-secrets AppHost (Parameters:sql-password),
// у коді й репозиторії його немає. Якщо секрету ще немає, Dashboard попросить ввести його під час запуску.
var sqlPassword = builder.AddParameter("sql-password", secret: true);

// SQL Server; том зберігає дані між перезапусками (без WithDataVolume бази зникали б щоразу)
var sql = builder.AddSqlServer("sql", sqlPassword)
    .WithDataVolume();

// Ім'я ресурсу бази = ім'я рядка підключення, який сервіс уже читає (ConnectionStrings:EnrollmentsDb).
// Aspire передає його змінною ConnectionStrings__EnrollmentsDb — код сервісу не змінюється.
// Базу Проєкту №1 створюють скрипти Лаб. 01: вони ідемпотентні, тож повторні запуски безпечні.
var enrollmentsDb = sql.AddDatabase("EnrollmentsDb")
    .WithCreationScript(ReadDatabaseScripts("schema.sql", "procedures.sql", "seed.sql") + ReadMigrations());

// Проєкт №2 — порожня база; схему й дані при старті застосовує сам Courses.Api (міграції EF Core + сідінг)
var coursesDb = sql.AddDatabase("CoursesDb");

// MongoDB — окремий том. Сталий порт 27017 — щоб підключатися з MongoDB Compass; пароль — секрет
// Parameters:mongo-password у user-secrets AppHost
var mongo = builder.AddMongoDB("mongo", port: 27017)
    .WithDataVolume();

// Проєкт №3: ресурс FeedbackDb (рядок ConnectionStrings:FeedbackDb), сама база в MongoDB — feedback_db
var feedbackDb = mongo.AddDatabase("FeedbackDb", databaseName: "feedback_db");

// ---------- Розподілений кеш (Лаб. 06) ----------

// Redis — рівень L2 кешу, спільний для всіх екземплярів сервісів, і кеш агрегованих відповідей.
// Том зберігає кеш між перезапусками. Рядок підключення сервіси отримують як ConnectionStrings:redis,
// тому адреса Redis в appsettings не прописується; ім'я "redis" = ім'я в AddRedisDistributedCache("redis").
var redis = builder.AddRedis("redis")
    .WithDataVolume();

// ---------- Брокер повідомлень (Лаб. 08) ----------

// RabbitMQ для подій між сервісами. Том зберігає durable-черги й persistent-повідомлення між перезапусками;
// Management UI — http://localhost:15672 (користувач guest, пароль — параметр messaging-password у Dashboard).
// Облікові дані генерує Aspire, сервіси отримують ConnectionStrings:messaging через WithReference.
var messaging = builder.AddRabbitMQ("messaging")
    .WithDataVolume()
    .WithManagementPlugin(port: 15672);

// ---------- Мікросервіси (Лаб. 02–04) ----------
// Ім'я ресурсу (enrollments-api...) — логічне ім'я для service discovery: Gateway й Aggregator
// звертаються на http://enrollments-api, а не на localhost:порт. Порти Aspire призначає сам.
// WithReference — передати рядок підключення / адресу; WaitFor — не стартувати, доки залежність не готова.
// WithHttpHealthCheck("/health/ready") (Лаб. 07) — Dashboard показує стан ресурсу (Healthy / Degraded /
// Unhealthy) за readiness-перевірками сервісу: база, Redis (Degraded), у Aggregator — gRPC-залежності.
// Тому WaitFor чекає, поки залежність стане ЗДОРОВОЮ (база відповідає, міграції застосовано), а не лише запуститься.
//
// Лаб. 06: кожен сервіс має два endpoint-и з розділу Kestrel:Endpoints свого appsettings.json —
// "http" (REST для Gateway, HTTP/1.1) і "grpc" (лише HTTP/2 без TLS, h2c). Aspire читає цей розділ,
// створює однойменні endpoint-и й призначає їм порти. Aggregator звертається до gRPC за адресою
// http://_grpc.courses-api — service discovery підставляє саме endpoint "grpc".

// Замовлення споживають події Каталогу: локальна копія довідника курсів (черга enrollments.catalog-sync)
var enrollments = builder.AddProject<Projects.Enrollments_Api>("enrollments-api")
    .WithReference(enrollmentsDb)
    .WaitFor(enrollmentsDb)
    .WithReference(messaging)
    .WaitFor(messaging)
    .WithHttpHealthCheck("/health/ready");

// Каталог курсів — read-heavy довідник: кеш L1 (пам'ять) + L2 (Redis)
var courses = builder.AddProject<Projects.Courses_Api>("courses-api")
    .WithReference(coursesDb)
    .WaitFor(coursesDb)
    .WithReference(redis)
    .WaitFor(redis)
    .WithReference(messaging)   // публікує події про зміну курсу (exchange catalog-events)
    .WaitFor(messaging)
    .WithHttpHealthCheck("/health/ready");

// Рейтинг і стрічка відгуків курсу — читаються з кожною карткою замовлення, змінюються рідко
var reviews = builder.AddProject<Projects.Reviews_WebApi>("reviews-api")
    .WithReference(feedbackDb)
    .WaitFor(feedbackDb)
    .WithReference(redis)
    .WaitFor(redis)
    .WithReference(messaging)   // споживає course.renamed: courseTitle у відгуках і обговореннях
    .WaitFor(messaging)
    .WithHttpHealthCheck("/health/ready");

// ---------- Нові компоненти платформи (Лаб. 05) ----------

// Aggregator паралельно опитує всі три сервіси (з Лаб. 06 — через gRPC), тож отримує посилання
// на кожен; Redis — для кешу агрегованих відповідей з коротким TTL
var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithReference(enrollments)
    .WithReference(courses)
    .WithReference(reviews)
    .WithReference(redis)
    .WaitFor(enrollments)
    .WaitFor(courses)
    .WaitFor(reviews)
    .WaitFor(redis)
    .WithHttpHealthCheck("/health/ready");

// Gateway — єдина точка входу з фіксованим портом 5000. Посилання на всі сервіси й Aggregator
// потрібні YARP для маршрутизації. Порядок старту: БД -> сервіси -> Aggregator -> Gateway.
builder.AddProject<Projects.Platform_ApiGateway>("gateway")
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithReference(enrollments)
    .WithReference(courses)
    .WithReference(reviews)
    .WithReference(aggregator)
    .WaitFor(aggregator)
    .WithHttpHealthCheck("/health/ready")
    .WithExternalHttpEndpoints();

builder.Build().Run();

// Склеює скрипти Проєкту №1 (db/p1) в один сценарій створення бази.
// Розділювач GO між файлами обов'язковий: Aspire виконує сценарій пакетами за GO.
string ReadDatabaseScripts(params string[] fileNames)
{
    var scriptsDirectory = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "db", "p1"));

    return string.Join(
        $"{Environment.NewLine}GO{Environment.NewLine}",
        fileNames.Select(fileName => File.ReadAllText(Path.Combine(scriptsDirectory, fileName))));
}

// Міграції EnrollmentsDb після Лаб. 01 (Лаб. 08: таблиця ProcessedEvents) — ідемпотентні SQL-скрипти
// з Enrollments.Dal/Migrations у порядку імен; виконуються після схеми, процедур і сідінгу
string ReadMigrations()
{
    var directory = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "Services", "Enrollments", "Enrollments.Dal", "Migrations"));

    return string.Concat(Directory.GetFiles(directory, "*.sql")
        .Order(StringComparer.Ordinal)
        .Select(file => $"{Environment.NewLine}GO{Environment.NewLine}{File.ReadAllText(file)}"));
}
