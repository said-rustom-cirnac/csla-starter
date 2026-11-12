# CslaStarter.Infrastructure

Runtime services that aren't data access: the change hub and its PostgreSQL LISTEN/NOTIFY backplane, presence, claims transformation (roles from our database), culture handling, the idle-timeout circuit handler, telemetry and health checks.

**May reference:** Core, Data, Npgsql.

## Rules

- **Change notices** carry type, key, version and who made the change. They never carry field data.
- **Delivery** is at-most-once, so notices are hints. Optimistic concurrency is the guarantee.
- **Dispatch** only reaches subscribers of the changed topic, `(type, key)` or `(type)`. Never broadcast.
- **Claims transformation** must be idempotent. It may run several times per request.

Guides: [architecture](../../docs/claude/architecture.md).
