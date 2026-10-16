using Mosen.GroupScheduler.OutboundPolicy;
using Mosen.GroupScheduler.StateMachine;

namespace Mosen.GroupScheduler.Delivery;

public enum DeliveryStatus { Planned, Submitting, Pending, Confirmed, Failed, Unknown, Review }
public enum FailureCategory { OfficialRejected, OfficialRateLimited, NotAuthorized, LocalPreflightRejected, NetworkUncertain, Timeout, TransportLost, UnverifiedResponse }

public sealed record DeliveryAttempt(string AttemptId,string RunId,string StepKey,
    Guid AccountId,string ChatId,string? TopicId,string CorrelationId,DeliveryStatus State,
    string? PlatformMessageId,string DiagnosticCode,int Revision,DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

/// <summary>Provided by a trusted future TDLib adapter ONLY, never accepted from WebView IPC.</summary>
public sealed record OfficialDeliveryReceipt(string CorrelationId,Guid AccountId,string ChatId,
    string? TopicId,string PlatformMessageId,bool SourceVerified);

public sealed record DeliveryOutcome(bool Changed,string Code,DeliveryAttempt Attempt);

public static class DeliveryFailurePolicy
{
    /// <summary>Only positively proven pre-send/official non-delivery is definitive.
    /// Ambiguous network timeouts NEVER become a new send attempt automatically.</summary>
    public static bool IsDefinitive(FailureCategory reason) => reason is
        FailureCategory.OfficialRejected or FailureCategory.OfficialRateLimited or
        FailureCategory.NotAuthorized or FailureCategory.LocalPreflightRejected;

    /// <summary>No automatic message retry. Only read-only or idempotent reconciliation may be repeated.</summary>
    public static bool MayAutomaticallyRetry(string operation) =>
        operation is "read-message-status" or "read-chat-permissions" or "read-auth-state";

    public static bool IsAmbiguous(FailureCategory reason)=>!IsDefinitive(reason);
}
