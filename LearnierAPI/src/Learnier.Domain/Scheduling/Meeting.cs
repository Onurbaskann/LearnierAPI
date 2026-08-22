using Learnier.Domain.Common;

namespace Learnier.Domain.Scheduling;

/// <summary>Bir ders oturumunun canli gorusme saglayicisindaki kalici karsiligi.</summary>
public sealed class Meeting : AggregateRoot, IAuditableEntity, ITenantScoped
{
    private Meeting()
    {
        Provider = string.Empty;
    }

    public Guid OrganizationId { get; private set; }

    public Guid SessionId { get; private set; }

    public string Provider { get; private set; }

    public string? ProviderMeetingId { get; private set; }

    public MeetingStatus Status { get; private set; }

    /// <summary>Ogrenciye yalnizca katilim penceresinde dondurulur.</summary>
    public string? JoinUrl { get; private set; }

    /// <summary>Yalnizca oturuma atanmis egitmenlere dondurulmelidir.</summary>
    public string? HostUrl { get; private set; }

    public DateTimeOffset StartsAt { get; private set; }

    public DateTimeOffset EndsAt { get; private set; }

    public int ProvisioningAttemptCount { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset? ProvisionedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public int CancellationAttemptCount { get; private set; }

    public DateTimeOffset? ProviderCancelledAt { get; private set; }

    public LessonSession Session { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public static Meeting Request(
        Guid organizationId,
        Guid sessionId,
        string provider,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt)
    {
        if (organizationId == Guid.Empty || sessionId == Guid.Empty)
        {
            throw new ArgumentException("Kurum ve oturum kimlikleri bos olamaz.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        if (endsAt <= startsAt)
        {
            throw new ArgumentException("Toplanti bitisi baslangictan sonra olmalidir.", nameof(endsAt));
        }

        return new Meeting
        {
            OrganizationId = organizationId,
            SessionId = sessionId,
            Provider = provider.Trim().ToLowerInvariant(),
            StartsAt = startsAt,
            EndsAt = endsAt,
            Status = MeetingStatus.Pending
        };
    }

    public void StartProvisioning()
    {
        if (Status is not (MeetingStatus.Pending or MeetingStatus.Failed))
        {
            throw new InvalidOperationException("Yalnizca bekleyen veya basarisiz toplanti hazirlanabilir.");
        }

        ProvisioningAttemptCount++;
        LastError = null;
        Status = MeetingStatus.Provisioning;
    }

    public void MarkReady(
        string providerMeetingId,
        string joinUrl,
        string hostUrl,
        DateTimeOffset provisionedAt)
    {
        EnsureProvisioning();
        ArgumentException.ThrowIfNullOrWhiteSpace(providerMeetingId);
        ValidateUrl(joinUrl, nameof(joinUrl));
        ValidateUrl(hostUrl, nameof(hostUrl));

        ProviderMeetingId = providerMeetingId.Trim();
        JoinUrl = joinUrl.Trim();
        HostUrl = hostUrl.Trim();
        ProvisionedAt = provisionedAt;
        Status = MeetingStatus.Ready;
    }

    public void MarkFailed(string error)
    {
        EnsureProvisioning();
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        LastError = error.Trim();
        Status = MeetingStatus.Failed;
    }

    public void Cancel(DateTimeOffset cancelledAt)
    {
        if (Status is MeetingStatus.Ended)
        {
            throw new InvalidOperationException("Bitmis toplanti iptal edilemez.");
        }

        CancelledAt = cancelledAt;
        Status = MeetingStatus.Cancelled;

        // Saglayicida henuz bir toplanti olusmadiysa dis iptal islemi gerekmez.
        if (ProviderMeetingId is null)
        {
            ProviderCancelledAt = cancelledAt;
        }
    }

    public void StartCancellationAttempt()
    {
        if (Status is not MeetingStatus.Cancelled
            || ProviderMeetingId is null
            || ProviderCancelledAt is not null)
        {
            throw new InvalidOperationException("Toplanti saglayici iptaline uygun degil.");
        }

        CancellationAttemptCount++;
        LastError = null;
    }

    public void MarkProviderCancelled(DateTimeOffset cancelledAt)
    {
        if (Status is not MeetingStatus.Cancelled || ProviderMeetingId is null)
        {
            throw new InvalidOperationException("Toplanti iptal durumunda olmalidir.");
        }

        ProviderCancelledAt = cancelledAt;
        LastError = null;
    }

    public void MarkProviderCancellationFailed(string error)
    {
        if (Status is not MeetingStatus.Cancelled || ProviderMeetingId is null)
        {
            throw new InvalidOperationException("Toplanti iptal durumunda olmalidir.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        LastError = error.Trim();
    }

    public void MarkEnded() => Status = MeetingStatus.Ended;

    private void EnsureProvisioning()
    {
        if (Status is not MeetingStatus.Provisioning)
        {
            throw new InvalidOperationException("Toplanti hazirlaniyor durumunda olmalidir.");
        }
    }

    private static void ValidateUrl(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (!Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            throw new ArgumentException("Toplanti adresi mutlak bir URL olmalidir.", parameterName);
        }
    }
}
