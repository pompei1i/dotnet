using Courses.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Courses.Dal.Configurations;

public class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> builder)
    {
        builder.ToTable("Lessons", table =>
        {
            table.HasCheckConstraint("CK_Lessons_OrderIndex", "[OrderIndex] > 0");
            table.HasCheckConstraint("CK_Lessons_DurationMinutes", "[DurationMinutes] > 0");
        });

        builder.HasKey(lesson => lesson.Id);

        builder.Property(lesson => lesson.Title).IsRequired().HasMaxLength(Lesson.TitleMaxLength);
        builder.Property(lesson => lesson.CreatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(lesson => lesson.UpdatedAt).HasPrecision(3);

        // Запит: уроки курсу в порядку проходження; два уроки не можуть мати той самий номер
        builder.HasIndex(lesson => new { lesson.CourseId, lesson.OrderIndex })
            .IsUnique()
            .HasDatabaseName("UQ_Lessons_CourseId_OrderIndex");

        builder.HasQueryFilter(lesson => !lesson.Course.IsDeleted);
    }
}
