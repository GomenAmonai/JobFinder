using Microsoft.AspNetCore.SignalR;

namespace JobRadar.Api.Hubs;

/// <summary>
/// SignalR-хаб live-обновлений вакансий. Серверный метод не нужен — сервер только
/// рассылает клиентам событие <c>VacancyChanged</c> (см. VacancyChangedConsumer).
/// </summary>
public sealed class VacancyHub : Hub;
