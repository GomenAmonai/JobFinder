# JobRadar

Агрегатор удалённых вакансий (.NET/C#, backend, full-stack) по рынкам. Коллекторы
на разных языках складывают сырьё в Kafka; .NET-нормализатор идемпотентно
сохраняет его в Postgres; REST API отдаёт вакансии с фильтрами, а новые/изменённые
вакансии прилетают в UI в реальном времени через SignalR.

**Стек:** .NET 10 · ASP.NET Core (Minimal API + SignalR) · EF Core + PostgreSQL ·
Kafka (Confluent.Kafka) · Clean Architecture · React + Vite + TypeScript ·
Docker Compose. (Phase 3: JWT-авторизация + сохранённые фильтры.)

## Showpiece: идемпотентный приём под конкуренцией

Одна и та же вакансия может прилететь повторно или одновременно из нескольких
потребителей. Гарантия — **ровно одна строка** на `(Source, ExternalId)`, без
дублей и без потери апдейтов:

- Уникальный индекс `(Source, ExternalId)` физически запрещает дубль.
- `VacancyUpsertService` использует стратегию **update-first**: атомарный
  set-based `ExecuteUpdate` (не читает строку, не штормит ретраями под нагрузкой),
  а на гонке вставки ловит нарушение уникального индекса (Postgres `23505`).
- Optimistic concurrency через системный столбец `xmin` сконфигурирован на сущности
  для интерактивных правок (Phase 3), где он уместен.

Проверяется тестом [VacancyUpsertConcurrencyTests](tests/JobRadar.IntegrationTests/VacancyUpsertConcurrencyTests.cs):
50 конкурентных писателей → 1 строка, ровно 1 INSERT.

## Архитектура

```
[ Коллекторы ]            [ Kafka ]                 [ .NET ]                  [ Клиент ]
 RemotiveCollector ─push─► vacancies.raw ──► Worker: VacancyConsumer
 (+ Python, скрейпинг)                       ├─ нормализация
                                             ├─ идемпотентный upsert ──► Postgres
                                             └─ publish ─► vacancies.changed
                                                                │
                                          Api: VacancyChangedConsumer ◄┘
                                             └─ IHubContext ─► SignalR ─► React (live)
                                          Api: GET /vacancies (фильтры) ──────► React
```

Worker и Api — **разные процессы**, поэтому live-обновления идут через Kafka-бэкплейн
(`vacancies.changed`), а не через in-process HubContext. Это корректный
распределённый дизайн: воркер не знает о SignalR, API не знает о приёме.

- `JobRadar.Domain` — сущности (`Vacancy`).
- `JobRadar.Application` — контракты Kafka, DTO/запросы чтения, нормализация, интерфейсы.
- `JobRadar.Infrastructure` — EF Core, миграции, идемпотентный upsert, query-сервис.
- `JobRadar.Api` — REST (`GET /vacancies`) + OpenAPI/Scalar + SignalR-хаб.
- `JobRadar.Worker` — Kafka-консьюмер + C#-коллекторы (BackgroundService, Polly).
- `frontend/` — React + Vite + TS SPA (TanStack Query + `@microsoft/signalr`).

## API

- `GET /health`
- `GET /vacancies?market=&level=&stack=&q=&page=1&pageSize=20` → `PagedResult<VacancyDto>`
- `GET /hubs/vacancies` — SignalR; сервер шлёт клиентам `VacancyChanged`
- `GET /scalar/v1` — OpenAPI UI (Development)

## Запуск

Требуется Docker и .NET 10 SDK (репозиторий пинит версию через `global.json`).

```bash
# 1. Инфраструктура: Postgres (host-порт 5433, чтобы не конфликтовать с локальным
#    Postgres на 5432) + Kafka (+ kafka-ui на :8080)
docker compose up -d

# 2. Применить миграции
dotnet ef database update -p src/JobRadar.Infrastructure -s src/JobRadar.Api

# 3. Воркер: собирает вакансии → Kafka → Postgres → vacancies.changed
dotnet run --project src/JobRadar.Worker

# 4. API (Scalar UI на /scalar/v1, SignalR на /hubs/vacancies)
dotnet run --project src/JobRadar.Api

# 5. Фронт (http://localhost:5173)
cd frontend && npm install && npm run dev
```

> Учётки Postgres в `appsettings`/`docker-compose` — локальные throwaway-значения.
> Для не-локального деплоя: вынести в секреты/env, добавить rate limiting и
> `UseExceptionHandler`, origin CORS брать из конфигурации.

## Тесты

```bash
dotnet test tests/JobRadar.UnitTests          # быстрые, без Docker
dotnet test tests/JobRadar.IntegrationTests    # Testcontainers (нужен Docker)
```
