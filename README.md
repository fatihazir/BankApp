# BankApp – Loan Module

Loan (credit) module of a banking application, built with **.NET 10** and **C#** following **Clean Architecture** and **CQRS**.

## Solution Structure

```
BankApp/
├── BankApp.slnx
├── .devcontainer/
│   └── devcontainer.json        # VS Code Dev Container (.NET 10 SDK)
├── docker-compose.yml           # Local SQL Server + API (Swagger)
├── scripts/
│   ├── dotnet.sh                # Runs the .NET CLI inside Docker (no local SDK needed)
│   ├── ef.sh                    # Runs dotnet-ef (migrations) inside Docker
│   └── test.sh                  # Runs all tests (integration tests need the Docker socket)
└── src/
    ├── BankingSystemCore/       # Shared kernel, reusable across any project
    │   ├── Repositories/        # Entity base, generic repository, EF implementation, paging
    │   └── CrossCuttingConcerns/Exceptions/
    │       ├── Types/           # BusinessException, ValidationException, NotFoundException, AuthorizationException
    │       ├── HttpProblemDetails/  # RFC 7807 responses per exception type
    │       ├── Handlers/        # ExceptionHandler (abstract), HttpExceptionHandler
    │       └── Middlewares/     # ExceptionMiddleware, UseCustomExceptionMiddleware()
    ├── Domain/
    │   └── Entities/            # Database entities of the loan module
    ├── Application/             # Use cases: commands, queries, handlers, validators, DTOs
    ├── Persistence/             # EF Core DbContext, configurations, repositories
    └── WebApi/                  # ASP.NET Core Web API (controllers), composition root
tests/
├── BankingSystemCore.UnitTests/ # Core: exception middleware, JWT, hashing, tokens, mailing, authorization pipeline
├── Application.UnitTests/       # Fast tests, no database (rules, validators, mappers, handlers)
├── Persistence.IntegrationTests/# Handlers + EF Core against a real SQL Server (Testcontainers)
└── WebApi.IntegrationTests/     # HTTP level: controllers, error format, Swagger
```

## Layers and Dependencies

```
WebApi ──► Application ──► Domain ──► BankingSystemCore
  │            │                             ▲
  └──► Persistence          Application ─────┘
```

| Project             | References                   | Responsibility |
|---------------------|------------------------------|----------------|
| `BankingSystemCore` | –                            | Generic building blocks (base entity, repository abstractions, paging, exceptions, pipeline behaviors). Must not contain BankApp-specific logic so it can be reused in other solutions. |
| `Domain`            | `BankingSystemCore`          | Database entities of the loan module (`Entities` folder only). |
| `Application`       | `Domain`, `BankingSystemCore`| CQRS commands/queries and their handlers, validation, mapping, repository interfaces. |
| `Persistence`       | `Application`                | Data access implementation (DbContext, migrations, repositories). |
| `WebApi`            | `Application`, `Persistence` | HTTP endpoints and dependency injection setup. |

Rule: dependencies always point inward. `BankingSystemCore` never references any other project.

## Entities

Every database entity derives from `BankingSystemCore.Repositories.Entity<T>`, where `T` is the primary key type (`int`, `Guid`, `string`, ...):

| Property     | Type        | Notes |
|--------------|-------------|-------|
| `Id`         | `T`         | Generic primary key, initialized to `default!` |
| `CreateDate` | `DateTime`  | |
| `UpdateDate` | `DateTime?` | `null` until the first update |
| `DeleteDate` | `DateTime?` | `null` unless soft-deleted |

```csharp
public class Customer : Entity<Guid> { }
```

## Run the API

```bash
docker compose up -d      # SQL Server + API; first start compiles the code and takes a minute
```

Open **http://localhost:5043/swagger** (the root URL redirects there). Request bodies of *POST* and *PUT* are pre-filled with sample data, so use **Try it out → Execute**.

```bash
docker compose logs -f api   # follow logs
docker compose down          # stop (add -v to delete the database)
```

- The database schema is created automatically on startup in the Development environment (EF Core migrations).
- Local-only database credentials are in `docker-compose.yml` (override with the `SA_PASSWORD` environment variable). SQL Server is published on `127.0.0.1:1433` only.
- After changing the API code, run `docker compose restart api`.
- Migrations: `./scripts/ef.sh migrations add <Name> --output-dir Migrations`.

## Loans

Customers apply for loans; individual customers can only use individual loan types, corporate customers only corporate ones.

```
LoanType (Segment)  1──N  LoanSubType (limits, rate)  1──N  LoanApplication  N──1  Customers (individual or corporate)
```

| Endpoint | Purpose |
|---|---|
| `GET/POST /api/loan-types`, `POST /api/loan-types/{id}/sub-types` | List (paged, `?segment=`) and create loan types and sub types. Six sub types are seeded by the migration. |
| `POST /api/loan-applications` | Apply for a loan. Response contains the calculated monthly payment. |
| `GET /api/loan-applications` | Paged list, filters `customerId`, `status`; includes customer and loan type. |
| `GET /api/loan-applications/calculate` | Monthly payment, total repayment and installment schedule, without applying. |
| `POST /api/loan-applications/{id}/start-review`, `approve`, `reject`, `cancel` | Decision flow: Submitted → UnderReview → Approved / Rejected, or Cancelled while open. |

Calculation: equal installments, monthly interest = annual rate / 12 on the remaining balance. Taxes and fees are not included.

## Users, security and sessions

**A customer is a user.** The user types form one inheritance chain, stored table-per-type:

```
Users (Core: login information only)            Email, PasswordHash, lockout, e-mail verification
  └─ ApplicationUsers (this application)        Role, ban
       └─ Customers                             CustomerNumber, address, ...
            ├─ IndividualCustomers              name, national ID, ...
            └─ CorporateCustomers               company name, tax number, ...
```

Creating a customer is **one command, one transaction**: a single insert writes a row in every table of its chain, so the customer
can log in at once. The customer id is the user id. Bank staff are plain `ApplicationUsers` with the `BankStaff` role. The role is never
taken from a request: customers always get `Customer`, and staff accounts exist only through `POST /api/users/staff`.

| Role | Can do |
|---|---|
| `BankStaff` | Everything: all customers, applications, decisions, loan types, user accounts (ban, unban). |
| `Customer` | Only their own data: their record, their applications (apply, read, cancel). Browse loan types and calculate. |

| Endpoint | Purpose |
|---|---|
| `POST /api/auth/login` | E-mail + password → access token (10 min) + refresh token (30 min). 5 wrong passwords lock the account for 15 minutes. |
| `POST /api/auth/refresh` | Refresh token → a new pair. A refresh token works **once**; using an old one again ends the whole session. |
| `POST /api/auth/logout` | Ends the session of the current token. |
| `GET /api/auth/me` | Who the current token belongs to. |
| `POST /api/auth/send-verification-email`, `POST /api/auth/verify-email` | E-mail verification (see below). |
| `GET /api/users`, `POST /api/users/staff`, `POST /api/users/{id}/ban`, `POST /api/users/{id}/unban` | Staff only. |

**Instant revocation.** Every access token carries a session id (`sid`). On every request the API checks in the database that the
session is not revoked and that its user is active and not deleted, so a **ban, a logout or a deleted customer takes effect on the very
next request**, not when the token expires. The price is one small query per request.

**E-mail verification (prepared, not live).** `IEmailSender` is the abstraction; the default `LoggingEmailSender` only logs ("not sent").
Tokens are random, stored only as a hash, valid 24 hours and usable once. `Security:EmailVerification:RequireVerifiedEmail` is `false`;
turn it on once a real sender is registered after `AddSecurityServices`. In local development `Mailing:LogBody=true` writes the e-mail
(with the token) to the API log so the flow can be tried: `docker compose logs api`.

How it fits together:

- `BankingSystemCore/Security`: `User` (login information only), JWT creation, PBKDF2 password hashing, `SecureToken`, `ICurrentUser`.
- `BankingSystemCore/Mailing`: `IEmailSender`, `LoggingEmailSender`.
- `BankingSystemCore/Application/Pipelines/Authorization`: `ISecuredRequest` + `AuthorizationBehavior`. Runs **before** validation and the
  handler: not logged in → 401, wrong role → 403. A test fails if a new request is neither secured nor on the explicit "open" list.
- Roles say *who may run* a request; `CustomerAccessPolicy` says *whose data* it may touch. A customer asking for somebody else's record
  gets 404; acting for somebody else gets 403.
- Concurrency: users and sessions have a row version, so a stale write (a login racing a ban) is refused with 409 instead of overwriting.
- The first staff account is created at startup from `Security:BootstrapStaff:Email/Password` (set in `docker-compose.yml` for local use).
  Remove those settings once it exists.
- Settings: `Security:Jwt:SecurityKey` (at least 32 bytes) must come from the environment or a secret store, never from a committed file.

Not included yet: rate limiting of the anonymous endpoints (login, refresh, verify-email), a real e-mail provider, password change / reset,
two-factor authentication for staff, changing a customer's e-mail address.

## Development Environment

No local .NET SDK is required; everything runs in the `mcr.microsoft.com/dotnet/sdk:10.0` Docker image.

### VS Code (IntelliSense, debugging)

1. Install the **Dev Containers** extension (`ms-vscode-remote.remote-containers`).
2. Open the folder and run **Dev Containers: Reopen in Container**.
3. C# Dev Kit is installed inside the container and loads `BankApp.slnx` automatically.

### Command line

```bash
./scripts/dotnet.sh restore
./scripts/dotnet.sh build
```

Both the dev container and the script mount the repository at `/src` and share the `bankapp-nuget-cache` Docker volume for NuGet packages, so they can be used interchangeably.

## Testing

Test projects: `BankingSystemCore.UnitTests`, `Application.UnitTests`, `Persistence.IntegrationTests`, `WebApi.IntegrationTests`.

Stack: xUnit v3 (Microsoft Testing Platform), Shouldly, NSubstitute, Testcontainers (SQL Server), Respawn.

```bash
./scripts/test.sh                                          # all tests
./scripts/test.sh --project tests/Persistence.IntegrationTests
./scripts/dotnet.sh test --project tests/Application.UnitTests   # unit tests only, no Docker socket
```

- **Unit tests** mock the repositories and run in under a second.
- **Integration tests** start one SQL Server container per run, build the real DI container (`AddApplicationServices` + `AddPersistenceServices`), send requests through MediatR, and empty the database before every test. The first run pulls the SQL Server image (~1.5 GB).
- `test.sh` mounts the Docker socket into the SDK container so Testcontainers can start the database. Use `dotnet.sh` for everything else.
- Write tests together with the code they cover (rule, validator, handler). Add HTTP-level tests (`WebApplicationFactory`) when controllers are added.

## Error Handling

`ExceptionMiddleware` (registered first in `WebApi/Program.cs`) turns exceptions into `application/problem+json`:

| Exception | HTTP status | Problem type |
|---|---|---|
| `BusinessException` | 400 | `urn:problem:business` |
| `ValidationException` (errors grouped by property) | 400 | `urn:problem:validation` |
| `NotFoundException` | 404 | `urn:problem:not-found` |
| `AuthorizationException` | 403 | `urn:problem:authorization` |
| anything else | 500 | `urn:problem:internal-server-error` (generic message, details only in the log) |

FluentValidation failures are converted to the core `ValidationException` by `ValidationBehavior` in the Application layer, so the core has no dependency on FluentValidation.
