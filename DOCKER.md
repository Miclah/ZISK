# Running ZISK with Docker

## Prerequisites

- Docker Desktop (or Docker Engine + Compose plugin)

## Setup

```bash
cp .env.example .env
# edit .env and set a real MSSQL_SA_PASSWORD
```

## Run

```bash
docker compose up --build
```

The app is available at **http://localhost:8080** once the `db` service passes
its healthcheck and `app` starts. First startup runs EF Core migrations and
seeds the database (see the root [README.md](README.md#demo-accounts) for seed
modes and default credentials).

## Stop

```bash
docker compose down          # stop containers, keep the DB volume
docker compose down -v       # stop containers and delete the DB volume
```

## Notes

- `ConnectionStrings__DefaultConnection` is injected via environment variable
  in `docker-compose.yml` — it points at the `db` service, not LocalDB.
- `ZISK_SEED_MODE` defaults to `local` in compose; set it to `demo` in your
  `.env` (plus `Seed__Passwords__*` env vars) to test the demo seed path
  before deploying.
