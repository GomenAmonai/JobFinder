From 8139f21313ea3804455c9ace7376218fb3b43f1c Mon Sep 17 00:00:00 2001
From: Daniil Khripunov <daniil.lednik000@mail.ru>
Date: Sat, 17 Oct 2026 03:07:19 +0900
Subject: [PATCH] docs: rewrite README in English, rename repo references to
 JobRadar

---
 README.md                      | 294 +++++++++++++++------------------
 frontend/src/api/config.ts     |   2 +-
 src/JobRadar.Worker/Program.cs |   2 +-
 3 files changed, 131 insertions(+), 167 deletions(-)

diff --git a/README.md b/README.md
index 760115b..8547d83 100644
--- a/README.md
+++ b/README.md
@@ -1,226 +1,190 @@
 # JobRadar
 
-[![CI](https://github.com/GomenAmonai/JobFinder/actions/workflows/ci.yml/badge.svg)](https://github.com/GomenAmonai/JobFinder/actions/workflows/ci.yml)
+[![CI](https://github.com/GomenAmonai/JobRadar/actions/workflows/ci.yml/badge.svg)](https://github.com/GomenAmonai/JobRadar/actions/workflows/ci.yml)
 ![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
 ![Kafka](https://img.shields.io/badge/Kafka-KRaft-231F20?logo=apachekafka&logoColor=white)
 ![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?logo=postgresql&logoColor=white)
 ![React](https://img.shields.io/badge/React-18-149ECA?logo=react&logoColor=white)
 
-Агрегатор remote-вакансий для .NET / backend-разработчиков **+** трекер откликов
-кандидата **+** кабинет работодателя, с обновлениями в реальном времени.
-Коллекторы → Kafka → идемпотентный .NET-воркер → PostgreSQL → REST + SignalR → React.
+A remote job board for .NET and backend developers, with an application tracker for
+candidates and a dashboard for employers. Vacancies are pulled from public job boards,
+pushed through Kafka, stored in PostgreSQL, and streamed to the browser over SignalR.
 
-**[Live demo](https://job-finder-eight-cyan.vercel.app)**: работает без бэкенда, API
-эмулируется прямо в браузере. Вход демо-кандидатом или демо-работодателем в один клик.
-Полный стек поднимается локально через `docker compose up` ([Запуск](#запуск)).
+**[Live demo](https://job-finder-eight-cyan.vercel.app)** runs without a backend: the API
+is emulated in the browser. One click signs you in as a demo candidate or employer.
+To run the real stack locally, see [Running locally](#running-locally).
 
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/feed-dark.png">
-  <img alt="JobRadar: лента вакансий с фильтрами и live-уведомлением о новой вакансии" src="docs/screenshots/feed-light.png">
+  <img alt="JobRadar vacancy feed with filters and a live notification about a new vacancy" src="docs/screenshots/feed-light.png">
 </picture>
 
-### На что посмотреть
+## Highlights
 
-- **Идемпотентный приём под конкуренцией.** 50 параллельных писателей дают 1 строку и
-  ровно 1 INSERT: update-first upsert + уникальный индекс + повтор на `23505`
-  ([тест](tests/JobRadar.IntegrationTests/VacancyUpsertConcurrencyTests.cs)).
-- **Event-driven, разные процессы.** Worker и API связаны только через Kafka
-  (`vacancies.changed`), live-лента доходит до браузера по SignalR, битые сообщения
-  уходят в dead-letter топик.
-- **Безопасность.** Ротация refresh-токенов с инвалидацией всей цепочки при reuse
-  (RFC 9700), скоупинг по пользователю (нет IDOR), rate limiting, проверка по
-  OWASP API Security Top 10.
-- **Optimistic concurrency** на системном столбце `xmin`: устаревшая версия даёт `409`,
-  а не тихую перезапись.
-- **79 тестов в CI:** 27 unit + 52 integration на реальном PostgreSQL (Testcontainers).
+- **Idempotent ingestion under concurrency.** The same vacancy can arrive several times
+  at once. A unique index on `(Source, ExternalId)` plus an update-first upsert with a
+  retry on Postgres `23505` guarantees one row. The
+  [test](tests/JobRadar.IntegrationTests/VacancyUpsertConcurrencyTests.cs) runs 50
+  concurrent writers and checks for exactly one row and one insert.
+- **Separate processes connected by Kafka.** The worker and the API share nothing but
+  the `vacancies.changed` topic. Messages that fail processing go to a dead-letter topic.
+- **Auth done carefully.** Refresh-token rotation that revokes the whole token chain on
+  reuse (RFC 9700), per-user scoping on every endpoint (no IDOR), rate limiting.
+- **Optimistic concurrency** on Postgres `xmin`: a stale update returns `409` instead of
+  silently overwriting.
+- **79 tests in CI:** 27 unit tests and 52 integration tests against real PostgreSQL
+  (Testcontainers).
 
-**Стек:** .NET 10 · ASP.NET Core (Minimal API + SignalR) · EF Core 10 + PostgreSQL ·
-Kafka (Confluent.Kafka, KRaft) · OpenTelemetry · Clean Architecture · React + Vite +
-TypeScript + TanStack Query · Docker Compose · xUnit + Testcontainers · GitHub Actions.
+## Roles
 
----
+- **Guest:** public feed with filters (market, level, stack, text search) and live updates.
+- **Candidate:** account, saved filters, applications, a personal pipeline with status
+  transitions, notifications when a new vacancy matches a saved filter.
+- **Employer:** posts vacancies directly on JobRadar, sees incoming applications, moves
+  them through stages. The candidate gets a live notification on every change.
 
-## Что это
+Aggregated vacancies live on other sites, so applying to one only adds it to the
+candidate's own tracker. Applications to vacancies posted on JobRadar reach the employer.
 
-Коллекторы на разных языках складывают сырьё в Kafka → .NET-воркер идемпотентно
-нормализует и сохраняет его в Postgres → REST API отдаёт вакансии с фильтрами →
-новые/изменённые вакансии и изменения статусов прилетают в SPA по SignalR.
-
-Три роли в одном продукте:
-
-- **Гость** — публичная лента вакансий с фильтрами (рынок/грейд/стек/поиск), live-обновления.
-- **Кандидат** — JWT-аккаунт, сохранённые фильтры, отклик на вакансию, личный
-  пайплайн откликов (state-machine статусов), live-уведомления о матчах.
-- **Работодатель** — постит нативные вакансии на JobRadar, видит входящие отклики
-  (с контактами кандидатов), двигает их по стадиям — кандидат получает push.
-
-> Вакансии агрегируются с внешних бордов, поэтому «настоящий отклик» имеет смысл
-> только для нативных (запощенных работодателем) вакансий. По агрегированным отклик —
-> это личный трекер кандидата. Это «гибрид»: трекер + посредник в одной модели.
-
-## Showpiece: идемпотентный приём под конкуренцией
-
-Одна и та же вакансия приходит повторно и одновременно из нескольких источников/потребителей.
-Гарантия — **ровно одна строка** на `(Source, ExternalId)`, без дублей и без потери апдейтов:
-
-- Уникальный индекс `(Source, ExternalId)` физически запрещает дубль.
-- [`VacancyUpsertService`](src/JobRadar.Infrastructure/Ingestion/VacancyUpsertService.cs)
-  использует стратегию **update-first**: атомарный set-based `ExecuteUpdate`
-  (не читает строку, не штормит ретраями под нагрузкой), а на гонке вставки ловит
-  нарушение уникального индекса (Postgres `23505`) и повторяет цикл.
-- Optimistic concurrency через системный столбец `xmin` задействован там, где он уместен —
-  на интерактивных правках (сохранённые фильтры, смена статуса отклика): устаревшая
-  клиентская версия даёт `409`, а не тихую перезапись.
-
-Проверяется тестом [`VacancyUpsertConcurrencyTests`](tests/JobRadar.IntegrationTests/VacancyUpsertConcurrencyTests.cs):
-**50 конкурентных писателей → 1 строка, ровно 1 INSERT.**
-
-## Архитектура
+## Architecture
 
 ```
-[ Коллекторы ]            [ Kafka ]                  [ .NET ]                     [ Клиент ]
- RemotiveCollector ─push─► vacancies.raw ──► Worker: VacancyConsumer
- RemoteOkCollector                          ├─ нормализация (рынок/грейд/стек,
- (+ Python-скрейперы)                       │   парсинг зарплат, dedup-ключ)
-                                            ├─ идемпотентный upsert ──────► Postgres
-                                            ├─ ошибки ──► vacancies.dead-letter
-                                            └─ publish ─► vacancies.changed
-                                                               │
-                                          Api: VacancyChangedConsumer ◄┘
-                                            └─ IHubContext ─► SignalR ─► React (live)
-                                          Api: REST (/vacancies, /auth, /me, /employer)
-                                            └─────────────────────────────► React SPA
+ Collectors            Kafka                 Worker (.NET)                   Client
+ Remotive    ──push──► vacancies.raw ──────► VacancyConsumer
+ RemoteOK                                    ├─ normalize (market, level, stack,
+                                             │  salary parsing, dedup key)
+                                             ├─ idempotent upsert ─────────► PostgreSQL
+                                             ├─ failures ──► vacancies.dead-letter
+                                             └─ publish ───► vacancies.changed
+                                                                   │
+                                             API: VacancyChangedConsumer ◄─┘
+                                             └─ SignalR hub ───────────────► React (live)
+                                             API: REST ────────────────────► React
 ```
 
-Worker и Api — **разные процессы**, поэтому live-обновления идут через Kafka-бэкплейн
-(`vacancies.changed`), а не in-process: воркер не знает о SignalR, API не знает о приёме.
+The worker knows nothing about SignalR and the API knows nothing about ingestion,
+so live updates go through Kafka rather than in-process events.
 
-**Слои (Clean Architecture, зависимости внутрь):**
+Projects follow Clean Architecture, with dependencies pointing inward:
 
-- `JobRadar.Domain` — сущности (`Vacancy`, `User`, `JobApplication`, `SavedFilter`, …)
-  и чистая доменная логика (`ApplicationStatusTransitions` — state-machine статусов).
-- `JobRadar.Application` — контракты, DTO, интерфейсы, нормализация
-  (`VacancyNormalization`, `SalaryParser`, `DedupKeyBuilder`).
-- `JobRadar.Infrastructure` — EF Core, миграции, сервисы (upsert, auth, фильтры, отклики, employer).
-- `JobRadar.Api` — Minimal API + SignalR-хаб + OpenAPI/Scalar + JWT + rate limiting + OpenTelemetry.
-- `JobRadar.Worker` — Kafka consumer + C#-коллекторы (BackgroundService, Polly).
-- `frontend/` — React + Vite + TS SPA (TanStack Query + `@microsoft/signalr`).
+| Project | Contents |
+|---|---|
+| `JobRadar.Domain` | Entities and pure domain logic, e.g. the application status state machine |
+| `JobRadar.Application` | Contracts, DTOs, normalization, salary parsing, dedup keys |
+| `JobRadar.Infrastructure` | EF Core, migrations, upsert, auth, filters, applications |
+| `JobRadar.Api` | Minimal API, SignalR hub, JWT, rate limiting, OpenTelemetry, Scalar |
+| `JobRadar.Worker` | Kafka consumer and collectors (BackgroundService + Polly) |
+| `frontend/` | React, Vite, TypeScript, TanStack Query, `@microsoft/signalr` |
 
 ## API
 
-Полный интерактивный справочник — Scalar UI на `GET /scalar/v1` (в Development).
+Interactive reference: Scalar UI at `/scalar/v1` (Development only).
 
-| Метод | Маршрут | Доступ |
+| Method | Route | Access |
 |---|---|---|
-| `GET` | `/vacancies?market=&level=&stack=&q=&page=&pageSize=` | публично |
-| `POST` | `/auth/register` · `/auth/login` · `/auth/refresh` | публично (rate-limited) |
-| `GET` | `/auth/me` | auth |
-| `GET/POST/PUT/DELETE` | `/me/filters[/{id}]` | auth |
-| `POST` | `/vacancies/{id}/applications` | auth |
-| `GET/PATCH/DELETE` | `/me/applications[/{id}][/status]` | auth |
-| `POST` | `/employer/vacancies` | роль `Employer` |
-| `GET` | `/employer/applications` | роль `Employer` |
-| `PATCH` | `/employer/applications/{id}/status` | роль `Employer` |
-| `GET` | `/hubs/vacancies` | SignalR (токен в `?access_token=`) |
-
-**SignalR-события (сервер → клиент):** `VacancyChanged` (всем — live-лента),
-`MatchedVacancy` (таргетированно — новая вакансия под сохранённым фильтром),
-`ApplicationStatusChanged` (таргетированно — работодатель сменил статус отклика).
-
-## Безопасность и прод-готовность
-
-- **JWT** (HS256): валидация issuer/audience/lifetime/подписи; refresh-токены опаковые,
-  в БД только SHA-256 хеш, ротация с **инвалидацией всей цепочки при reuse** (RFC 9700).
-- **Авторизация:** эндпоинты скоупятся по пользователю (нет BOLA/IDOR); `/employer/*`
-  под `RequireRole("Employer")` — покрыто HTTP-тестами (anon 401 / candidate 403 / employer 200).
-- **Хардening (вне Development):** `UseExceptionHandler` + ProblemDetails (без stack-trace),
-  HSTS + HTTPS-redirect, fail-fast на дев/слабом JWT-ключе, security-заголовки
-  (nosniff/frame-options/referrer), CORS origins из конфигурации.
-- **Rate limiting:** `/auth/*` 10/мин/IP, `/vacancies` 120/мин/IP.
-- **Защита от DoS:** LIKE-экранирование + лимит длины поиска, пагинация ≤ 100, лимит длины пароля.
-- **Надёжность приёма:** at-least-once Kafka + идемпотентный upsert; необработанные
-  сообщения уходят в **dead-letter** топик, а не теряются.
-- **Наблюдаемость:** OpenTelemetry (traces + metrics: ASP.NET / HttpClient / Npgsql / runtime),
-  экспорт по OTLP при заданном `OTEL_EXPORTER_OTLP_ENDPOINT`.
-
-Прошло state security-аудит по OWASP API Security Top 10 (BOLA, broken auth, BOPLA,
+| `GET` | `/vacancies?market=&level=&stack=&q=&page=&pageSize=` | public |
+| `POST` | `/auth/register`, `/auth/login`, `/auth/refresh` | public, rate-limited |
+| `GET` | `/auth/me` | signed in |
+| `GET/POST/PUT/DELETE` | `/me/filters[/{id}]` | signed in |
+| `POST` | `/vacancies/{id}/applications` | signed in |
+| `GET/PATCH/DELETE` | `/me/applications[/{id}][/status]` | signed in |
+| `POST` | `/employer/vacancies` | `Employer` role |
+| `GET` | `/employer/applications` | `Employer` role |
+| `PATCH` | `/employer/applications/{id}/status` | `Employer` role |
+| `GET` | `/hubs/vacancies` | SignalR, token in `?access_token=` |
+
+SignalR events: `VacancyChanged` (broadcast), `MatchedVacancy` (to the user whose saved
+filter matched), `ApplicationStatusChanged` (to the candidate).
+
+## Security and reliability
+
+- **JWT** (HS256) with issuer, audience, lifetime and signature validation. Refresh
+  tokens are opaque; only their SHA-256 hash is stored.
+- **Authorization:** queries are scoped to the current user; `/employer/*` requires the
+  `Employer` role. HTTP tests cover anonymous (401), candidate (403) and employer (200).
+- **Outside Development:** ProblemDetails without stack traces, HSTS and HTTPS redirect,
+  startup fails on a weak or dev JWT key, security headers, CORS origins from config.
+- **Rate limits:** `/auth/*` 10 requests/min per IP, `/vacancies` 120 requests/min per IP.
+- **Input limits:** escaped `LIKE` search with a length cap, page size ≤ 100, password
+  length cap.
+- **Delivery:** at-least-once Kafka consumption plus idempotent upsert; failed messages
+  are kept in the dead-letter topic.
+- **Observability:** OpenTelemetry traces and metrics (ASP.NET Core, HttpClient, Npgsql,
+  runtime), exported over OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set.
+
+I went through the OWASP API Security Top 10 as a checklist (BOLA, broken auth, BOPLA,
 resource consumption, function-level authz, SSRF, misconfiguration).
 
-## Запуск
+## Running locally
 
-Требуется Docker и .NET 10 SDK (репозиторий пинит версию через `global.json`).
+Requires Docker and the .NET 10 SDK (pinned in `global.json`).
 
 ```bash
-# 1. Инфраструктура: Postgres (host-порт 5433) + Kafka (+ kafka-ui на :8080)
+# 1. Infrastructure: Postgres (host port 5433), Kafka, kafka-ui on :8080
 docker compose up -d
 
-# 2. Применить миграции
+# 2. Apply migrations
 dotnet ef database update -p src/JobRadar.Infrastructure -s src/JobRadar.Api
 
-# 3. Воркер: коллекторы → Kafka → Postgres → vacancies.changed
+# 3. Worker: collectors → Kafka → Postgres → vacancies.changed
 dotnet run --project src/JobRadar.Worker
 
-# 4. API (REST + SignalR; Scalar UI на /scalar/v1) — на http://localhost:5088
+# 4. API (REST + SignalR) on http://localhost:5088
 dotnet run --project src/JobRadar.Api
 
-# 5. Фронт — на http://localhost:5173
+# 5. Frontend on http://localhost:5173
 cd frontend && npm install && npm run dev
 ```
 
-Фронт ходит в API по `VITE_API_URL` (по умолчанию `http://localhost:5088`) — см. `frontend/env.example`.
+The frontend reads the API address from `VITE_API_URL` (default
+`http://localhost:5088`); see `frontend/env.example`.
 
-> Локальные dev-значения (Postgres-креды, JWT-ключ в `appsettings`) — throwaway, в git
-> только для разработки. Перед реальным деплоем: секреты в env/secret-store, верификация
-> работодателя (сейчас роль self-service — демо), TTL/retention вакансий (требует смены
-> cascade-FK), обработчик/реплей dead-letter.
+> The Postgres credentials and JWT key in `appsettings` are throwaway dev values.
+> Before a real deployment: move secrets to env vars or a secret store, add employer
+> verification (the role is self-service for the demo), add vacancy retention, and
+> add a dead-letter replay handler.
 
-## Деплой
+## Deployment
 
-Контейнеры для всех сервисов готовы: `src/JobRadar.Api/Dockerfile`,
-`src/JobRadar.Worker/Dockerfile`, `frontend/Dockerfile` (multi-stage, .NET 10 / Node).
-API биндится на `$PORT` и опционально накатывает миграции на старте
-(`RunMigrationsOnStartup=true`). Пошаговый план для Railway (5 сервисов, переменные,
-Kafka, порядок) — в [DEPLOY.md](DEPLOY.md).
+Every service has a multi-stage Dockerfile: `src/JobRadar.Api/Dockerfile`,
+`src/JobRadar.Worker/Dockerfile`, `frontend/Dockerfile`. The API binds to `$PORT` and can
+apply migrations on startup (`RunMigrationsOnStartup=true`). A step-by-step Railway
+setup is in [DEPLOY.md](DEPLOY.md).
 
-## Тесты
+## Tests
 
 ```bash
-dotnet test tests/JobRadar.UnitTests          # быстрые, без Docker (27)
-dotnet test tests/JobRadar.IntegrationTests   # Testcontainers, нужен Docker (52)
+dotnet test tests/JobRadar.UnitTests          # fast, no Docker (27)
+dotnet test tests/JobRadar.IntegrationTests   # Testcontainers, needs Docker (52)
 ```
 
-Оба набора и сборка фронта (`tsc` + `vite build`) гоняются в GitHub Actions на каждый
-push и PR: [`.github/workflows/ci.yml`](.github/workflows/ci.yml).
+Both suites and the frontend build (`tsc` + `vite build`) run in
+[GitHub Actions](.github/workflows/ci.yml) on every push and pull request.
 
-- **Unit (27):** чистая доменная/прикладная логика — нормализация, парсинг зарплат,
-  dedup-ключ, релевантность, state-machine статусов, маппинг.
-- **Integration (52):** реальный Postgres через Testcontainers — идемпотентный upsert
-  под конкуренцией, query/дедуп, auth + ротация refresh, сохранённые фильтры (xmin-конфликт),
-  отклики, employer-сценарии (IDOR-скоупинг), и HTTP-level role-enforcement через
-  `WebApplicationFactory`.
+- **Unit:** normalization, salary parsing, dedup keys, relevance, status transitions,
+  mapping.
+- **Integration:** concurrent upsert, querying and dedup, auth and refresh rotation,
+  saved filters with `xmin` conflicts, applications, employer flows with IDOR checks,
+  role enforcement over HTTP via `WebApplicationFactory`.
 
 ## Frontend
 
-SPA на React + Vite + TS: публичная лента с фильтрами, авторизация (login/register),
-сохранённые фильтры, отклик + доска статусов, сторона работодателя, live через SignalR.
-Данные — TanStack Query (кэш, инвалидация по SignalR), доступ к API — типизированный
-клиент с авто-refresh токена. Стили на чистом CSS без UI-библиотек: дизайн-токены
-в `frontend/src/styles/tokens.css` (светлая и тёмная схема по настройке ОС), плотная
-таблица вакансий, которая на узких экранах складывается в карточки.
+React + Vite + TypeScript, plain CSS with no UI library. Design tokens live in
+`frontend/src/styles/tokens.css` and follow the OS light/dark setting. The vacancy table
+collapses into cards on narrow screens. Data goes through TanStack Query, and SignalR
+events invalidate the cache. The typed API client refreshes tokens automatically.
 
-### Демо-режим
+### Demo mode
 
-Чтобы статический деплой фронта не показывал пустой экран без API, есть демо-режим:
-[`frontend/src/api/demo/server.ts`](frontend/src/api/demo/server.ts) отвечает на те же
-маршруты, что и настоящий API (коды ответов, версии для `409`, state-machine откликов,
-роль `Employer`), а состояние хранит в `localStorage`. Симулированный воркер раз в
-~25 секунд «находит» вакансию и шлёт те же события, что SignalR-хаб (`VacancyChanged`,
-`MatchedVacancy`, `ApplicationStatusChanged`). Код компонентов не знает, в каком режиме
-он работает: подменяется только транспорт в `api/http.ts`.
+[`frontend/src/api/demo/server.ts`](frontend/src/api/demo/server.ts) answers the same
+routes as the real API (status codes, `409` versions, the status state machine, the
+`Employer` role) and keeps state in `localStorage`. A simulated worker "finds" a vacancy
+about every 25 seconds and emits the same events as the SignalR hub. Components don't know
+which mode they run in; only the transport in `api/http.ts` is swapped.
 
-| Сборка | Режим |
+| Build | Mode |
 |---|---|
-| `npm run dev` | реальный API на `VITE_API_URL` (по умолчанию `http://localhost:5088`) |
-| `npm run build` с `VITE_API_URL` | реальный API |
-| `npm run build` без `VITE_API_URL` | демо (так собирается Vercel-деплой) |
-| `VITE_DEMO_MODE=true` / `false` | принудительно включить / выключить |
+| `npm run dev` | real API at `VITE_API_URL` |
+| `npm run build` with `VITE_API_URL` | real API |
+| `npm run build` without `VITE_API_URL` | demo (used for the Vercel deployment) |
+| `VITE_DEMO_MODE=true` / `false` | force on / off |
diff --git a/frontend/src/api/config.ts b/frontend/src/api/config.ts
index 5a6c8c6..80026ec 100644
--- a/frontend/src/api/config.ts
+++ b/frontend/src/api/config.ts
@@ -23,4 +23,4 @@ export const VACANCIES_HUB_URL = `${API_BASE_URL}/hubs/vacancies`;
 
 export const DEMO_MODE = resolveDemoMode();
 
-export const REPOSITORY_URL = 'https://github.com/GomenAmonai/JobFinder';
+export const REPOSITORY_URL = 'https://github.com/GomenAmonai/JobRadar';
diff --git a/src/JobRadar.Worker/Program.cs b/src/JobRadar.Worker/Program.cs
index c2cddd7..7721b3c 100644
--- a/src/JobRadar.Worker/Program.cs
+++ b/src/JobRadar.Worker/Program.cs
@@ -23,7 +23,7 @@ builder.Services.AddHttpClient("remoteok", client =>
         client.BaseAddress = new Uri("https://remoteok.com/");
         client.Timeout = TimeSpan.FromSeconds(30);
         // RemoteOK отдаёт 403 на запросы без User-Agent.
-        client.DefaultRequestHeaders.UserAgent.ParseAdd("JobRadar/1.0 (+https://github.com/GomenAmonai/JobFinder)");
+        client.DefaultRequestHeaders.UserAgent.ParseAdd("JobRadar/1.0 (+https://github.com/GomenAmonai/JobRadar)");
     })
     .AddStandardResilienceHandler();
 
-- 
2.43.0
