using Courses.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Courses.Dal.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");

        builder.HasKey(category => category.Id);

        builder.Property(category => category.Name).IsRequired().HasMaxLength(Category.NameMaxLength);
        builder.Property(category => category.Slug).IsRequired().HasMaxLength(Category.SlugMaxLength);
        builder.Property(category => category.Description).HasMaxLength(Category.DescriptionMaxLength);
        builder.Property(category => category.CreatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");

        // Запит: сторінка категорії /categories/{slug}; обидва значення унікальні
        builder.HasIndex(category => category.Name).IsUnique().HasDatabaseName("UQ_Categories_Name");
        builder.HasIndex(category => category.Slug).IsUnique().HasDatabaseName("UQ_Categories_Slug");

        // Модельний (статичний) сідінг довідника з фіксованими Id — потрапляє в міграцію.
        // Ті самі Id, що в db/p2/seed.sql з Лаб. 01. Анонімні об'єкти — бо в сутності приватні setter-и.
        var seededAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        builder.HasData(
            new { Id = 1, Name = "Програмування", Slug = "programming", Description = "Мови програмування та основи розробки", CreatedAt = seededAt },
            new { Id = 2, Name = "Веб-розробка", Slug = "web-development", Description = "Серверна та клієнтська веб-розробка", CreatedAt = seededAt },
            new { Id = 3, Name = "Бази даних", Slug = "databases", Description = "Реляційні та документні СУБД, ORM", CreatedAt = seededAt },
            new { Id = 4, Name = "DevOps", Slug = "devops", Description = "Контейнери, оркестрація, CI/CD", CreatedAt = seededAt },
            new { Id = 5, Name = "Архітектура ПЗ", Slug = "software-architecture", Description = "Мікросервіси, DDD, патерни проєктування", CreatedAt = seededAt });
    }
}
