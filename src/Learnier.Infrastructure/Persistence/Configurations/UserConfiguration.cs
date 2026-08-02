using Learnier.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            // citext: buyuk/kucuk harf duyarsiz metin tipi. Kaynak dokumanin 2. bolumu
            // geregi e-posta benzersizligi harf durumundan bagimsiz olmali; bunu
            // lower(email) index yerine sutun tipiyle cozmek sorgulari da basitlestirir.
            .HasColumnType("citext")
            .HasMaxLength(320)
            .IsRequired();

        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.Phone).HasMaxLength(32);
        builder.Property(u => u.PasswordHash).HasMaxLength(256);
        builder.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(100).IsRequired();

        builder.Property(u => u.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.HasMany(u => u.Memberships)
            .WithOne(m => m.User)
            .HasForeignKey(m => m.UserId)
            // Kullanici silinirse uyelikleri de gider; ancak silme yerine
            // Suspended durumu tercih edilir.
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(User.Memberships))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(u => u.DomainEvents);
    }
}
