# Live updates, presence and drafts

How open screens learn that a record changed, who else has it open, and how unsaved edits survive a reload. The components (`CslaBaseComponent` and its family) use all of this; feature code rarely touches it directly.

## Change notices

| Piece | Where | What it does |
|---|---|---|
| `EntityChange` | Core/Realtime | Type, key, kind (`Inserted`, `Updated`, `Deleted`, `AccessChanged`), new version, who changed it, origin circuit. **Never field data.** |
| `ChangeTopic` | Core/Realtime | `ForRecord(type, key)` for an open editor, `ForType(type)` for a list |
| `ChangePublishingInterceptor` | Data/Realtime | Turns every save into notices, after commit |
| `IEntityChangeHub` / `EntityChangeHub` | Core / Infrastructure | This instance's subscriptions; dispatch reaches only the change's two topics |
| `IEntityChangeNotifier` | Core | `InMemoryChangeNotifier` (one instance) or `PostgresChangeBackplane` (any number) |

**Publishing is automatic.**
- One notice per aggregate root a save touched. A child change counts as `Updated` for its root, and a root's own insert or delete wins over that. The type and key are the same as the history's (`[AuditEntity]` name and primary key), so they match the business object's `EntityType` and `EntityKey`.
- Rows marked `[AccessGrant(nameof(UserId), watched…)]` also send an `AccessChanged` notice keyed by that user's id when added, removed, or when a watched property changes. Today these are `UserRole`, `ProjectMemberRow` and `AppUser.IsActive`.
- Notices go out after `SaveChanges` completes, or when the surrounding transaction commits. A failed save, a rollback or a transaction disposed without commit publishes nothing. Several saves in one transaction publish one notice per record.
- A publishing failure is logged and never fails the save.
- Platform rows and `[AuditIgnore]` entities (drafts) are never announced.

**Subscribing.**
- `hub.Subscribe(topic, handler)` returns an `IDisposable`. Dispose it when the screen closes.
- Handlers run concurrently and must return quickly: schedule the refresh, don't do it inline. A handler that throws is logged and doesn't stop the others.
- Ignore your own echo by comparing `change.OriginId` with your circuit's id.
- `hub.OnResync(handler)` runs after the backplane reconnects, because notices sent meanwhile are lost: lists refresh, editors compare their version.

**Semantics:** at-most-once hints. Optimistic concurrency (the root's `xmin`) is the guarantee; a missed notice only means a screen refreshes later.

## The PostgreSQL backplane

- `PublishAsync` dispatches to this instance's hub, then `NOTIFY app_entity_changes` on a pooled connection with a small JSON payload (`instance, type, key, kind, version, userId, userName, origin`, about 250 bytes).
- A dedicated connection `LISTEN`s on `app_entity_changes` and `app_presence`, drops its own instance's echoes, and hands the rest to one dispatch loop (bounded at 10 000, oldest dropped).
- It probes the connection after 30 s of silence. When the connection is lost it reconnects with backoff (0.5 s doubling to 30 s), then raises Resync.
- `/health/ready` reports **Degraded** (still HTTP 200) while it isn't listening: the instance serves correct data, only live hints are missing.

## Presence

- `IPresenceTracker.Join(topic, new PresenceEntry(userId, name, circuitId, mode))` announces the circuit until the handle is disposed. Join again with the same circuit to switch between `Viewing` and `Editing`.
- `Present(topic)` lists everyone on the topic, one entry per user (`Editing` wins), including the caller: filter yourself out.
- `Watch(topic, changed)` calls back when the topic's presence changes.
- Local entries are re-announced every 30 s. Another instance's entries expire 90 s after their last announcement. A fresh instance learns existing presence from the next heartbeat.

## Access changes

An `AccessChanged` notice for a user:
- clears that user from `UserDirectory`'s cache on every instance;
- makes that user's open circuits revalidate at once (`UserRevalidatingAuthenticationStateProvider`). A deactivated user, or one whose roles changed, is signed out of the circuit and signs in again with current roles;
- tells open screens to re-check access (plan 5): an editor whose record is now hidden closes with a message and its draft is discarded.

## Drafts

| Piece | Where | What it does |
|---|---|---|
| `IDraftStore` / `DraftStore` | Core / Data | Save, get and delete the signed-in user's draft of one record |
| `DraftCodec` | Business | A business object graph to bytes and back (CSLA's serializer: dirty and new flags survive) |
| `DraftCleanupService` | Infrastructure | Deletes expired drafts at startup and hourly |

- Keyed by (user id, entity type, entity key) in the `draft` table. One draft per user and record; saving again replaces it.
- Encrypted with Data Protection, purpose `CslaStarter.Drafts.v1` plus the user id. A payload that doesn't decrypt (tampered with, copied from another user, or its key retired) reads as no draft.
- Expires 8 hours after it was saved. Reads ignore expired drafts.
- A key for another user throws `UnauthorizedAccessException`.
- Each call uses its own short-lived context, so several components of one circuit can save at once.
- Never in the browser: Protected B data stays on the server.

## Configuration

| Setting | Values |
|---|---|
| `Realtime:Backplane` | `Postgres` (default) or `InMemory` (one instance, some tests). Anything else fails at startup. |

`AddCslaStarterRealtime(configuration)` registers the hub, presence, the backplane (as notifier, presence transport and hosted service), the readiness check and the draft cleanup. `AddCslaStarterData` registers the publishing interceptor and `IDraftStore`.

## Tests

- `Realtime/EntityChangeHubTests`: topic dispatch, disposal, failing handlers, Resync.
- `Realtime/ChangePublishingTests`: one notice per root, transactions, rollbacks, failed saves, access notices.
- `Realtime/BackplaneTests`: two instances on one database, echo drop, reconnect (kills the listeners with `pg_terminate_backend`), payload shape.
- `Realtime/PresenceTests`: join and leave, heartbeat and expiry with `FakeTimeProvider`, two instances.
- `Realtime/UserAccessTests`, `Drafts/DraftStoreTests`, `Business.Tests/Drafts/DraftCodecTests`.
