using Courses.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Courses.Dal.Configurations;

public class InstructorConfiguration : IEntityTypeConfiguration<Instructor>
{
    public void Configure(EntityTypeBuilder<Instructor> builder)
    {
        builder.ToTable("Instructors", table =>
            table.HasCheckConstraint("CK_Instructors_Email", "[Email] LIKE N'_%@_%._%'"));

        builder.HasKey(instructor => instructor.Id);

        builder.Property(instructor => instructor.Email).IsRequired().HasMaxLength(Instructor.EmailMaxLength);
        builder.Property(instructor => instructor.FullName).IsRequired().HasMaxLength(Instructor.FullNameMaxLength);
        builder.Property(instructor => instructor.Bio).HasMaxLength(Instructor.BioMaxLength);

        builder.Property(instructor => instructor.CreatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(instructor => instructor.CreatedBy).HasMaxLength(100);
        builder.Property(instructor => instructor.UpdatedAt).HasPrecision(3);
        builder.Property(instructor => instructor.UpdatedBy).HasMaxLength(100);
        builder.Property(instructor => instructor.RowVersion).IsRowVersion();

        // Запит: вхід викладача та перевірка дубля під час реєстрації — пошук за Email
        builder.HasIndex(instructor => instructor.Email).IsUnique().HasDatabaseName("UQ_Instructors_Email");

        // 1:N Instructors -> Courses. Restrict: викладача з курсами видалити не можна
        builder.HasMany(instructor => instructor.Courses)
            .WithOne(course => course.Instructor)
            .HasForeignKey(course => course.InstructorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
