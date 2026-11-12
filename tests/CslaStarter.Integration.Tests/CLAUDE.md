# CslaStarter.Integration.Tests

Tests against a real PostgreSQL 18 database, started by Testcontainers (Docker required). They cover the schema, roles and grants, audit capture and append-only protection, the security event log, Data Protection keys, sign-in plumbing and the web host.

## How it works

- `PostgresFixture` (an assembly fixture) starts one container per run. It runs `deploy/sql/create-roles.sql` and migrates a template database as `app_owner`.
- `TestDatabase.CreateAsync(postgres)` gives each test its own copy of that template. `CreateContext()` connects as `app_runtime`, as the app does; pass `asOwner: true` for owner-only checks.
- `FakeDataAccessContext.Tester` is the default "who made this change" for audit tests.

## Rules

- Create a fresh `TestDatabase` per test (in `InitializeAsync`). Never share rows between tests.
- Assert PostgreSQL errors by `SqlState` (for example `42501` permission denied), not by message text, except for trigger messages the app defines.

Guide: [testing](../../docs/claude/testing.md).
