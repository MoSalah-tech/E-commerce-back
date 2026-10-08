# E-Commerce API

A REST API for an e-commerce store built with **.NET 9** and **Clean Architecture**, covering authentication, catalogue, cart, orders and reviews.

## Tech stack

| Layer | Technology |
| --- | --- |
| Web API | ASP.NET Core 9, Swagger/OpenAPI |
| Application | MediatR (CQRS handlers), FluentValidation, Result-based errors |
| Domain | Entities, value objects (`Money`, `OrderNumber`, `Slug`), domain exceptions |
| Infrastructure | EF Core 9, ASP.NET Core Identity, JWT bearer auth, SQL Server 2022 |
| Tests | xUnit — 256 unit tests + 37 integration tests (real pipeline on SQLite) |
| DevOps | Docker Compose, multi-stage `Dockerfile` |

## Project structure

```
src/
  ECommerce.Domain          Entities, value objects, domain rules (no dependencies)
  ECommerce.Application     Use cases: commands/queries, DTOs, validators, behaviors
  ECommerce.Infrastructure  EF Core, Identity, JWT, repositories, seeding
  ECommerce.Api             Controllers, middleware, Swagger, composition root
tests/
  ECommerce.UnitTests       Domain + application behavior
  ECommerce.IntegrationTests Booted API end-to-end (auth, admin, shopping flow)
docker/                     SQL Server bootstrap script (init-db.sql)
```

Dependencies point inwards: `Api → Application → Domain`, `Infrastructure → Application → Domain`. The domain and application layers have no knowledge of ASP.NET Core or EF Core.

## Features

- **Auth** — register, login, change password; JWT access tokens; customer/admin roles
- **Products & categories** — CRUD, search/filter/pagination, featured products, stock and discount management, slug generation
- **Cart** — per-customer cart with add/update/remove items
- **Orders** — place order (stock validation, totals via `Money`), order history, status transitions, cancellation
- **Reviews** — customer reviews with moderation
- **Cross-cutting** — MediatR pipeline with validation, logging and performance behaviors; uniform `{ success, data, message }` response envelope; global exception middleware

### API endpoints

| Area | Routes |
| --- | --- |
| Auth | `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/change-password` |
| Products | `GET/POST /api/products`, `GET/PUT/DELETE /api/products/{id}`, `GET /api/products/featured`, `POST /api/products/{id}/stock`, `POST /api/products/{id}/discount` |
| Categories | `GET/POST /api/categories`, `GET/PUT/DELETE /api/categories/{id}` |
| Cart | `GET /api/cart`, `POST /api/cart/items`, `PUT /api/cart/items/{productId}`, `DELETE /api/cart/items/{productId}`, `DELETE /api/cart` |
| Orders | `GET/POST /api/orders`, `GET /api/orders/{id}`, `POST /api/orders/{id}/status`, `POST /api/orders/{id}/cancel` |
| Reviews | `GET/POST /api/reviews`, `PUT/DELETE /api/reviews/{id}`, `POST /api/reviews/{id}/moderation` |

## Getting started

### Option 1 — Docker Compose (recommended)

Prerequisites: Docker with Compose.

```bash
cp .env.example .env
# Edit .env: set MSSQL_SA_PASSWORD, MSSQL_PASSWORD and Jwt__SigningKey
# (passwords need upper+lower case, a digit and a symbol, min 8 chars)
docker compose up --build
```

The API starts at **http://localhost:8080** (Swagger at `/swagger`). Compose boots SQL Server, runs `docker/init-db.sql` to create the database and least-privilege login, then starts the API, which applies EF migrations and seeds demo data on startup.

### Option 2 — Local development

Prerequisites: .NET 9 SDK and a SQL Server instance (local, Docker or SSMS).

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Server=localhost,1433;Database=ECommerceDb;User Id=sa;Password=YourPassword;TrustServerCertificate=True" \
  --project src/ECommerce.Api

dotnet user-secrets set "Jwt:SigningKey" "<at-least-32-random-characters>" --project src/ECommerce.Api

dotnet run --project src/ECommerce.Api
```

`appsettings*.json` intentionally contains no connection string or signing key — configuration comes from user-secrets (local) or environment variables (Docker).

### Seeded accounts

With `Database:ApplyMigrationsOnStartup=true` (Development), migrations and seed data are applied automatically:

| Role | Email | Password |
| --- | --- | --- |
| Admin | `admin@ecommerce.com` | `Admin!2345` |

> Change these before deploying anywhere real.

## Tests

```bash
dotnet test
```

Integration tests boot the actual API pipeline (`WebApplicationFactory`) with SQLite standing in for SQL Server, so routing, JWT auth, validation and EF mappings are exercised for real.

## Configuration

All values can be supplied via environment variables (`Section__Key` format) which take precedence over appsettings:

| Key | Purpose |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | SQL Server connection string |
| `Jwt__Issuer` / `Jwt__Audience` | Token validation parameters |
| `Jwt__SigningKey` | HMAC-SHA256 signing key (min 32 chars) |
| `Jwt__ExpiryMinutes` | Access token lifetime |
| `Database__ApplyMigrationsOnStartup` | Run migrations + seed on boot |
| `MSSQL_SA_PASSWORD`, `MSSQL_PASSWORD`, `MSSQL_USER`, `MSSQL_DATABASE` | Docker SQL Server bootstrap (`.env`) |
