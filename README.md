# TodoApp — Task Management REST API

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)
![EF Core](https://img.shields.io/badge/EF%20Core-10.0-512BD4?logo=dotnet&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL%20Server-2022-CC2927?logo=microsoftsqlserver&logoColor=white)
![Tests](https://img.shields.io/badge/tests-211%20passed-brightgreen?logo=checkmarx)
![Architecture](https://img.shields.io/badge/architecture-Clean%20Architecture-blue)
![SignalR](https://img.shields.io/badge/SignalR-Real--time-blueviolet?logo=dotnet)
![License](https://img.shields.io/badge/license-MIT-green)

A production-ready ASP.NET Core 10 REST API for task management, featuring real-time notifications via SignalR, two-factor authentication (TOTP), role-based authorization, refresh token rotation, email-based password reset, and 211 automated tests.

Live demo: `https://your-api.azurewebsites.net/swagger`

---

## Table of Contents

- [Architecture](#architecture)
- [Features](#features)
- [API Endpoints](#api-endpoints)
- [Security](#security)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [Running Tests](#running-tests)
- [Project Structure](#project-structure)
- [Documentation](#documentation)
- [Roadmap](#roadmap)
- [License](#license)

---

## Architecture

The project follows **Clean Architecture** with a strict inward dependency rule. The Domain layer has zero external dependencies; the Application layer depends only on abstractions. Business logic is fully decoupled from infrastructure concerns and can be tested in isolation.

```
┌─────────────────────────────────────────────────┐
│  TodoApp.Api          Controllers · Middleware   │
│                       SignalR Hub · Background   │
├─────────────────────────────────────────────────┤
│  TodoApp.Application  Services · DTOs           │
│                       Validators · Interfaces   │
├─────────────────────────────────────────────────┤
│  TodoApp.Domain       Entities · Enums          │
│                       Custom Exceptions         │
├─────────────────────────────────────────────────┤
│  TodoApp.Infrastructure  EF Core · Repositories │
│                          External Services      │
└─────────────────────────────────────────────────┘
```

---

## Features

### Authentication & Security

- **JWT + Refresh Token Rotation** — Every refresh invalidates the previous token; stolen tokens cannot be reused indefinitely.
- **Refresh Token Hashing** — Tokens are stored as SHA-256 hashes; a database leak does not expose usable tokens.
- **Two-Factor Authentication (TOTP)** — Google Authenticator-compatible 2FA with enable/disable/verify flow.
- **Rate Limiting** — Per-IP limits on sensitive endpoints (Login: 5/min, Register: 3/min, Forgot Password: 2/min).
- **Security Headers** — HSTS, CSP, X-Frame-Options, X-Content-Type-Options applied globally.
- **Timing Attack Prevention** — A dummy BCrypt hash is computed when a user is not found, preventing response-time-based user enumeration.
- **BCrypt DoS Protection** — Password input is capped at 128 characters to prevent hash-computation denial-of-service.
- **User Enumeration Prevention** — Forgot-password endpoint always returns the same message regardless of whether the email exists.

### Task Management

- **Todo Lists** — Group tasks into named lists (e.g., Work, Personal, Project X).
- **Todo Items** — Full CRUD with priority, due date, and status management.
- **Subtasks** — Nested subtasks per item (capped at 50 to prevent abuse).
- **Soft Delete & Trash** — Deleted tasks are recoverable; permanent deletion is a separate explicit action.
- **Toggle Complete** — Tasks and subtasks can be toggled between complete and incomplete.
- **Dynamic Filtering** — Filter by status, priority, date range, search term, share type, and sort order with server-side pagination.
- **Activity Audit Log** — Every create, update, share, complete, and delete action on a task is recorded with a timestamp.
- **Tags** — Admin-managed global tags assignable to any task.

### Collaboration

- **Task Sharing** — Share tasks with other users by email; shared users can view and add subtasks.
- **Leave Shared Task** — Shared users can remove themselves from a task at any time.
- **Ownership Transfer** — Owners can initiate a transfer request; the recipient must accept, preserving data integrity.

### Real-time & Automation

- **SignalR WebSocket Hub** — Connected clients receive instant push notifications when a task is shared, updated, or completed.
- **Background Reminder Service** — A hosted `BackgroundService` sends email reminders for tasks with approaching due dates.
- **Email Password Reset** — Secure, time-limited reset tokens delivered by email (MailKit / SMTP).

### Cross-Cutting

- **RFC 7807 Problem Details** — All error responses follow the IETF standard format.
- **FluentValidation** — Declarative input validation with detailed field-level error messages.
- **Serilog Structured Logging** — Configurable log levels per namespace, console sink with structured output.
- **Options Pattern** — All settings (`JwtSettings`, `SmtpSettings`, etc.) are bound to typed classes and validated at startup.
- **EF Core Global Query Filter** — `IsDeleted` is enforced at the ORM level; developers cannot accidentally expose soft-deleted records.

---

## API Endpoints

**48 total endpoints** (REST + SignalR). All protected endpoints require `Authorization: Bearer <JWT>`.

<details>
<summary><strong>Authentication — /api/auth</strong></summary>

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `POST` | `/api/auth/register` | Register a new user | Public |
| `POST` | `/api/auth/login` | Authenticate and receive JWT + refresh token | Public |
| `POST` | `/api/auth/login-2fa` | Complete login with TOTP code | Public |
| `POST` | `/api/auth/refresh` | Rotate refresh token | Public |
| `POST` | `/api/auth/logout` | Revoke refresh token | Public |
| `POST` | `/api/auth/forgot-password` | Send password reset email | Public |
| `POST` | `/api/auth/reset-password` | Reset password with token | Public |
| `PUT`  | `/api/auth/change-password` | Change password (authenticated) | User |
| `POST` | `/api/auth/2fa/enable` | Generate TOTP QR code | User |
| `POST` | `/api/auth/2fa/verify` | Verify and activate 2FA | User |
| `POST` | `/api/auth/2fa/disable` | Disable 2FA | User |

</details>

<details>
<summary><strong>Todo Items — /api/todoitems</strong></summary>

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET`    | `/api/todoitems` | List tasks (filtered, paginated) | User |
| `POST`   | `/api/todoitems` | Create a task | User |
| `GET`    | `/api/todoitems/{id}` | Get task detail | Owner / Shared |
| `PUT`    | `/api/todoitems/{id}` | Update task | Owner |
| `PATCH`  | `/api/todoitems/{id}/complete` | Toggle complete/incomplete | Owner |
| `DELETE` | `/api/todoitems/{id}` | Soft delete (moves to trash) | Owner |
| `DELETE` | `/api/todoitems/{id}/permanent` | Permanently delete | Owner |
| `POST`   | `/api/todoitems/{id}/restore` | Restore from trash | Owner |
| `GET`    | `/api/todoitems/trash` | List trashed tasks | User |
| `GET`    | `/api/todoitems/{id}/activities` | Audit log for a task | Owner / Shared |

</details>

<details>
<summary><strong>Subtasks, Sharing, Tags, Lists, Users</strong></summary>

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `POST`   | `/api/todoitems/{id}/subtasks` | Add subtask | Owner / Shared |
| `PATCH`  | `/api/subtasks/{id}/complete` | Toggle subtask | Owner / Shared |
| `DELETE` | `/api/subtasks/{id}` | Delete subtask | Owner |
| `POST`   | `/api/todoitems/{id}/shares` | Share task by email | Owner |
| `DELETE` | `/api/todoitems/{id}/shares/me` | Leave shared task | Shared User |
| `POST`   | `/api/todoitems/{id}/transfer-requests` | Initiate ownership transfer | Owner |
| `POST`   | `/api/transfer-requests/{id}/accept` | Accept ownership transfer | Recipient |
| `POST`   | `/api/transfer-requests/{id}/reject` | Reject ownership transfer | Recipient |
| `GET`    | `/api/tags` | List tags | User |
| `POST`   | `/api/tags` | Create tag | Admin |
| `PUT`    | `/api/tags/{id}` | Update tag | Admin |
| `DELETE` | `/api/tags/{id}` | Delete tag | Admin |
| `GET`    | `/api/todolists` | List todo lists | User |
| `POST`   | `/api/todolists` | Create list | User |
| `PUT`    | `/api/todolists/{id}` | Update list | Owner |
| `DELETE` | `/api/todolists/{id}` | Delete list | Owner |
| `GET`    | `/api/users/me` | Get current user profile | User |
| `DELETE` | `/api/users/me` | Delete account (cascade cleanup) | User |

</details>

<details>
<summary><strong>Real-time — SignalR Hub</strong></summary>

| Connection | Route | Auth |
|------------|-------|------|
| WebSocket | `/hubs/todo` | `?access_token=<JWT>` query parameter |

**Server → Client Events:**

| Event | Payload | Trigger |
|-------|---------|---------|
| `ReceiveNotification` | `(string title, string message)` | Task shared, updated, or completed |

</details>

> Full request/response schemas with examples: [`docs/api-endpoints.md`](docs/api-endpoints.md)

---

## Security

A 16-point security audit was performed covering OWASP Top 10 and API-specific attack vectors. All findings are addressed.

| Attack Vector | Mitigation | Status |
|---------------|------------|--------|
| SQL Injection | EF Core parameterized queries | ✅ |
| XSS | HtmlEncode + stateless Bearer auth (no cookies) | ✅ |
| CSRF | Bearer token auth (no session cookies) | ✅ |
| Mass Assignment | Strict DTO pattern — entities never bound directly from input | ✅ |
| Brute Force | Rate limiting per IP | ✅ |
| Timing Attack | Dummy BCrypt hash on unknown user | ✅ |
| Token Theft | Refresh token rotation + SHA-256 storage | ✅ |
| User Enumeration | Uniform responses + constant-time comparison | ✅ |
| Privilege Escalation | Centralized `ITaskAuthorizationService` | ✅ |
| DoS (payload) | MaxLength constraints + subtask cap (50) | ✅ |

Full audit report: [`docs/security_audit.md`](docs/security_audit.md)

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for local SQL Server)

### Setup

```bash
# 1. Clone the repository
git clone https://github.com/your-username/TodoApp.git
cd TodoApp

# 2. Start SQL Server via Docker
docker-compose up -d

# 3. Configure secrets
cd src/TodoApp.Api
dotnet user-secrets set "Jwt:Key" "your-secret-key-at-least-32-characters-long"
dotnet user-secrets set "Jwt:Issuer" "TodoApp"
dotnet user-secrets set "Jwt:Audience" "TodoAppUser"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=TodoAppDb;User Id=sa;Password=YourPassword123!;TrustServerCertificate=True"
dotnet user-secrets set "Smtp:Host" "smtp.example.com"
dotnet user-secrets set "Smtp:Port" "587"
dotnet user-secrets set "Smtp:Username" "noreply@example.com"
dotnet user-secrets set "Smtp:Password" "your-smtp-password"
dotnet user-secrets set "Smtp:FromEmail" "noreply@example.com"
dotnet user-secrets set "Smtp:FromName" "TodoApp"

# 4. Apply database migrations
dotnet ef database update --project ../TodoApp.Infrastructure

# 5. Run the API
dotnet run

# Swagger UI → https://localhost:5001/swagger
```

---

## Configuration

Copy `src/TodoApp.Api/appsettings.Example.json` to `appsettings.json` (or use environment variables / User Secrets for production).

| Section | Key | Description |
|---------|-----|-------------|
| `ConnectionStrings` | `DefaultConnection` | SQL Server connection string |
| `Jwt` | `Key` | HMAC-SHA256 signing key (min 32 chars) |
| `Jwt` | `Issuer` / `Audience` | Token issuer and audience identifiers |
| `Jwt` | `ExpiryMinutes` | Access token lifetime (default: 60) |
| `Jwt` | `RefreshTokenExpiryDays` | Refresh token lifetime (default: 7) |
| `PasswordReset` | `ExpiryMinutes` | Reset token validity window (default: 60) |
| `PasswordReset` | `ResetUrl` | Base URL for the reset link sent by email |
| `Smtp` | `Host` / `Port` | SMTP server address and port |
| `Smtp` | `FromEmail` / `FromName` | Sender identity |
| `Smtp` | `Username` / `Password` | SMTP credentials |
| `Cors` | `AllowedOrigins` | Allowed frontend origins |

> For production deployments on Azure App Service, configure all secrets as **Application Settings** (environment variables). No secrets should be committed to source control.

---

## Running Tests

```bash
dotnet test
```

Expected output:

```
Passed! - Failed: 0, Passed: 169 - TodoApp.Application.Tests.dll
Passed! - Failed: 0, Passed:  42 - TodoApp.IntegrationTests.dll
```

**Test breakdown:**

| Suite | Count | Scope |
|-------|-------|-------|
| Unit Tests | 169 | Services, validators, authorization logic, business rules (BR-001 ~ BR-030) |
| Integration Tests | 42 | Real database — cascade delete, FK constraints, end-to-end flows |
| **Total** | **211** | |

- Business rule → test mapping: [`docs/test_matrix.md`](docs/test_matrix.md)
- Manual end-to-end test guide: [`docs/postman_manual_testing_flow.md`](docs/postman_manual_testing_flow.md)

---

## Project Structure

```
TodoApp/
├── src/
│   ├── TodoApp.Api/                  # Entry point — Controllers, Middleware, Hubs, Background Services
│   ├── TodoApp.Application/          # Business logic — Services, DTOs, Interfaces, Validators
│   ├── TodoApp.Domain/               # Core — Entities, Enums, Custom Exceptions
│   └── TodoApp.Infrastructure/       # Data access — EF Core DbContext, Repositories, Email, JWT
├── tests/
│   ├── TodoApp.Application.Tests/    # 169 unit tests (xUnit + Moq)
│   └── TodoApp.IntegrationTests/     # 42 integration tests (real SQL Server)
├── docs/
│   ├── api-endpoints.md              # Full endpoint reference with request/response examples
│   ├── business-rules.md             # 30 domain business rules (BR-001 ~ BR-030)
│   ├── business-rules-layers.md      # Rule enforcement layer mapping
│   ├── security_audit.md             # 16-point security audit report
│   ├── test_matrix.md                # Business rule ↔ test coverage matrix
│   ├── smoke_test_guide.md           # Swagger-based end-to-end verification guide
│   ├── postman_manual_testing_flow.md# Step-by-step Postman testing flow (all features)
│   ├── auth_workflows.md             # Authentication flow deep-dive
│   ├── FLUTTER_API_HANDBOOK.md       # Flutter client integration handbook
│   └── flutter_integration_guide.md  # Azure + Flutter setup guide
├── docker-compose.yml                # SQL Server container for local development
└── README.md
```

---

## Documentation

| Document | Description |
|----------|-------------|
| [`docs/api-endpoints.md`](docs/api-endpoints.md) | Complete REST & SignalR endpoint reference |
| [`docs/business-rules.md`](docs/business-rules.md) | 30 business rules with database schema |
| [`docs/security_audit.md`](docs/security_audit.md) | 16-point security & performance audit |
| [`docs/postman_manual_testing_flow.md`](docs/postman_manual_testing_flow.md) | Full Postman test flow covering all features |
| [`docs/test_matrix.md`](docs/test_matrix.md) | Business rule → automated test traceability |
| [`docs/FLUTTER_API_HANDBOOK.md`](docs/FLUTTER_API_HANDBOOK.md) | Architecture and API handbook for Flutter clients |

---

## Deployment

The API is deployed to **Azure App Service** via a GitHub Actions CI/CD pipeline. The workflow builds, tests, and publishes the application on every push to `main`.

- All secrets are managed as Azure App Service **Application Settings**.
- The database is Azure SQL Server with EF Core migrations applied at startup.
- Azure SignalR Service can be substituted for the built-in hub for horizontal scale-out.

---

## Roadmap

- [ ] Account Lockout — Protection against distributed brute-force attacks
- [ ] JWT SecurityStamp — Immediate token invalidation on role/password change
- [ ] Refresh Token via HttpOnly Cookie — Increased XSS resilience
- [ ] Redis Backplane — SignalR scale-out across multiple instances
- [ ] Background Job Queue — Decouple activity logging and notifications from the request thread
- [ ] Automated Migration Pipeline — EF Core migrations applied automatically in CI/CD

---

## License

This project is licensed under the [MIT License](LICENSE).
