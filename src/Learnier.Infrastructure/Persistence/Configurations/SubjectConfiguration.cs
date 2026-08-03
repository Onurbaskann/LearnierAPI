using Learnier.Domain.Catalog;
using Learnier.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        builder.ToTable("subjects");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Slug).HasMaxLength(100).IsRequired();

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // Slug yalnizca kurum icinde benzersizdir: iki kurum da "ingilizce" kullanabilir.
        builder.HasIndex(s => new { s.OrganizationId, s.Slug }).IsUnique();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(s => s.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Alt alanlari olan bir alan silinemez; once alt kirilim tasinmali.
        builder.HasOne<Subject>()
            .WithMany()
            .HasForeignKey(s => s.ParentSubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_subjects_parent_not_self",
            @"parent_subject_id IS NULL OR parent_subject_id <> id"));

        builder.Ignore(s => s.DomainEvents);
    }
}
