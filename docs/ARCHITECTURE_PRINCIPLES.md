# Architecture Principles

A domain-independent rulebook for building layered, request-driven backend systems.
The rules are written as constraints, not suggestions. Each rule states what must hold and, where it is not obvious, why.

**Keywords.** MUST / MUST NOT = a violation is a defect. SHOULD / SHOULD NOT = deviate only with a written reason. MAY = optional.

---

## 1. Layering and the dependency rule

### 1.1 Layers

| Layer | Responsibility | May depend on |
|---|---|---|
| **Kernel** | Domain-agnostic building blocks: base entity, repository abstractions, paging, error types, pipeline behaviors, security primitives. | nothing in the solution |
| **Domain** | Persistent entities and enumerations. State and invariants of the model. | Kernel |
| **Application** | Use cases: commands, queries, handlers, validators, business rules, mappers, ports (interfaces) to infrastructure. | Domain, Kernel |
| **Infrastructure** (persistence, messaging, mail, ...) | Implements ports defined by Application. | Application |
| **Host** (API, worker, CLI) | Composition root and transport. Translates protocol to requests and results to protocol. | Application, Infrastructure |

### 1.2 Rules

1. **Dependencies point inward only.** Source-code references go Host → Infrastructure → Application → Domain → Kernel. A reference in the other direction MUST NOT exist.
2. **The Kernel is reusable.** It MUST NOT contain business vocabulary, product names, or references to any other project of the solution. If a piece of code only makes sense for one product, it does not belong in the Kernel.
3. **Application defines ports, Infrastructure implements them.** Application MUST NOT reference a database driver, an ORM provider, an HTTP client, or a mail library. It talks to interfaces.
4. **Domain holds data and invariants only.** It MUST NOT contain use-case orchestration, I/O, or framework attributes tied to a transport.
5. **The Host is thin.** A controller/endpoint maps transport input to one request object, sends it to the mediator, and maps the result back. It MUST NOT contain business rules, queries, or transaction logic.
6. **Only the composition root wires things together.** Each layer exposes exactly one registration entry point (`Add<Layer>Services`). No layer registers another layer's types.
7. **Dependency direction is enforced by tests or build rules**, not by convention alone.

---

## 2. Request-driven design (CQRS)

1. **Every use case is exactly one request** (command or query) with exactly one handler.
2. **Commands change state, queries read state.** A query MUST NOT write. A command MAY return the minimum needed by the caller (an identifier, the created resource), not an arbitrary read model.
3. **Request and response are separate types.** Persistence entities MUST NOT cross the Application boundary in either direction. Inbound data is a request; outbound data is a response DTO.
4. **Transport-only data is not part of the request.** Anything the caller must not control (actor identity, role, timestamps, tenant, ownership) is resolved server-side and never bound from the payload.
5. **One use case = one unit of work.** A handler persists its changes through a single save so the outcome is atomic. A handler MUST NOT leave half-written state on failure.
6. **Handlers orchestrate; rules decide.** Business checks live in named rule objects (one class per feature area) that throw typed errors. Handlers call rules; they do not inline conditionals that encode policy.
7. **Handlers are stateless** and have no hidden dependencies on request-scoped globals other than injected abstractions (current user, clock).

### 2.1 Pipeline order

Cross-cutting concerns are mediator pipeline behaviors, executed in this fixed order:

```
Authorization → Validation → (Logging / Transaction / Caching, if present) → Handler
```

- Authorization MUST run before validation so an unauthenticated or unauthorized caller learns nothing about the shape of the request.
- Validation MUST run before the handler so handlers never see structurally invalid input.
- A behavior MUST be constrained to a base request marker that covers **both** result-returning and result-less requests. Constraining on the result-returning interface only silently skips result-less requests, which is a security defect when the behavior is authorization.

---

## 3. Validation and business rules

1. **Two kinds of checks, kept apart.**
   - *Structural validation* (shape, format, ranges, required fields) is stateless, declarative, and runs in the pipeline.
   - *Business rules* (uniqueness, existence, state transitions, ownership) need data and run inside the handler through rule objects.
2. Structural validators MUST NOT access the database.
3. Validation failures are collected and reported together, grouped by property, not one at a time.
4. The Kernel's error type for validation is library-independent. The Application layer converts the validation library's failures into it, so the Kernel has no dependency on that library.
5. **State transitions are explicit.** Entities with lifecycle states expose an allowed-transition check; a request to leave an invalid state fails with a business error.
6. **Every rule has a test, and every message is a constant** (no inline strings in rules).

---

## 4. Error model

1. **Errors are typed exceptions at the Application boundary, translated once at the Host.** There is one exception-handling middleware, registered first in the pipeline.
2. Required error types and their transport mapping:

| Error type | Meaning | HTTP |
|---|---|---|
| Business | Request is well-formed but violates a rule | 400 |
| Validation | Structural failures, grouped by property | 400 |
| Authentication | Caller identity missing or invalid | 401 |
| Authorization | Identity known, action not permitted | 403 |
| NotFound | Target does not exist **or is not visible to the caller** | 404 |
| Conflict | Concurrent modification detected | 409 |
| Unexpected | Anything else | 500 |

3. **Responses use one machine-readable format** (RFC 7807 `application/problem+json`) with a stable `type` URI per error kind, a `traceId`, and the request `instance`.
4. **Unexpected errors never leak internals.** The client gets a generic message and a trace id; the stack trace and details go to the log only.
5. **Existence is not disclosed across ownership boundaries.** Reading another principal's record returns *not found*, not *forbidden*. Acting on behalf of another principal returns *forbidden*.
6. **Concurrency conflicts are errors, not silent overwrites** (see §6.5) and surface as 409.

---

## 5. Security

### 5.1 Principles

1. **Secure by default.** An endpoint or request is protected unless it is explicitly declared open. Opening one is a reviewed decision.
2. **Defense in depth.** Authentication at the transport, authorization in the pipeline, ownership in the handler. Each layer assumes the others can fail.
3. **Least privilege.** Roles grant the minimum set of use cases. Elevated roles are created only through dedicated, access-controlled operations.
4. **Fail closed.** Missing configuration, an unreadable claim, or an unknown role results in denial, never in a default allow.

### 5.2 Authorization model

1. Authorization is split into two questions that MUST be answered separately:
   - **Who may run this use case?** Declared per request (an interface listing the allowed roles; empty = any authenticated caller). Enforced by the authorization behavior.
   - **Whose data may it touch?** Enforced in the handler by an ownership policy that compares the caller to the target.
2. **Roles are declared on the request type but are not part of its payload.** They are implemented so they never appear in serialization, model binding, or API documentation.
3. **A contract test enumerates every request in the Application assembly** and fails when one is neither secured nor on an explicit "deliberately open" list. A second test pins each request's role set, so any change to access is visible in a diff.
4. **The actor identity comes only from the validated token** via an abstraction (`ICurrentUser`), never from request data.
5. **Privileged roles are never assignable by an end-user-facing operation.** The role of a created account is decided by the use case, not by the caller.

### 5.3 Identity model

1. **Authentication data is separate from domain data.** The Kernel's user type holds login information only (identifier, credential hash, lockout state, verification state, active flag, concurrency token). Application-specific attributes live in derived types.
2. **Inheritance maps to table-per-type** when subtypes represent real specializations. Creating a subtype writes every table of its chain in one transaction; the subtype id equals the base user id.
3. **The principal id in the token equals the entity id** of the authenticated account. There is no second identifier to keep in sync.
4. **Login identifiers are unique** (filtered unique index that ignores soft-deleted rows) and compared case-insensitively.

### 5.4 Credentials

1. Passwords are stored only as salted, slow, adaptive hashes (PBKDF2-SHA256 ≥ 600 000 iterations, or Argon2id / bcrypt). The iteration count is configurable and stored with the hash so it can be raised later.
2. Password comparison is constant-time.
3. A password policy is enforced on **every** operation that sets a password.
4. **Account lockout:** N consecutive failures lock the account for a fixed period. Lockout state is persisted, not in memory.
5. Login responses MUST NOT reveal whether the identifier exists. The same error and comparable timing are used for "unknown user" and "wrong password".
6. Hashes, tokens, and secrets MUST NOT appear in any response DTO, log line, or exception message. A test asserts this for list/read endpoints.

### 5.5 Tokens and sessions

1. **Short-lived access token, rotating refresh token.** The access token is a signed JWT. The refresh token is opaque.
2. **Every access token is bound to a server-side session** (a `sid` claim). The token alone is never sufficient.
3. **Instant revocation.** On every authenticated request the Host asks a session validator whether the session is not revoked and the account is active and not deleted. Ban, logout, and deletion therefore take effect on the next request, not at token expiry. The cost is one small indexed query per request; it MUST NOT be cached in a way that delays revocation.
4. **Refresh tokens are single-use.** Using one issues a new pair and invalidates the old one. **Reuse of an already-used token revokes the whole session** (theft signal).
5. Refresh tokens and verification tokens are generated from a cryptographic RNG, stored **only as a hash**, and compared by hash.
6. Revoking all of a principal's sessions is a first-class operation, executed in bounded batches.
7. **Signing keys:** the key MUST come from the environment or a secret store, never from a committed file; minimum 256 bits. Plan for rotation (a `kid` header and a window where old and new keys are both accepted). A leaked key lets anyone mint tokens with any role.
8. Token validation requires signature, issuer, audience, lifetime, and a zero or very small clock skew. The accepted algorithm is pinned; `none` and algorithm switching are rejected.

### 5.6 Verification flows

1. Out-of-band verification (e-mail, phone) uses single-use, expiring, hash-stored tokens.
2. Delivery sits behind an abstraction (`IEmailSender`). A logging implementation MAY be the default in development; it MUST be replaceable without touching use cases. Logging of message bodies is a development-only switch.
3. Whether verification is *required* for login is a configuration flag, defaulting to the safest value the product can tolerate.

### 5.7 Hardening checklist (apply before exposure)

- Rate limiting on every anonymous endpoint (login, refresh, verification, reset). Keyed by client address and by identifier. Behind a proxy, forwarded headers MUST be configured correctly.
- HTTPS only, HSTS, restrictive CORS, security response headers, request size limits.
- Audit log of security-relevant and state-changing actions (who, what, when, from where, outcome).
- Encryption at rest for sensitive personal fields; masking in logs.
- Idempotency keys on create operations that clients may retry.
- Multi-factor authentication for privileged roles.
- Dependency and secret scanning in CI.

---

## 6. Persistence

### 6.1 Entities

1. All entities derive from one base type with a generic primary key and standard audit fields (`CreateDate`, `UpdateDate`, `DeleteDate`).
2. **Soft delete is the default.** A global query filter excludes soft-deleted rows. Hard delete is an explicit, separate operation.
3. Navigation properties that may be unloaded are nullable, and mapping code fails loudly when a required navigation was not included. It MUST NOT yield `null`-reference errors later.

### 6.2 Repositories

1. One generic repository abstraction in the Kernel (get, list, any, add, add-range, update, update-range, delete, delete-range, paging), implemented once over the ORM.
2. Feature-specific repositories extend it only for queries that cannot be expressed generically.
3. Repository interfaces live in Application; implementations live in Infrastructure.
4. **Every list operation is paged.** There is no unbounded read endpoint. Page size has an enforced upper bound.
5. Read-only queries use no-tracking.
6. Data-access conventions that depend on the database provider (filtered indexes, check constraints) are isolated in configuration classes so a provider change touches one place.

### 6.3 Configuration and migrations

1. One entity configuration class per entity. No mapping by attribute or convention that the team cannot see in one place.
2. **Constraints belong in the database as well as in code:** unique indexes, foreign keys, check constraints, required columns, max lengths.
3. Migrations are generated, reviewed, and committed. A migration MUST NOT be edited after it has been applied outside a developer machine.
4. Applying migrations automatically at startup is allowed in development only. Other environments apply them as an explicit deployment step.
5. Reference/seed data ships inside migrations with fixed identifiers so it is identical in every environment.

### 6.4 Transactions

1. A request's changes are saved once. If multiple aggregates must change together, they are added to the same unit of work before the single save.
2. Operations that bypass change tracking (bulk updates/deletes) MUST be reviewed for compatibility with inheritance mappings and global filters.

### 6.5 Concurrency

1. Entities that can be changed by competing flows (accounts, sessions, anything with state transitions) carry a **row-version concurrency token**.
2. A stale write fails; the Host maps it to 409. Handlers MUST NOT catch it to retry blindly.
3. A flow that only needs to read an entity MUST NOT write it back. Read with no-tracking so an unrelated stale copy cannot overwrite a newer decision (for example a revocation).

---

## 7. Mapping

1. Mapping between entities and DTOs is **compile-time generated** (source generators), not reflection-based, so errors surface at build time and performance is predictable.
2. Mappers are static partial classes, one per feature, placed with the feature.
3. **Target members are mandatory.** An unmapped target member is a build error unless it is explicitly ignored. Fields the caller must never set (identifiers, role, credentials, audit fields) are ignored explicitly.
4. A contract test per mapper asserts that every target member is mapped or deliberately ignored, and that sensitive source fields never reach a response type.

---

## 8. Configuration and secrets

1. **Typed options bound from configuration**, validated at startup (fail fast on missing or invalid values).
2. **Layering of sources:** defaults in the repository, environment-specific overrides, then environment variables / secret store. Later sources win.
3. **Secrets are never committed.** Connection strings, signing keys, provider credentials come from the environment or a secret manager. Development defaults in a compose file are labeled as such and are not reused anywhere else.
4. The composition root validates the DI container on build (scope validation and build-time resolution checks) so lifetime errors fail at startup.
5. Bootstrap accounts or data (first administrator) come from configuration and are removable once the system has been initialized.

---

## 9. Dependency injection

1. Constructor injection only. No service locator, no static state.
2. Default lifetime is **scoped** for anything touching the unit of work; singletons are stateless or thread-safe by construction.
3. Time and randomness are injected (`TimeProvider`, RNG abstractions) so behavior is testable.
4. The current user is an abstraction resolved from the request context; Application code never touches the HTTP context.

---

## 10. API contract

1. Resource-oriented routes, nouns not verbs; state transitions that are not CRUD are explicit sub-resources (`POST /things/{id}/approve`).
2. Enumerations are serialized as strings.
3. The API is described by a generated OpenAPI document; interactive documentation is generated from it. Every request body has a realistic example.
4. Authentication is declared in the document (bearer scheme) so tools can authorize.
5. List endpoints share one paging contract (index, size, total, pages, has-next, has-previous, items).
6. Breaking changes require a new API version. Additive changes do not.
7. Input is trusted nowhere: IDs from the route are verified against the caller's access before use.

---

## 11. Testing

### 11.1 Test pyramid

| Level | Scope | Infrastructure | Speed |
|---|---|---|---|
| **Kernel unit** | Pure building blocks (middleware, hashing, tokens, pipeline behaviors) | none | ms |
| **Application unit** | Rules, validators, mappers, handlers with mocked ports | none | ms |
| **Integration** | Real handlers + real ORM against a real database in a container | container | seconds |
| **API integration** | Full HTTP stack through the test host, real authentication | container | seconds |

### 11.2 Rules

1. **Tests are written together with the code they cover**, not afterwards. A rule, validator, mapper, or handler without a test is incomplete.
2. **Integration tests use the real database engine** (container), not an in-memory substitute. Behavior differences (constraints, collation, concurrency) are exactly what they exist to catch.
3. One database container per test run; the database is **reset between tests** with a data-reset tool. Seed/reference data is restored after each reset. Reset MUST NOT touch the migrations history.
4. Tests are independent and order-agnostic. A test never depends on data created by another.
5. **Contract tests guard architecture:** every request is secured or listed as open; every request's roles are pinned; every mapper target is accounted for; the number of requests is asserted so adding one forces a conscious update.
6. **Security behaviors are tested at the HTTP level:** missing token, malformed token, tampered payload, wrong signature, wrong algorithm, expired token, revoked session, reused refresh token, wrong role, other principal's data, banned account.
7. **Mutation check for guards.** When a security test is added, remove the protection once and confirm the test fails.
8. Time-dependent logic is tested through the injected clock, never by sleeping.
9. Cross-cutting infrastructure that is disabled in tests (rate limiter, caches) must be configurable per environment so tests do not fail on shared client addresses or stale state.

---

## 12. Cross-cutting infrastructure to add behind abstractions

These are not present by default. When added, they MUST follow these constraints.

### 12.1 Caching

- Cache only data that is read often, changes rarely, and may be slightly stale (reference data).
- **Never cache authorization or session state** in a way that delays revocation (§5.5.3).
- Single instance may use in-process memory. Multiple instances need a shared distributed cache; in-process caches diverge between nodes.
- Every cache entry has an expiry and an explicit invalidation path on write.
- Cache is behind an abstraction and cleared between tests.

### 12.2 Rate limiting

- Applied at the Host, per endpoint class (anonymous, authenticated, expensive).
- Partition keys: client address for anonymous endpoints, principal id for authenticated ones.
- Behind a reverse proxy, the real client address must be resolved from trusted forwarded headers; otherwise all clients share one bucket.
- Multiple instances need a shared limiter store to be accurate.
- Limits are configuration, and tests run with limits raised or disabled.

### 12.3 Observability

- Structured logging with a correlation / trace id on every line and every error response.
- Health endpoints: *liveness* (process is up) and *readiness* (dependencies reachable) are separate.
- Metrics and distributed tracing through a vendor-neutral standard.
- Never log secrets, tokens, credential hashes, or full personal data.

### 12.4 Background work

- Long or retryable work runs outside the request, triggered by a persisted message or job, never by fire-and-forget inside a handler.
- Jobs are idempotent; retries are safe.

---

## 13. Delivery (CI/CD)

1. **Build and test run from a clean checkout with one command each**, identical locally and in CI. Containerized toolchains are acceptable and encouraged.
2. The pipeline stages, in order: restore → build (warnings as errors) → unit tests → integration tests → security scan → package → deploy.
3. Integration tests need a container runtime. CI runners MUST provide one; service containers (database, cache) are declared in the pipeline.
4. Secrets are injected by the CI secret store; they never appear in the repository, image layers, or logs.
5. **Schema changes are a deployment step** with its own approval and rollback plan, run before the new version takes traffic. Changes are backward compatible with the previous version (expand → migrate → contract).
6. Dependency updates are automated and gated by the same tests. Known-vulnerable packages fail the build.
7. Images are built once and promoted across environments; configuration, not code, differs between them.
8. Production never auto-applies migrations or seeds development data.

---

## 14. Change discipline

1. A new use case = request + handler + validator + rule(s) + mapper + endpoint + tests + contract-table entry. A change set missing any of these is incomplete.
2. Introducing a new open (anonymous) request, a new role, or a new cross-layer reference is an architectural decision and requires explicit review.
3. Rules in this document change only by deliberate edit, in the same change set as the code that needs the exception.
4. When a rule and the code disagree, one of them is wrong; fix it the same day.
