using Courses.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Courses.Dal.Configurations;

/// <summary>Проміжна сутність M:N Courses <-> Categories з власними полями AddedAt та IsPrimary.</summary>
public class CourseCategoryConfiguration : IEntityTypeConfiguration<CourseCategory>
{
    public void Configure(EntityTypeBuilder<CourseCategory> builder)
    {
        builder.ToTable("CourseCategories");

        builder.HasKey(link => new { link.CourseId, link.CategoryId });   // складений ключ

        builder.Property(link => link.AddedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");

        // Каскад від курсу доречний: зв'язок не існує без курсу
        builder.HasOne(link => link.Course)
            .WithMany(course => course.CourseCategories)
            .HasForeignKey(link => link.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        // Від категорії каскаду немає: категорію, в якій є курси, видалити не можна
        builder.HasOne(link => link.Category)
            .WithMany(category => category.CourseCategories)
            .HasForeignKey(link => link.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Запит: курси категорії, спершу нещодавно додані
        builder.HasIndex(link => new { link.CategoryId, link.AddedAt })
            .HasDatabaseName("IX_CourseCategories_CategoryId")
            .IsDescending(false, true);

        // Запит: основна категорія курсу. Унікальний фільтрований індекс водночас гарантує,
        // що основна категорія в курсу лише одна
        builder.HasIndex(link => link.CourseId)
            .HasDatabaseName("UX_CourseCategories_Primary")
            .IsUnique()
            .HasFilter("[IsPrimary] = 1")
            .IncludeProperties(link => link.CategoryId);

        builder.HasQueryFilter(link => !link.Course.IsDeleted);
    }
}
