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
    .WithCreationScript(ReadDatabaseScripts("schema.sql", "procedures.sql", "seed.sql"));

// Проєкт №2 — порожня база; схему й дані при старті застосовує сам Courses.Api (міграції EF Core + сідінг)
var coursesDb = sql.AddDatabase("CoursesDb");

// MongoDB — окремий том. Сталий порт 27017 — щоб підключатися з MongoDB Compass; пароль — секрет
// Parameters:mongo-password у user-secrets AppHost
var mongo = builder.AddMongoDB("mongo", port: 27017)
    .WithDataVolume();

// Проєкт №3: ресурс FeedbackDb (рядок ConnectionStrings:FeedbackDb), сама база в MongoDB — feedback_db
var feedbackDb = mongo.AddDatabase("FeedbackDb", databaseName: "feedback_db");

// ---------- Мікросервіси (Лаб. 02–04) ----------
// Ім'я ресурсу (enrollments-api...) — логічне ім'я для service discovery: Gateway й Aggregator
// звертаються на http://enrollments-api, а не на localhost:порт. Порти Aspire призначає сам.
// WithReference — передати рядок підключення / адресу; WaitFor — не стартувати, доки залежність не готова.
// WithHttpHealthCheck — «готовий» означає, що /health відповідає (міграції, індекси вже застосовано).

var enrollments = builder.AddProject<Projects.Enrollments_Api>("enrollments-api")
    .WithReference(enrollmentsDb)
    .WaitFor(enrollmentsDb)
    .WithHttpHealthCheck("/health");

var courses = builder.AddProject<Projects.Courses_Api>("courses-api")
    .WithReference(coursesDb)
    .WaitFor(coursesDb)
    .WithHttpHealthCheck("/health");

var reviews = builder.AddProject<Projects.Reviews_WebApi>("reviews-api")
    .WithReference(feedbackDb)
    .WaitFor(feedbackDb)
    .WithHttpHealthCheck("/health");

// ---------- Нові компоненти платформи (Лаб. 05) ----------

// Aggregator паралельно опитує всі три сервіси, тож отримує посилання на кожен
var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithReference(enrollments)
    .WithReference(courses)
    .WithReference(reviews)
    .WaitFor(enrollments)
    .WaitFor(courses)
    .WaitFor(reviews)
    .WithHttpHealthCheck("/health");

// Gateway — єдина точка входу з фіксованим портом 5000. Посилання на всі сервіси й Aggregator
// потрібні YARP для маршрутизації. Порядок старту: БД -> сервіси -> Aggregator -> Gateway.
builder.AddProject<Projects.Platform_ApiGateway>("gateway")
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithReference(enrollments)
    .WithReference(courses)
    .WithReference(reviews)
    .WithReference(aggregator)
    .WaitFor(aggregator)
    .WithHttpHealthCheck("/health")
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
