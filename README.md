**English** | [Slovenčina](README.sk.md)

# ZISK

Attendance tracking, training scheduling and communication between parents and coaches for a sports club, built as a .NET 10 server that hosts both a REST API and a Blazor WebAssembly client.

[![CI](https://github.com/Miclah/ZISK/actions/workflows/ci.yml/badge.svg)](https://github.com/Miclah/ZISK/actions/workflows/ci.yml)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green)](LICENSE)

## Contents

- [Live demo](#live-demo)
- [About this project](#about-this-project)
- [Features by role](#features-by-role)
- [Screenshots](#screenshots)
- [Tech stack](#tech-stack)
- [Architecture](#architecture)
- [Demo mode](#demo-mode)
- [Running locally](#running-locally)
- [Configuration and secrets](#configuration-and-secrets)
- [Deployment](#deployment)
- [Uninstalling](#uninstalling)
- [License](#license)
- [Author](#author)

## Live demo

**[miclah-zisk.azurewebsites.net/demo](https://miclah-zisk.azurewebsites.net/demo)**

Pick a role and you are signed in. There is no password to enter and no account to create. Each visitor gets a private copy of the data, so anything you change belongs to you alone.

The site runs on the Azure free tier, which stops the app and pauses the database when nobody is using them. First load after an idle period takes roughly 30 seconds while both wake up. After that it responds normally.


![](https://github.com/Miclah/ZISK/blob/main/docs/screenshots/walkthrough.webp)

## About this project

ZISK is an acronym of *Žiarsky Informačný Systém pre športové Kluby*, the Žiar information system for sports clubs.

It was built for a municipal sports club in Žiar nad Hronom and submitted as a bachelor's thesis at the Faculty of Management Science and Informatics, University of Žilina. Deployment at the club is planned for August 2026.

The interface is available in Slovak and English.

## Features by role

**Admin**

- Create, edit, deactivate and delete user accounts across all five roles
- Manage teams and their rosters, and assign coaches to teams
- Create seasons and switch the active one, with one-off and recurring trainings
- Read club-wide attendance statistics and publish system announcements

**Coach**

- See the roster and absence requests for the teams they are assigned to
- Open a training and review who is present, absent or excused
- Mark an individual player as an unexcused absence, which is the only manual attendance edit in the application
- Publish announcements to their team

**Parent**

- Follow their children's training schedule and attendance history
- Submit and edit absence requests on behalf of a child, either for one training or for a date range
- Invite the second parent to the same child, by email link or by a one-time code

**Athlete**

- The same schedule, attendance and announcement views as a parent, scoped to themselves
- Submit their own absence requests

**Child**

- Schedule, attendance and announcements, read only
- A parent submits absence requests for them

A background worker promotes a Child to Athlete once they pass an age threshold, and from that point the account can file its own absence requests.

### How attendance works

Attendance closes itself. Ten minutes after a training starts, every team member who joined the team before that training is marked present. If an absence request covers them, either by naming that training directly or by overlapping its date, they are marked excused instead. A coach only intervenes to record someone who did not turn up and did not send an excuse.

## Screenshots

### Coach - recording attendance
![Recording attendance](docs/screenshots/coach-attendance.png)

### Training calendar
![Training calendar](docs/screenshots/training-calendar.png)

### Parent - submitting an absence request
![Absence request](docs/screenshots/parent-absence.png)

## Tech stack

| Layer | Technology | Version |
|---|---|---|
| Framework | .NET | 10.0 |
| Client | Blazor WebAssembly | 10.0.5 |
| Server | ASP.NET Core, Static SSR for Identity pages | 10.0.5 |
| Component library | MudBlazor | 9.4.0 |
| Calendar | Heron.MudCalendar | 4.0.0 |
| ORM | Entity Framework Core (SQL Server) | 10.0.5 |
| Database | SQL Server: LocalDB or Docker locally, Azure SQL serverless for the demo | 2022 |
| Typed HTTP client | Refit | 10.1.6 |
| Auth | ASP.NET Core Identity, cookie based, 14 day sliding expiry | 10.0.5 |
| Email | MailKit | 4.16.0 |
| Tests | xUnit with EF Core InMemory | 2.9.3 |

## Architecture

![Architecture](docs/architecture.svg)

One ASP.NET Core project serves the compiled WebAssembly client and the REST API from the same origin, so there is no CORS configuration and both share one authentication cookie. The client talks to the API through 14 Refit interfaces: one method per endpoint, with the implementation generated at compile time.

Identity pages such as login and registration have to set an `HttpOnly` cookie, so they render as Static SSR on the server rather than in WebAssembly. They call the same Refit interfaces, with a `ForwardAuthHeaderHandler` that passes the cookie along on server to server calls.

`ZISK.Shared` holds the DTO record types, the enums and the Slovak and English translation table. Both the client and the server reference it, which turns a contract change into a build error on both sides instead of a runtime surprise.

Controllers stay thin and hand off to a service layer that owns the business rules and the LINQ. `TeamAccessService` sits in that layer and resolves which team IDs a given user may read: unrestricted for an admin, the coach's assigned teams for a coach, and the teams their children belong to for a parent. Five background workers run alongside the request path, closing attendance, extending recurring training series from a weekday bitmask, promoting children by age, and maintaining the demo data.

## Demo mode

The public demo is the same application with `ZISK_SEED_MODE=demo`, and it isolates visitors from one another rather than sharing one dataset.

When someone picks a role on the landing page, the server clones the entire seeded template into a new demo session: teams, users with their roles, memberships, trainings, attendance, absence requests and announcements. The session ID goes into an `HttpOnly` cookie and into a claim on the auth cookie. Every entity that belongs to a session implements `IDemoScoped`, and `ApplicationDbContext` applies a global EF Core query filter on `DemoSessionId` to 14 of them. Isolation therefore lives in the model rather than in each query, so no service or controller had to be changed to support it, and no new endpoint can forget it.

Cloned users are prefixed (`a1b2c3d4.admin@zisk.sk`) because ASP.NET Identity keeps its own unique index on the normalised user name, which is not session scoped. Uniqueness constraints that are ours, such as phone number, birth number and team name, are scoped by `DemoSessionId` instead, so two visitors' clones never collide.

Several things are switched off for a demo visitor. A logging sender replaces real email delivery. Password change, email change and account deletion return an error to anyone inside a demo session, and file uploads are refused. A guard middleware answers 404 on `/login`, `/registracia` and the whole Identity scaffold, so the one visible way in is the landing page. Two background workers keep the database from growing without limit: one deletes sessions that have been idle for 24 hours, the other regenerates the shared template daily so the calendar never fills up with past trainings. A configurable cap evicts the least recently used session when too many are live at once.

The reset button drops the current session's data, clones a fresh one and signs the visitor out, since their account was part of what was just deleted.

## Running locally

### With Docker

```bash
git clone https://github.com/Miclah/ZISK.git
cd ZISK
cp .env.example .env
```

Edit `.env` and set `MSSQL_SA_PASSWORD` to a password that satisfies SQL Server's complexity policy (at least 8 characters, and 3 of upper case, lower case, digit and symbol). Then:

```bash
docker compose up --build
```

The app is at **http://localhost:8080**. The first run starts SQL Server, waits for its healthcheck, applies the EF Core migration and seeds the database. Later runs need only `docker compose up`, or `docker compose up -d` to detach.

Compose defaults to `ZISK_SEED_MODE=local`, which gives you the ordinary login page and the seeded development accounts. Set `ZISK_SEED_MODE=demo` in `.env` to run the public demo build instead.

See [DOCKER.md](DOCKER.md) for rebuilding after dependency changes, removing the database volume, and the NuGet signature validation error you may hit on a clean build.

### Without Docker

Requires the [.NET SDK 10.0](https://dotnet.microsoft.com/download) and SQL Server LocalDB, which ships with Visual Studio on Windows.

```bash
dotnet restore ZISK/ZISK.sln
dotnet run --project ZISK/ZISK/ZISK.csproj
```

The app is at **http://localhost:5224**. Migrations are applied and the database is seeded on startup, so no separate `dotnet ef database update` is needed. To create a migration after changing the model:

```bash
dotnet ef migrations add <Name> --project ZISK/ZISK/ZISK.csproj
```

### Development accounts

In `local` mode the seeded accounts are `admin@zisk.sk`, `trener@zisk.sk`, `rodic@zisk.sk` and `dieta@zisk.sk`. Their passwords are hardcoded in `DatabaseInitializer.SeedPasswordSet.LocalDefault`. Roughly 25 further sample coaches, parents, athletes and children are seeded so that rosters, attendance history and statistics have something to show, and they reuse the same password per role.

### Tests

```bash
dotnet test
```

20 test classes, 135 tests. They cover automated attendance close-out, bulk attendance, password and email change flows, forgotten password rate limiting, the Child to Athlete upgrade, parent invitations, training cancellation, recurring series generation (including parity between the manual endpoint and the background worker), username generation, seed mode configuration, demo session cloning and cleanup, delete path integrity, and API error mapping.

## Configuration and secrets

Nothing sensitive is committed. `appsettings.json` carries only the LocalDB connection string and non-secret SMTP defaults. Locally, use user secrets:

```bash
dotnet user-secrets init --project ZISK/ZISK
dotnet user-secrets set "Smtp:Password" "..." --project ZISK/ZISK
```

In Docker, values come from `.env` and `docker-compose.yml`. On Azure App Service they are application settings, using the double underscore form (`Seed__Passwords__Admin`).

| Setting | Purpose |
|---|---|
| `ConnectionStrings__DefaultConnection` | SQL Server connection string |
| `ZISK_SEED_MODE` | `local`, `demo` or `production`. Defaults to `local` |
| `Seed__Passwords__Admin` / `Coach` / `Parent` / `Child` | Seed passwords. Read in `demo` mode only. Startup fails if any is missing or violates the password policy |
| `Seed__InitialAdmin__Email` / `Password` / `FirstName` / `LastName` | The single admin account created in `production` mode, which seeds no sample data |
| `Smtp__Host` / `Port` / `UseSsl` / `SenderName` / `SenderEmail` / `Username` / `Password` | Outgoing mail. Ignored in `demo` mode, where a logging sender replaces it |
| `Demo__OwnerKey` | Secret that lets the maintainer reach the normal login form on a demo deployment. Required when `ZISK_SEED_MODE=demo` |
| `Demo__MaxActiveSessions` | Cap on concurrent demo sessions before the least recently used one is evicted. Defaults to 200 |
| `MSSQL_SA_PASSWORD` | Docker Compose only, the `sa` password for the database container |

A seed password that violates the Identity policy (minimum 8 characters, with a digit, a lower case letter and an upper case letter) throws at startup rather than skipping the affected accounts.

## Deployment

The demo runs on Azure App Service on the free F1 plan, against an Azure SQL serverless database that auto-pauses. Infrastructure is defined in [azure/main.bicep](azure/main.bicep): the App Service plan, the web app, the SQL server and database, the firewall rule, and the application settings including seed passwords.

[.github/workflows/ci.yml](.github/workflows/ci.yml) restores, builds and tests on every push and pull request against `main`. On a push to `main` it also publishes the app and uploads it as an artefact, then a second job signs in to Azure through OIDC federated credentials, with no client secret stored in the repository, and deploys that artefact to App Service.

Because the database auto-pauses, `UseSqlServer` is configured with `EnableRetryOnFailure` and a 120 second command timeout, and startup retries the migration ten times with backoff before failing loudly rather than serving an app with no schema.

## Uninstalling

Nothing is installed outside the repository folder, the database and Docker's own storage, so removing the project means clearing those three.

### With Docker

```bash
docker compose down -v --rmi all
```

`-v` deletes the `zisk-db-data` volume, which is where SQL Server keeps the database, so this discards all data with it. `--rmi all` removes both the image built for the app and the `mcr.microsoft.com/mssql/server:2022-latest` base image pulled for the database, the latter being a couple of gigabytes.

Leave off `--rmi all` to keep the images if you expect to come back, or leave off `-v` to keep the data.

Then delete the `.env` file you created and the cloned repository.

### Without Docker

The database is called `ZISK` and lives on the `(localdb)\mssqllocaldb` instance:

```bash
dotnet ef database drop --project ZISK/ZISK/ZISK.csproj
```

Do not delete the LocalDB instance itself with `sqllocaldb delete`. It is shared with every other LocalDB project on the machine.

Files uploaded through the announcements and documents screens are stored in `ZISK/ZISK/wwwroot/uploads/` and are not removed along with the database. The folder only exists if something was actually uploaded.

If you set any user secrets, clear them:

```bash
dotnet user-secrets clear --project ZISK/ZISK/ZISK.csproj
```

Then delete the cloned repository, which takes `bin/` and `obj/` with it.

NuGet packages are not kept in the project. They live in the shared cache under `~/.nuget/packages` and are used by every .NET project on the machine, so leave them be unless you specifically want to empty it with `dotnet nuget locals all --clear`.

## License

Apache License 2.0. See [LICENSE](LICENSE).

## Author

Michal Petrán
[GitHub](https://github.com/Miclah) · [LinkedIn](https://www.linkedin.com/in/mpetran)
