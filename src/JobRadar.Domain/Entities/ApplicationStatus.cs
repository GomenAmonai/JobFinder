namespace JobRadar.Domain.Entities;

/// <summary>
/// Стадия отклика в пайплайне кандидата. Порядок объявления = порядок прохождения
/// (см. <see cref="ApplicationStatusTransitions"/>): двигаться можно только вперёд
/// либо в терминальное состояние (Rejected/Withdrawn).
/// </summary>
public enum ApplicationStatus
{
    Submitted,
    UnderReview,
    InterviewScheduled,
    OfferExtended,
    Rejected,
    Withdrawn,
}
