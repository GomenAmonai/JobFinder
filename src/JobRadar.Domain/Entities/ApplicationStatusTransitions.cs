namespace JobRadar.Domain.Entities;

/// <summary>
/// Допустимые переходы статуса отклика. Кандидат сам ведёт пайплайн, но переходы
/// валидируются на сервере, чтобы статус не «прыгал» назад или из терминального
/// состояния. Правило: среди активных стадий — только вперёд по пайплайну; в
/// терминальное состояние (Rejected/Withdrawn) — из любой активной; из
/// терминального — никуда.
/// </summary>
public static class ApplicationStatusTransitions
{
    public static bool IsTerminal(ApplicationStatus status)
        => status is ApplicationStatus.Rejected or ApplicationStatus.Withdrawn;

    public static bool CanTransition(ApplicationStatus from, ApplicationStatus to)
    {
        if (from == to) return false;
        if (IsTerminal(from)) return false;
        if (IsTerminal(to)) return true;
        return to > from; // порядок объявления enum = порядок пайплайна
    }
}
