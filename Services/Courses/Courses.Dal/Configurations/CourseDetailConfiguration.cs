using Courses.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Courses.Dal.Configurations;

public class CourseDetailConfiguration : IEntityTypeConfiguration<CourseDetail>
{
    public void Configure(EntityTypeBuilder<CourseDetail> builder)
    {
        builder.ToTable("CourseDetails", table =>
            table.HasCheckConstraint("CK_CourseDetails_DurationHours", "[DurationHours] > 0"));

        // Ключ є водночас зовнішнім ключем на Courses (1:1, налаштовано в CourseConfiguration)
        builder.HasKey(detail => detail.CourseId);

        builder.Property(detail => detail.Description).IsRequired();   // NVARCHAR(MAX)
        builder.Property(detail => detail.Requirements).HasMaxLength(CourseDetail.RequirementsMaxLength);
        builder.Property(detail => detail.DurationHours).HasColumnType("decimal(5,1)");
        builder.Property(detail => detail.CreatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(detail => detail.UpdatedAt).HasPrecision(3);

        // Опис видаленого курсу теж не читається
        builder.HasQueryFilter(detail => !detail.Course.IsDeleted);
    }
}
