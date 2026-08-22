using Learnier.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class LevelConfiguration : IEntityTypeConfiguration<Level>
{
    public void Configure(EntityTypeBuilder<Level> builder)
    {
        builder.ToTable("levels");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Code).HasMaxLength(32).IsRequired();
        builder.Property(l => l.Name).HasMaxLength(100).IsRequired();

        builder.HasIndex(l => new { l.SubjectId, l.Code }).IsUnique();

        // Seviye listeleri her zaman sirali okunur.
        builder.HasIndex(l => new { l.SubjectId, l.SortOrder });

        builder.HasOne(l => l.Subject)
            .WithMany()
            .HasForeignKey(l => l.SubjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
