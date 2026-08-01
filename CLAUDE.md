# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

An ASP.NET Core minimal API (.NET 10) backed by PostgreSQL via EF Core (Npgsql provider), deployed to Render as a Docker web service with a free Render Postgres database. There is no test project.

## Commands

```powershell
dotnet build                  # Build
dotnet run                    # Run (Development profile listens on http://localhost:5056)

# EF Core migrations (dotnet-ef tool required)
dotnet ef migrations add <Name>
dotnet ef database update     # optional — the app auto-migrates on startup

# Local Postgres for development (app default connects to localhost:5432)
docker run -d --name apidb -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=apidb -p 5432:5432 postgres:16-alpine
```

## Architecture

- `Program.cs` holds everything besides entities and the DbContext: connection-string resolution, startup migration, all endpoint definitions, and the request DTOs (`UserDto`, `ProductDto`, `OrderDto`).
- `Models/` — `User`, `Product`, `Order` entities. `Order` has FKs to both `User` (cascade delete) and `Product` (restrict delete).
- `Data/AppDbContext.cs` — DbContext with a unique index on `User.Email` and `HasData` seed rows (3 users, 3 products, 3 orders). Seed values must stay static (fixed dates/ids) or EF generates a new migration diff.
- `Migrations/` — EF Core migrations. The app calls `Database.Migrate()` on startup, so schema + seed data are applied automatically on first run (this is how the Render database gets initialized — never hand-create the schema).

### Connection string resolution (`BuildConnectionString` in Program.cs)

1. If the `DATABASE_URL` env var is set (Render provides it as a `postgres://user:pass@host:port/db` URL), it is parsed into an Npgsql connection string with `SslMode=Require`.
2. Otherwise falls back to `ConnectionStrings:DefaultConnection` in appsettings (localhost dev Postgres).

Env-var override for local runs: `$env:ConnectionStrings__DefaultConnection = '...'`.

## Deployment (Render)

- `render.yaml` is a Render Blueprint defining the free-plan Docker web service and the free Postgres database, wiring `DATABASE_URL` from the database into the service.
- The Dockerfile is a standard multi-stage build; Render routes traffic to port 8080 (`ASPNETCORE_HTTP_PORTS=8080` is the aspnet image default).
- `UseHttpsRedirection()` is intentionally absent: Render terminates TLS at its proxy and forwards plain HTTP to the container; redirecting would loop.

## Notes

- OpenAPI document is served at `/openapi/v1.json` (mapped in all environments); `/` is a health/status endpoint used as Render's health check.
- `API.http` contains runnable sample requests for every endpoint against the local dev server.
