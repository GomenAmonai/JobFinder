# JobRadar — Frontend

A single-page React app that shows a filterable, paginated table of remote .NET / backend
vacancies, with live updates pushed over SignalR when new or changed vacancies arrive.

## Stack

- Vite 7 + React 18 + TypeScript (strict)
- [`@tanstack/react-query`](https://tanstack.com/query) for data fetching, caching and refetch-on-event
- [`@microsoft/signalr`](https://www.npmjs.com/package/@microsoft/signalr) for the live `VacancyChanged` stream
- Plain CSS with design tokens (no UI framework) — dark slate dashboard theme

## Configuration

The app reads the backend base URL from `VITE_API_URL` and falls back to
`http://localhost:5088` when it is not set.

Copy the template and adjust if needed:

```bash
cp env.example .env
```

`.env`:

```
VITE_API_URL=http://localhost:5088
```

From this base the app derives:

- REST: `GET {VITE_API_URL}/vacancies?market&level&stack&q&page&pageSize`
- SignalR hub: `{VITE_API_URL}/hubs/vacancies` (client method `VacancyChanged`)

> The backend must allow CORS for the dev origin (`http://localhost:5173`) for both the REST
> endpoint and the SignalR hub.

## Develop

```bash
npm install
npm run dev        # http://localhost:5173
```

## Build & type-check

```bash
npm run build      # tsc -b (project references) + vite build  ->  dist/
npm run preview    # serve the production build locally
npm run typecheck  # types only, no emit
```

## Project structure

```
frontend/
├─ index.html              # entry, loads Space Grotesk + JetBrains Mono
├─ vite.config.ts          # React plugin, dev server on :5173
├─ env.example             # copy to .env
└─ src/
   ├─ main.tsx             # QueryClient + ToastProvider + render
   ├─ App.tsx              # page composition, query/filter/page state
   ├─ api/
   │  ├─ config.ts         # resolves VITE_API_URL -> base + hub URL
   │  ├─ vacancies.ts      # fetchVacancies + typed VacanciesApiError
   │  └─ format.ts         # date / skills formatting helpers
   ├─ hooks/
   │  ├─ use-vacancies.ts        # TanStack Query for the vacancies list
   │  ├─ use-vacancy-stream.ts   # SignalR connection + invalidate + toast
   │  ├─ use-toasts.tsx          # toast context/provider/hook
   │  └─ use-debounced-value.ts  # generic debounce (search box)
   ├─ components/
   │  ├─ Header.tsx, ConnectionIndicator.tsx
   │  ├─ FilterBar.tsx           # market/level/stack chips + debounced search
   │  ├─ VacancyTable.tsx, VacancyRow.tsx, Badges.tsx
   │  ├─ Pagination.tsx, StatePanels.tsx (loading/empty/error)
   │  ├─ ToastRegion.tsx, icons.tsx
   ├─ types/vacancy.ts      # VacancyDto, PagedResult, VacancyChangedPayload
   └─ styles/               # tokens.css, global.css, app.css
```

## Live updates

`useVacancyStream` opens a SignalR connection (`withAutomaticReconnect`) and listens for
`VacancyChanged`. On each event it invalidates the vacancies query (TanStack Query refetches the
current page/filters) and raises a toast: a prominent "New vacancy" toast for `Inserted`, a subtler
"Updated" toast for `Updated`. The header shows a green pulsing dot while the connection is live.
