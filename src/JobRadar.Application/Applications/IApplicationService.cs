namespace JobRadar.Application.Applications;

public interface IApplicationService
{
    Task<ApplyOutcome> ApplyAsync(Guid userId, int vacancyId, CreateApplicationRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ApplicationDto>> ListAsync(Guid userId, CancellationToken ct = default);
    Task<ApplicationDto?> GetAsync(Guid userId, int id, CancellationToken ct = default);
    Task<StatusChangeOutcome> ChangeStatusAsync(Guid userId, int id, UpdateApplicationStatusRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid userId, int id, CancellationToken ct = default);
}
