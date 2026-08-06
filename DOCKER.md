# Running ZISK with Docker

Compose brings up two containers: SQL Server 2022 and the app itself. The app
image builds the server project, which pulls in the WebAssembly client and the
shared project, so one build produces everything.

## Prerequisites

Docker Desktop, or Docker Engine with the Compose plugin.

## Setup

```bash
cp .env.example .env
```

Open `.env` and set `MSSQL_SA_PASSWORD`. SQL Server enforces its own complexity
policy: at least 8 characters, using 3 of upper case, lower case, digits and
symbols. A password that fails the policy makes the database container exit
during startup.

`.env` is gitignored.

## Run

First time, or after changing the Dockerfile:

```bash
docker compose up --build
```

After that:

```bash
docker compose up        # foreground, logs in the terminal
docker compose up -d     # background
```

The app is at **http://localhost:8080**. Startup order is handled by a
healthcheck: the app container waits until SQL Server answers a query, then
applies the EF Core migration and seeds the database. Expect the first run to
take a minute or so.

## Seed mode

Compose defaults to `ZISK_SEED_MODE=local`, which gives you the normal login
page and the development accounts described in the root
[README](README.md#development-accounts).

Setting `ZISK_SEED_MODE=demo` in `.env` switches to the public demo build: the
one-click role picker at `/demo`, per-visitor data isolation, no outgoing email,
and a 404 on `/login`. In that mode the `Seed__Passwords__*` values from
`docker-compose.yml` are used for the seeded accounts. In `local` mode they are
ignored.

## Stop

```bash
docker compose down       # stop the containers, keep the database
docker compose down -v    # stop and delete the database volume too
```

Use `down -v` when you want the next start to seed from scratch.

## Rebuilding

Changing a `.csproj` or the Dockerfile means the cached restore layer is stale:

```bash
docker compose build --no-cache
docker compose up
```

A plain `docker compose up --build` is enough for source changes.

## Troubleshooting

**NuGet signature validation fails during build (NU3012).** Refit's author
signing certificate was revoked after the package was published. The package
itself is intact, its hash still matches, but a clean restore refuses it. The
repository ships a `NuGet.Config` that turns signature validation off, and the
Dockerfile passes it to `dotnet restore` explicitly. If you hit this error, the
usual cause is a build context that excluded `NuGet.Config`, so check that the
`COPY NuGet.Config ./` line in the Dockerfile ran. The CI workflow solves the
same problem a different way, with `NUGET_CERT_REVOCATION_MODE=offline`.

**The app container starts and immediately exits.** Almost always the database:
check `docker compose logs db` for a rejected `MSSQL_SA_PASSWORD`.

**Uploaded files disappear after a rebuild.** Attachments are written to
`wwwroot/uploads` inside the container and there is no volume mounted there, so
recreating the container loses them. The database survives, because that one
does have a volume.
