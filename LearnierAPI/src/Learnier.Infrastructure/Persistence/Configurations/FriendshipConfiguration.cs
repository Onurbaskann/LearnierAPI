using Learnier.Domain.Identity;
using Learnier.Domain.Social;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class FriendshipConfiguration : IEntityTypeConfiguration<Friendship>
{
    public void Configure(EntityTypeBuilder<Friendship> builder)
    {
        builder.ToTable("friendships");
        builder.HasKey(friendship => friendship.Id);
        builder.Ignore(friendship => friendship.DomainEvents);

        builder.Property(friendship => friendship.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // Siralanmis kullanici cifti, ters yonde ikinci bir kaydi da engeller.
        builder.HasIndex(friendship => new { friendship.FirstUserId, friendship.SecondUserId })
            .IsUnique();
        builder.HasIndex(friendship => new { friendship.RequestedByUserId, friendship.Status });

        builder.HasOne(friendship => friendship.FirstUser)
            .WithMany()
            .HasForeignKey(friendship => friendship.FirstUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(friendship => friendship.SecondUser)
            .WithMany()
            .HasForeignKey(friendship => friendship.SecondUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(friendship => friendship.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "ck_friendships_distinct_users",
                "first_user_id <> second_user_id");
            table.HasCheckConstraint(
                "ck_friendships_requester_is_participant",
                "requested_by_user_id = first_user_id OR requested_by_user_id = second_user_id");
            table.HasCheckConstraint(
                "ck_friendships_response_matches_status",
                "(status = 'Pending') = (responded_at IS NULL)");
        });
    }
}
