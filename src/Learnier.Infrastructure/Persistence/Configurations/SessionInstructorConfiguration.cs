using Learnier.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class SessionInstructorConfiguration : IEntityTypeConfiguration<SessionInstructor>
{
    public void Configure(EntityTypeBuilder<SessionInstructor> builder)
    {
        builder.ToTable("session_instructors");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Role)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(i => new { i.SessionId, i.InstructorProfileId }).IsUnique();

        // Egitmenin programi ve cakisma kontrolu bu index uzerinden calisir.
        // Ayni saatte iki derse atanmayi normal bir unique index onleyemez;
        // kontrol islem icinde yapilir (kaynak dokuman, 13. bolum).
        builder.HasIndex(i => new { i.InstructorProfileId, i.SessionId });

        builder.HasOne(i => i.InstructorProfile)
            .WithMany()
            .HasForeignKey(i => i.InstructorProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
