using Learnier.Domain.Social;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class DirectMessageConfiguration : IEntityTypeConfiguration<DirectMessage>
{
    public void Configure(EntityTypeBuilder<DirectMessage> builder)
    {
        builder.ToTable("direct_messages");
        builder.HasKey(message => message.Id);

        builder.Property(message => message.Body)
            .HasMaxLength(DirectMessage.MaxBodyLength)
            .IsRequired();

        // Konusma dokumu: karsi tarafla olan mesajlar tarih sirasinda okunur.
        builder.HasIndex(message => new
        {
            message.SenderUserId,
            message.RecipientUserId,
            message.SentAt,
        });

        // Rozet sorgusu: alicinin okunmamislari. ReadAt filtreye girdigi icin
        // index'e dahil edilir, aksi halde her yoklamada tablo taranirdi.
        builder.HasIndex(message => new { message.RecipientUserId, message.ReadAt });

        builder.HasOne(message => message.SenderUser)
            .WithMany()
            .HasForeignKey(message => message.SenderUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(message => message.RecipientUser)
            .WithMany()
            .HasForeignKey(message => message.RecipientUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(table => table.HasCheckConstraint(
            "ck_direct_messages_distinct_users",
            "sender_user_id <> recipient_user_id"));
    }
}
