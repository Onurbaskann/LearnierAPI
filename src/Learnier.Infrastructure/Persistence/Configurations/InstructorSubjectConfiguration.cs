using Learnier.Domain.Catalog;
using Learnier.Domain.Teaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class InstructorSubjectConfiguration : IEntityTypeConfiguration<InstructorSubject>
{
    public void Configure(EntityTypeBuilder<InstructorSubject> builder)
    {
        builder.ToTable("instructor_subjects");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // LevelId bos olabildigi icin benzersizlik NULL'lari esit sayacak sekilde
        // kurulur. Varsayilan davranista iki ayri NULL farkli kabul edilir ve
        // "tum seviyeler" yetkinligi ayni egitmene defalarca eklenebilirdi.
        builder.HasIndex(s => new { s.InstructorProfileId, s.SubjectId, s.LevelId })
            .IsUnique()
            .AreNullsDistinct(false);

        // Ders alanina gore egitmen arama.
        builder.HasIndex(s => new { s.SubjectId, s.Status });

        builder.HasOne(s => s.Subject)
            .WithMany()
            .HasForeignKey(s => s.SubjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Level>()
            .WithMany()
            .HasForeignKey(s => s.LevelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
