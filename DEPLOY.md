# Deploying JobRadar on Railway

JobRadar is five services: **Postgres**, **Kafka**, **API**, **Worker**, **Frontend**.
All build from this repo (Dockerfiles) except Postgres (Railway plugin) and Kafka (public image).

> Set each code service's **Root Directory = `/`** (repo root — projects reference each
> other across `src/`) and point it at the Dockerfile below.

Deploy order: **Postgres + Kafka first**, then **API** (it migrates the DB on boot), then **Worker** and **Frontend**.

---

## 1. Postgres

Add the Railway **PostgreSQL** plugin. It exposes `PGHOST/PGPORT/PGUSER/PGPASSWORD/PGDATABASE`.
Npgsql needs a key-value string (not the `DATABASE_URL` URL form) — build it from those (see API/Worker env).

## 2. Kafka

New service → **Deploy from Docker image** → `apache/kafka:3.9.0`. Variables (single-node KRaft):

```
KAFKA_NODE_ID=1
KAFKA_PROCESS_ROLES=broker,controller
KAFKA_CONTROLLER_QUORUM_VOTERS=1@localhost:9093
KAFKA_LISTENERS=PLAINTEXT://0.0.0.0:9092,CONTROLLER://0.0.0.0:9093
KAFKA_ADVERTISED_LISTENERS=PLAINTEXT://kafka.railway.internal:9092
KAFKA_LISTENER_SECURITY_PROTOCOL_MAP=CONTROLLER:PLAINTEXT,PLAINTEXT:PLAINTEXT
KAFKA_CONTROLLER_LISTENER_NAMES=CONTROLLER
KAFKA_INTER_BROKER_LISTENER_NAME=PLAINTEXT
KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR=1
KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR=1
KAFKA_TRANSACTION_STATE_LOG_MIN_ISR=1
KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS=0
```

- `kafka.railway.internal` is the service's private hostname — rename the env if your service isn't named `kafka`.
- Clients reach it over the **private network**, so no public domain is needed.
- Topics are created automatically by the Worker (`KafkaTopicInitializer`). Storage is
  reformatted on restart unless you attach a volume — fine for a demo (only missed live
  messages, no DB data loss).
- Kafka is a JVM service (~512 MB+) — the heaviest piece; mind hobby-plan limits. No
  managed Kafka on Railway, so this image service is the path (or use a hosted Kafka and
  point `Kafka__BootstrapServers` at it).

## 3. API

Dockerfile: `src/JobRadar.Api/Dockerfile`. Variables:

```
ASPNETCORE_ENVIRONMENT=Production
RunMigrationsOnStartup=true
ConnectionStrings__Postgres=Host=${{Postgres.PGHOST}};Port=${{Postgres.PGPORT}};Database=${{Postgres.PGDATABASE}};Username=${{Postgres.PGUSER}};Password=${{Postgres.PGPASSWORD}}
Kafka__BootstrapServers=kafka.railway.internal:9092
Jwt__SigningKey=<run: openssl rand -base64 48>
Cors__AllowedOrigins__0=https://<your-frontend-domain>
```

- **`Jwt__SigningKey` is mandatory** — in Production the app refuses to start on a missing,
  dev-placeholder, or <32-byte key (fail-fast by design).
- **`Cors__AllowedOrigins__0`** must be the frontend's public origin, or the browser blocks API calls.
- `$PORT` is honored automatically (the Dockerfile binds Kestrel to it).
- `RunMigrationsOnStartup=true` applies EF migrations on boot — turn it off after the first deploy if you prefer.
- Generate the frontend domain first (deploy the frontend, copy its URL) or set CORS after, then redeploy.

## 4. Worker

Dockerfile: `src/JobRadar.Worker/Dockerfile`. Variables:

```
ConnectionStrings__Postgres=Host=${{Postgres.PGHOST}};Port=${{Postgres.PGPORT}};Database=${{Postgres.PGDATABASE}};Username=${{Postgres.PGUSER}};Password=${{Postgres.PGPASSWORD}}
Kafka__BootstrapServers=kafka.railway.internal:9092
```

No HTTP port — it's a private background service. It assumes the schema exists, so deploy it
after the API has migrated (its consumers are resilient and will retry if it starts early).

## 5. Frontend

Dockerfile: `frontend/Dockerfile`. Set a service variable so it's passed as a build arg:

```
VITE_API_URL=https://<your-api-domain>
```

Vite inlines the API URL at **build time**, so changing it requires a redeploy. The image
serves the static SPA and binds to `$PORT`.

> Simpler alternative: deploy the frontend on **Vercel** — set root `frontend`, build
> `npm run build`, output `dist`, env `VITE_API_URL`. Then it's just the four backend
> services on Railway.

---

## Gotchas checklist

- [ ] `Jwt__SigningKey` set (≥32 bytes) — else the API won't boot in Production.
- [ ] `Cors__AllowedOrigins__0` = frontend origin — else CORS blocks every call.
- [ ] Postgres connection string built from `PG*` vars (not `DATABASE_URL`).
- [ ] `Kafka__BootstrapServers` points at the Kafka service's `.railway.internal` host.
- [ ] `RunMigrationsOnStartup=true` on the API for the first deploy.
- [ ] Frontend `VITE_API_URL` = API public URL (build-time).
- [ ] Deploy order: Postgres + Kafka → API → Worker + Frontend.

## Local sanity-check of the images

```bash
docker build -f src/JobRadar.Api/Dockerfile    -t jobradar-api .
docker build -f src/JobRadar.Worker/Dockerfile -t jobradar-worker .
docker build -f frontend/Dockerfile --build-arg VITE_API_URL=http://localhost:5088 -t jobradar-web .
```
