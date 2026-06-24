# JobRadar — API-бриф для Claude Design

Контракт бэкенда JobRadar для проектирования UI. Дизайн-агент строит интерфейс
против этих эндпоинтов; бэкенд не меняется. Всё JSON, поля — `camelCase`.

- **Base URL (dev):** `http://localhost:5088` (API). Фронт-dev на `http://localhost:5173`.
- **OpenAPI/Scalar (только Development):** `GET /scalar/v1` — живой справочник.
- **CORS:** разрешён origin SPA (по умолчанию `http://localhost:5173`).

## Аутентификация

JWT Bearer. Токен кладётся в заголовок `Authorization: Bearer <accessToken>`.
Для SignalR токен передаётся query-параметром `access_token` (см. ниже).

- `POST /auth/register` — `{ email, password, displayName?, role? }` → `TokenPair`
  - `password`: 8–128 символов. `role`: `"Candidate"` (по умолчанию) или `"Employer"`.
  - 409 если email занят, 400 при невалидном вводе.
- `POST /auth/login` — `{ email, password }` → `TokenPair` (401 при неверных кредах).
- `POST /auth/refresh` — `{ refreshToken }` → `TokenPair` (ротация; повторное использование
  старого токена инвалидирует всю цепочку → 401).
- `GET /auth/me` (auth) → `{ id, email, role }`.
- Rate limit на `/auth/*`: 10 запросов/мин/IP → 429.

`TokenPair`: `{ accessToken: string, refreshToken: string, accessTokenExpiresAt: string (ISO) }`

## Публичная лента вакансий

- `GET /vacancies?market=&level=&stack=&q=&page=1&pageSize=20` → `PagedResult<VacancyDto>`
  - Анонимно. Rate limit 120/мин/IP. `pageSize` ≤ 100. `q` — поиск по title/company/skills.
  - Дубли из разных источников свёрнуты (показывается одна каноническая вакансия).

`PagedResult<T>`: `{ items: T[], page, pageSize, total, totalPages }`

`VacancyDto`: `{ id, source, externalId, title, company?, market?, level?, stack?, location?,
salaryRaw?, salaryMin?, salaryMax?, salaryCurrency?, skills?, url?, publishedAt?, firstSeen, lastSeen }`

### ⚠️ Точные значения фильтров (должны совпадать дословно, иначе пусто)

- **market** (кириллица): `Япония` · `Россия` · `СНГ` · `США` · `Канада` · `Европа` ·
  `Азия` · `Worldwide` · `Другое` · `—` (нет данных)
- **level:** `junior` · `middle` · `senior+` · `mid/unknown`
- **stack:** `C#/.NET` · `backend`

UI фильтров должен предлагать ровно эти токены (не «dotnet»/«senior»/«global»).

## Сохранённые фильтры (auth, скоуп по пользователю)

- `GET /me/filters` → `SavedFilterDto[]`
- `POST /me/filters` — `{ name, market?, level?, stack?, q? }` → `SavedFilterDto` (201)
- `PUT /me/filters/{id}` — `{ name, market?, level?, stack?, q?, version }` → `SavedFilterDto`
  - `version` — из прочитанного DTO (optimistic concurrency); устаревшая → **409**.
- `DELETE /me/filters/{id}` → 204 / 404

`SavedFilterDto`: `{ id, name, market?, level?, stack?, q?, version, createdAt, updatedAt }`

## Отклики кандидата (auth, скоуп по пользователю)

- `POST /vacancies/{id}/applications` — `{ coverLetter? }` → `ApplicationDto` (201)
  - Повторный отклик на ту же вакансию → **409** (`already_applied`).
- `GET /me/applications` → `ApplicationDto[]` — мой пайплайн.
- `GET /me/applications/{id}` → `ApplicationDto` / 404
- `PATCH /me/applications/{id}/status` — `{ status, version }` → `ApplicationDto`
  - Нелегальный переход → **422** (`illegal_transition`); устаревшая версия → **409**.
- `DELETE /me/applications/{id}` → 204 (убрать из пайплайна)

`ApplicationDto`: `{ id, status, coverLetter?, version, createdAt, updatedAt,
vacancy: { id, title, company?, url?, market?, level? } }`

## Сторона работодателя (auth + роль `Employer`)

Доступ только с ролью Employer; иначе **403** (анонимно — 401).

- `POST /employer/vacancies` — `{ title, company?, location?, salaryRaw?, skills?, url? }`
  → `VacancyDto` (201). `url` — только http(s). Вакансия попадает в общую ленту.
- `GET /employer/applications` → `EmployerApplicationDto[]` — отклики на СВОИ вакансии.
- `PATCH /employer/applications/{id}/status` — `{ status, version }` → `ApplicationDto`
  - Чужая вакансия → 404; `Withdrawn` работодателю запрещён → 422; устаревшая версия → 409.
  - При успехе кандидату прилетает SignalR-событие `ApplicationStatusChanged`.

`EmployerApplicationDto`: `{ id, status, coverLetter?, version, createdAt, updatedAt,
candidateEmail, candidateDisplayName?, vacancy: { id, title, company?, url?, market?, level? } }`

## Статусы отклика (state-machine, строки)

`ApplicationStatus`: `Submitted` · `UnderReview` · `InterviewScheduled` · `OfferExtended`
· `Rejected` · `Withdrawn`

Переходы валидируются на сервере:
- Среди активных стадий — только **вперёд** (Submitted→…→OfferExtended).
- В терминальное (`Rejected`/`Withdrawn`) — из любой активной; из терминального — никуда.
- **Кандидат:** на агрегированной вакансии ведёт все стадии сам (это его трекер); на
  нативной (запощенной работодателем) может только `Withdrawn`.
- **Работодатель:** двигает стадии/`Rejected` на своих вакансиях; `Withdrawn` — нельзя.

UI подсказка: показывать только разрешённые переходы для текущего статуса/роли/типа вакансии.

## Реальное время (SignalR)

Хаб: `GET /hubs/vacancies`. Подключение с токеном в query: `/hubs/vacancies?access_token=<jwt>`
(анонимно тоже можно — тогда только публичные события). События (сервер → клиент):

- `VacancyChanged` (всем) — `VacancyChangedEvent` при новой/изменённой вакансии (live-лента).
- `MatchedVacancy` (только подходящим юзерам) — новая вакансия под сохранённым фильтром
  пользователя (шлётся только на ВСТАВКУ).
- `ApplicationStatusChanged` (конкретному кандидату) — работодатель сменил статус его отклика;
  payload = `ApplicationDto`.

`VacancyChangedEvent`: `{ source, externalId, title, company?, market?, level?, stack?, url?,
publishedAt?, outcome: "Inserted" | "Updated" }`

## Поверхности UI (две роли)

1. **Кандидат:** публичная лента + фильтры (с точными токенами), логин/регистрация,
   сохранённые фильтры, live-уведомления о матчах, отклик на вакансию, трекер откликов
   (доска статусов), live-обновление статуса от работодателя.
2. **Работодатель:** постинг нативной вакансии, входящие отклики (с контактами кандидата),
   смена статуса отклика.

## Заметки

- Все ошибки валидации — `400` с телом ProblemDetails (`errors` по полям).
- Optimistic concurrency (`version`) — на фильтрах и статусах откликов: клиент
  возвращает прочитанный `version`, при конфликте — 409, нужно перечитать и повторить.
- Зарплата: `salaryRaw` (как пришло из источника) + распарсенные `salaryMin/Max/Currency`
  (могут быть null, если распарсить не удалось).
