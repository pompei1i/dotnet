using Courses.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Courses.Dal.Configurations;

public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("Courses", table =>
        {
            table.HasCheckConstraint("CK_Courses_Price", "[Price] >= 0");
            table.HasCheckConstraint("CK_Courses_Level", "[Level] IN (N'Beginner', N'Intermediate', N'Advanced')");
            table.HasCheckConstraint("CK_Courses_AverageRating", "[AverageRating] BETWEEN 1 AND 5");
            table.HasCheckConstraint("CK_Courses_ReviewsCount", "[ReviewsCount] >= 0");
            table.HasCheckConstraint("CK_Courses_PublishedAt", "[IsPublished] = 0 OR [PublishedAt] IS NOT NULL");
        });

        builder.HasKey(course => course.Id);

        builder.Property(course => course.Title).IsRequired().HasMaxLength(Course.TitleMaxLength);
        builder.Property(course => course.Slug).IsRequired().HasMaxLength(Course.SlugMaxLength);
        builder.Property(course => course.Price).HasColumnType("decimal(18,2)");
        builder.Property(course => course.Level).HasConversion<string>().HasMaxLength(20);   // enum зберігається рядком
        builder.Property(course => course.Language).IsRequired().HasMaxLength(Course.LanguageMaxLength);
        builder.Property(course => course.PublishedAt).HasPrecision(3);
        builder.Property(course => course.AverageRating).HasColumnType("decimal(3,2)");

        builder.Property(course => course.CreatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(course => course.CreatedBy).HasMaxLength(100);
        builder.Property(course => course.UpdatedAt).HasPrecision(3);
        builder.Property(course => course.UpdatedBy).HasMaxLength(100);
        builder.Property(course => course.RowVersion).IsRowVersion();   // оптимістична конкурентність

        // М'яке видалення: видалені курси не потрапляють у жоден запит без IgnoreQueryFilters()
        builder.HasQueryFilter(course => !course.IsDeleted);

        // 1:1 Courses <-> CourseDetails: ключ CourseDetails.CourseId є водночас зовнішнім ключем
        builder.HasOne(course => course.Detail)
            .WithOne(detail => detail.Course)
            .HasForeignKey<CourseDetail>(detail => detail.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        // 1:N Courses -> Lessons
        builder.HasMany(course => course.Lessons)
            .WithOne(lesson => lesson.Course)
            .HasForeignKey(lesson => lesson.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        // Запит: сторінка курсу за адресою /courses/{slug}
        builder.HasIndex(course => course.Slug).IsUnique().HasDatabaseName("UQ_Courses_Slug");

        // Запит каталогу: опубліковані курси рівня, відсортовані за ціною. Фільтрований і покриваючий
        builder.HasIndex(course => new { course.Level, course.Price })
            .HasDatabaseName("IX_Courses_Level_Price")
            .HasFilter("[IsPublished] = 1 AND [IsDeleted] = 0")
            .IncludeProperties(course => new { course.Title, course.Slug, course.AverageRating, course.ReviewsCount });

        // Запит кабінету викладача: усі його курси, зокрема чернетки
        builder.HasIndex(course => course.InstructorId)
            .HasDatabaseName("IX_Courses_InstructorId")
            .HasFilter("[IsDeleted] = 0")
            .IncludeProperties(course => new { course.Title, course.IsPublished });

        // Запит пошуку: курси, назва яких починається з введеного тексту
        builder.HasIndex(course => course.Title).HasDatabaseName("IX_Courses_Title");
    }
}
