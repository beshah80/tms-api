using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmsApi.Entities;

namespace TmsApi.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<TmsApi.Entities.Student>
{
    public void Configure(EntityTypeBuilder<TmsApi.Entities.Student> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.RegistrationNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(s => s.RegistrationNumber)
            .IsUnique();

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.GPA)
            .HasPrecision(3, 2);

        // Exercise 8: shadow audit column — exists in DB, invisible in the entity class
        builder.Property<DateTime>("LastUpdated");

        // Exercise 8: concurrency token — prevents two users overwriting each other
        builder.Property(s => s.Version).IsRowVersion();

        // Exercise 9: soft-delete filter — IsDeleted students hidden from all normal queries
        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
