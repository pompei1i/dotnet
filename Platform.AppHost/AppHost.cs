var builder = DistributedApplication.CreateBuilder(args);

// SQL Server у контейнері. Том зберігає дані між запусками; пароль sa Aspire генерує сам
// і запам'ятовує в user-secrets цього проєкту.
var sql = builder.AddSqlServer("sql")
    .WithDataVolume();

// Ім'я ресурсу бази збігається з ім'ям рядка підключення в appsettings.json сервісу
// (ConnectionStrings:EnrollmentsDb), тому Aspire передає його в API автоматично —
// змінною оточення ConnectionStrings__EnrollmentsDb, без змін у коді Enrollments.Api.
// Базу створюють скрипти Лаб. 01: вони ідемпотентні, тож повторні запуски безпечні.
var enrollmentsDb = sql.AddDatabase("EnrollmentsDb")
    .WithCreationScript(ReadDatabaseScripts("schema.sql", "procedures.sql", "seed.sql"));

// Варіант без контейнера — уже розгорнута база з Лаб. 01 (рядок підключення в appsettings.json AppHost):
// var enrollmentsDb = builder.AddConnectionString("EnrollmentsDb");

builder.AddProject<Projects.Enrollments_Api>("enrollments-api")
    .WithReference(enrollmentsDb)
    .WaitFor(enrollmentsDb);

// Проєкт №2 — сервіс Courses на EF Core. Aspire лише створює порожню базу; схему й дані
// при старті застосовує сам сервіс: MigrateAsync (міграції Code First) і DbInitializer.
// Ім'я ресурсу збігається з ConnectionStrings:CoursesDb в appsettings.json сервісу.
var coursesDb = sql.AddDatabase("CoursesDb");

builder.AddProject<Projects.Courses_Api>("courses-api")
    .WithReference(coursesDb)
    .WaitFor(coursesDb);

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
