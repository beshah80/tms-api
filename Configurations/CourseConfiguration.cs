using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmsApi.Entities;

namespace TmsApi.Configurations;

public class CourseConfiguration : IEntityTypeConfiguration<TmsApi.Entities.Course>
{
    public void Configure(EntityTypeBuilder<TmsApi.Entities.Course> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Code)
            .IsRequired()
            .HasMaxLength(10);

        builder.HasIndex(c => c.Code)
            .IsUnique();

        builder.Property(c => c.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasMany(c => c.Enrollments)
            .WithOne(e => e.Course)
            .HasForeignKey(e => e.CourseId);
    }
}
