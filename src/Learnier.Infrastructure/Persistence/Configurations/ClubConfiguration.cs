using Learnier.Domain.Identity;
using Learnier.Domain.Social;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class ClubConfiguration : IEntityTypeConfiguration<Club>
{
    public void Configure(EntityTypeBuilder<Club> builder)
    {
        builder.ToTable("clubs");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Description)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(c => c.IsActive)
            .IsRequired();

        builder.HasOne(c => c.Subject)
            .WithMany()
            .HasForeignKey(c => c.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(c => c.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new
        {
            c.OrganizationId,
            c.SubjectId
        }).IsUnique();

        builder.HasMany(c => c.Rooms)
            .WithOne(room => room.Club)
            .HasForeignKey(room => room.ClubId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Club.Rooms))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(c => c.DomainEvents);
    }
}

internal sealed class ClubRoomConfiguration : IEntityTypeConfiguration<ClubRoom>
{
    public void Configure(EntityTypeBuilder<ClubRoom> builder)
    {
        builder.ToTable("club_rooms");
        builder.HasKey(room => room.Id);

        builder.Property(room => room.Name).HasMaxLength(100).IsRequired();
        builder.Property(room => room.Type).HasConversion<string>().HasMaxLength(16).IsRequired();

        builder.HasIndex(room => new { room.ClubId, room.Name }).IsUnique();
        builder.HasIndex(room => new { room.ClubId, room.SortOrder });
    }
}

internal sealed class ClubMessageConfiguration : IEntityTypeConfiguration<ClubMessage>
{
    public void Configure(EntityTypeBuilder<ClubMessage> builder)
    {
        builder.ToTable("club_messages");
        builder.HasKey(message => message.Id);

        builder.Property(message => message.Body).HasMaxLength(2000).IsRequired();
        builder.HasIndex(message => new { message.RoomId, message.CreatedAt });

        builder.HasOne(message => message.Room)
            .WithMany()
            .HasForeignKey(message => message.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(message => message.AuthorUser)
            .WithMany()
            .HasForeignKey(message => message.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
