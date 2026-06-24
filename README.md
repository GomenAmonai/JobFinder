# JobRadar

Полноценная платформа поиска работы для remote .NET / backend / full-stack ролей:
агрегатор вакансий с нескольких бордов **+** трекер откликов кандидата **+**
сторона работодателя (нативные вакансии и управление откликами) — с авторизацией
и обновлениями в реальном времени.

**Стек:** .NET 10 · ASP.NET Core (Minimal API + SignalR) · EF Core 10 + PostgreSQL ·
Kafka (Confluent.Kafka, KRaft) · OpenTelemetry · Clean Architecture · React + Vite +
TypeScript + TanStack Query · Docker Compose · xUnit + Testcontainers.

---

## Что это

Коллекторы на разных языках складывают сырьё в Kafka → .NET-воркер идемпотентно
нормализует и сохраняет его в Postgres → REST API отдаёт вакансии с фильтрами →
новые/изменённые вакансии и изменения статусов прилетают в SPA по SignalR.

Три роли в одном продукте:

- **Гость** — публичная лента вакансий с фильтрами (рынок/грейд/стек/поиск), live-обновления.
- **Кандидат** — JWT-аккаунт, сохранённые фильтры, отклик на вакансию, личный
  пайплайн откликов (state-machine статусов), live-уведомления о матчах.
- **Работодатель** — постит нативные вакансии на JobRadar, видит входящие отклики
  (с контактами кандидатов), двигает их по стадиям — кандидат получает push.

> Вакансии агрегируются с внешних бордов, поэтому «настоящий отклик» имеет смысл
> только для нативных (запощенных работодателем) вакансий. По агрегированным отклик —
> это личный трекер кандидата. Это «гибрид»: трекер + посредник в одной модели.

## Showpiece: идемпотентный приём под конкуренцией

Одна и та же вакансия приходит повторно и одновременно из нескольких источников/потребителей.
Гарантия — **ровно одна строка** на `(Source, ExternalId)`, без дублей и без потери апдейтов:

- Уникальный индекс `(Source, ExternalId)` физически запрещает дубль.
- [`VacancyUpsertService`](src/JobRadar.Infrastructure/Ingestion/VacancyUpsertService.cs)
  использует стратегию **update-first**: атомарный set-based `ExecuteUpdate`
  (не читает строку, не штормит ретраями под нагрузкой), а на гонке вставки ловит
  нарушение уникального индекса (Postgres `23505`) и повторяет цикл.
- Optimistic concurrency через системный столбец `xmin` задействован там, где он уместен —
  на интерактивных правках (сохранённые фильтры, смена статуса отклика): устаревшая
  клиентская версия даёт `409`, а не тихую перезапись.

Проверяется тестом [`VacancyUpsertConcurrencyTests`](tests/JobRadar.IntegrationTests/VacancyUpsertConcurrencyTests.cs):
**50 конкурентных писателей → 1 строка, ровно 1 INSERT.**

## Архитектура

```
[ Коллекторы ]            [ Kafka ]                  [ .NET ]                     [ Клиент ]
 RemotiveCollector ─push─► vacancies.raw ──► Worker: VacancyConsumer
 RemoteOkCollector                          ├─ нормализация (рынок/грейд/стек,
 (+ Python-скрейперы)                       │   парсинг зарплат, dedup-ключ)
                                            ├─ идемпотентный upsert ──────► Postgres
                                            ├─ ошибки ──► vacancies.dead-letter
                                            └─ publish ─► vacancies.changed
                                                               │
                                          Api: VacancyChangedConsumer ◄┘
                                            └─ IHubContext ─► SignalR ─► React (live)
                                          Api: REST (/vacancies, /auth, /me, /employer)
                                            └─────────────────────────────► React SPA
```

Worker и Api — **разные процессы**, поэтому live-обновления идут через Kafka-бэкплейн
(`vacancies.changed`), а не in-process: воркер не знает о SignalR, API не знает о приёме.

**Слои (Clean Architecture, зависимости внутрь):**

- `JobRadar.Domain` — сущности (`Vacancy`, `User`, `JobApplication`, `SavedFilter`, …)
  и чистая доменная логика (`ApplicationStatusTransitions` — state-machine статусов).
- `JobRadar.Application` — контракты, DTO, интерфейсы, нормализация
  (`VacancyNormalization`, `SalaryParser`, `DedupKeyBuilder`).
- `JobRadar.Infrastructure` — EF Core, миграции, сервисы (upsert, auth, фильтры, отклики, employer).
- `JobRadar.Api` — Minimal API + SignalR-хаб + OpenAPI/Scalar + JWT + rate limiting + OpenTelemetry.
- `JobRadar.Worker` — Kafka consumer + C#-коллекторы (BackgroundService, Polly).
- `frontend/` — React + Vite + TS SPA (TanStack Query + `@microsoft/signalr`).

## API

Полный интерактивный справочник — Scalar UI на `GET /scalar/v1` (в Development).

| Метод | Маршрут | Доступ |
|---|---|---|
| `GET` | `/vacancies?market=&level=&stack=&q=&page=&pageSize=` | публично |
| `POST` | `/auth/register` · `/auth/login` · `/auth/refresh` | публично (rate-limited) |
| `GET` | `/auth/me` | auth |
| `GET/POST/PUT/DELETE` | `/me/filters[/{id}]` | auth |
| `POST` | `/vacancies/{id}/applications` | auth |
| `GET/PATCH/DELETE` | `/me/applications[/{id}][/status]` | auth |
| `POST` | `/employer/vacancies` | роль `Employer` |
| `GET` | `/employer/applications` | роль `Employer` |
| `PATCH` | `/employer/applications/{id}/status` | роль `Employer` |
| `GET` | `/hubs/vacancies` | SignalR (токен в `?access_token=`) |

**SignalR-события (сервер → клиент):** `VacancyChanged` (всем — live-лента),
`MatchedVacancy` (таргетированно — новая вакансия под сохранённым фильтром),
`ApplicationStatusChanged` (таргетированно — работодатель сменил статус отклика).

Полный контракт для UI-агентов — в [docs/claude-design-brief.md](docs/claude-design-brief.md).

## Безопасность и прод-готовность

- **JWT** (HS256): валидация issuer/audience/lifetime/подписи; refresh-токены опаковые,
  в БД только SHA-256 хеш, ротация с **инвалидацией всей цепочки при reuse** (RFC 9700).
- **Авторизация:** эндпоинты скоупятся по пользователю (нет BOLA/IDOR); `/employer/*`
  под `RequireRole("Employer")` — покрыто HTTP-тестами (anon 401 / candidate 403 / employer 200).
- **Хардening (вне Development):** `UseExceptionHandler` + ProblemDetails (без stack-trace),
  HSTS + HTTPS-redirect, fail-fast на дев/слабом JWT-ключе, security-заголовки
  (nosniff/frame-options/referrer), CORS origins из конфигурации.
- **Rate limiting:** `/auth/*` 10/мин/IP, `/vacancies` 120/мин/IP.
- **Защита от DoS:** LIKE-экранирование + лимит длины поиска, пагинация ≤ 100, лимит длины пароля.
- **Надёжность приёма:** at-least-once Kafka + идемпотентный upsert; необработанные
  сообщения уходят в **dead-letter** топик, а не теряются.
- **Наблюдаемость:** OpenTelemetry (traces + metrics: ASP.NET / HttpClient / Npgsql / runtime),
  экспорт по OTLP при заданном `OTEL_EXPORTER_OTLP_ENDPOINT`.

Прошло state security-аудит по OWASP API Security Top 10 (BOLA, broken auth, BOPLA,
resource consumption, function-level authz, SSRF, misconfiguration).

## Запуск

Требуется Docker и .NET 10 SDK (репозиторий пинит версию через `global.json`).

```bash
# 1. Инфраструктура: Postgres (host-порт 5433) + Kafka (+ kafka-ui на :8080)
docker compose up -d

# 2. Применить миграции
dotnet ef database update -p src/JobRadar.Infrastructure -s src/JobRadar.Api

# 3. Воркер: коллекторы → Kafka → Postgres → vacancies.changed
dotnet run --project src/JobRadar.Worker

# 4. API (REST + SignalR; Scalar UI на /scalar/v1) — на http://localhost:5088
dotnet run --project src/JobRadar.Api

# 5. Фронт — на http://localhost:5173
cd frontend && npm install && npm run dev
```

Фронт ходит в API по `VITE_API_URL` (по умолчанию `http://localhost:5088`) — см. `frontend/env.example`.

> Локальные dev-значения (Postgres-креды, JWT-ключ в `appsettings`) — throwaway, в git
> только для разработки. Перед реальным деплоем: секреты в env/secret-store, верификация
> работодателя (сейчас роль self-service — демо), TTL/retention вакансий (требует смены
> cascade-FK), обработчик/реплей dead-letter.

## Тесты

```bash
dotnet test tests/JobRadar.UnitTests          # быстрые, без Docker (27)
dotnet test tests/JobRadar.IntegrationTests   # Testcontainers, нужен Docker (52)
```

- **Unit (27):** чистая доменная/прикладная логика — нормализация, парсинг зарплат,
  dedup-ключ, релевантность, state-machine статусов, маппинг.
- **Integration (52):** реальный Postgres через Testcontainers — идемпотентный upsert
  под конкуренцией, query/дедуп, auth + ротация refresh, сохранённые фильтры (xmin-конфликт),
  отклики, employer-сценарии (IDOR-скоупинг), и HTTP-level role-enforcement через
  `WebApplicationFactory`.

## Frontend

SPA на React + Vite + TS: публичная лента с фильтрами, авторизация (login/register),
сохранённые фильтры, отклик + доска статусов, сторона работодателя, live через SignalR.
Данные — TanStack Query (кэш, инвалидация по SignalR), доступ к API — типизированный
клиент с авто-refresh токена. Дизайн (тёмная тема, токены, Geist) сделан в
**Claude Design** и подключён как слой стилей в `frontend/src/styles/`.
