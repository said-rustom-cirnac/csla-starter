# Plan 4: Realtime Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Change notifications that reach only the open screens showing the changed record or type, on one server or many. On top of that: presence ("Jane is also editing"), access-changed notices, and server-side encrypted drafts. These are the services the components (plan 5) consume.

**Architecture:**

- **Core** gets the contracts.
- **Data** publishes changes automatically after commit:
  - every saved aggregate root becomes one `EntityChange` (a child change counts as an update of its root);
  - role and membership changes also become `UserAccess` notices.
- **Infrastructure:**
  - an in-memory `EntityChangeHub` dispatches each change only to subscribers of its topic;
  - a PostgreSQL LISTEN/NOTIFY backplane carries changes and presence between instances, and raises Resync after reconnecting;
  - `PresenceTracker` keeps who has which record open, with heartbeats and expiry across instances.
- **Drafts:** `DraftStore` (Data) keeps Data Protection–encrypted drafts keyed by user and record; `DraftCodec` (Business) turns a CSLA object into draft bytes and back.

**Tech Stack:** Npgsql 10.0.3 (LISTEN/NOTIFY on the shared `NpgsqlDataSource`), System.Text.Json, ASP.NET Core Data Protection, `TimeProvider`.

**Spec:** `docs/superpowers/specs/2026-11-12-csla-starter-design.md` §11 (11.1, 11.2, 11.3) and §9.1 (access-changed notices).

## Global Constraints

- **Notices** carry `EntityType`, `EntityKey`, `Kind`, `Version`, `ChangedByUserId`, `ChangedByName` and `OriginId`. **Never field data.** One notice per changed aggregate root, so the entity *is* the root (the spec's separate `RootType`/`RootKey` would always repeat it).
- **Payloads** are small JSON documents, well under PostgreSQL's 8000-byte NOTIFY limit. The channels are `app_entity_changes` and `app_presence`.
- **Topics:**
  - `ChangeTopic.ForRecord(type, key)` is for open editors.
  - `ChangeTopic.ForType(type)` is for lists.
  - Dispatch touches only those two topics' subscribers. The cost is O(subscribers of the topic).
- **Delivery** happens after commit only; a rolled-back save publishes nothing. Delivery is at-most-once, so after a backplane reconnect the hub raises `Resync`.
- **Presence:** a heartbeat every 30 seconds, and entries expire after 90 seconds without one. Mode is `Viewing` or `Editing`.
- **Drafts:**
  - Keyed by (user id, entity type, entity key), and they expire 8 hours after they were saved.
  - The payload is encrypted with Data Protection purpose `CslaStarter.Drafts.v1`.
  - A user can only read, save or delete their own drafts.
  - Drafts are not audited (`[AuditIgnore]`).
- **Backplane:** `Realtime:Backplane` is `Postgres` (the default) or `InMemory` (used for tests and single-instance development).
- **User access:** a `UserAccess` notice keyed by user id clears that user's directory cache on every instance. This fixes the plan 2 gap where `Invalidate` was local to one instance.

## Review Focus

1. **Saving a project where only a task changed must notify the project's record topic once,** not once per changed row. *Pinned by* `Publish_ChildOnlyChange_NotifiesRootOnce` (Task 2).
2. **A rolled-back transaction must publish nothing.** *Pinned by* `Publish_RolledBack_PublishesNothing` (Task 2).
3. **On instance A, a change must reach A's subscribers exactly once,** not again through its own NOTIFY echo. *Pinned by* `Backplane_OwnEcho_IsNotDispatchedTwice` (Task 3).
4. **After the listener connection drops, the hub must raise `Resync` and keep receiving.** *Pinned by* `Backplane_AfterReconnect_RaisesResync_AndReceives` (Task 3).
5. **Another user's draft for the same record must never be returned.** *Pinned by* `Drafts_OtherUser_CannotReadOrDelete` (Task 5).

---

### Task 1: Contracts and the in-memory hub

**Files:**
- Create in `src/CslaStarter.Core/Realtime/`: `EntityChange.cs`, `ChangeTopic.cs`, `IEntityChangeNotifier.cs`, `IEntityChangeHub.cs`, `IPresenceTracker.cs`, `PresenceEntry.cs`
- Create in `src/CslaStarter.Core/Drafts/`: `IDraftStore.cs`, `DraftKey.cs`, `Draft.cs`
- Create in `src/CslaStarter.Infrastructure/Realtime/`: `EntityChangeHub.cs`, `InMemoryChangeNotifier.cs`, `RealtimeSetup.cs`
- Test: `tests/CslaStarter.Integration.Tests/Realtime/EntityChangeHubTests.cs`

**Interfaces (Produces):**

```csharp
public enum ChangeKind { Inserted, Updated, Deleted, AccessChanged }
public sealed record EntityChange(string EntityType, string EntityKey, ChangeKind Kind, uint? Version, Guid? ChangedByUserId, string ChangedByName, string? OriginId);
public readonly record struct ChangeTopic(string EntityType, string? EntityKey) { static ChangeTopic ForRecord(string type, string key); static ChangeTopic ForType(string type); }
public interface IEntityChangeNotifier { Task PublishAsync(IReadOnlyList<EntityChange> changes, CancellationToken cancellationToken); }
public interface IEntityChangeHub
{
    IDisposable Subscribe(ChangeTopic topic, Func<EntityChange, Task> handler);
    IDisposable OnResync(Func<Task> handler);
    Task DispatchAsync(EntityChange change);   // record topic + type topic subscribers; one failing handler never stops the others
    Task RaiseResyncAsync();
}
public const string UserAccessType = "UserAccess";   // on EntityChange; key = user id
```

- [ ] **Step 1: Write the failing tests.**
  - `Hub_RecordSubscriber_GetsOnlyItsRecord`
  - `Hub_TypeSubscriber_GetsEveryRecordOfItsType`
  - `Hub_UnrelatedSubscribers_AreNotCalled`: 1,000 subscribers on other topics; a counter shows none of them was called.
  - `Hub_DisposedSubscription_StopsReceiving`
  - `Hub_FailingHandler_DoesNotStopOthers`
  - `Hub_Resync_ReachesResyncHandlers`
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement.**
  - Subscriptions live in `ConcurrentDictionary<ChangeTopic, ImmutableArray<Subscription>>` (copy-on-write).
  - Handler exceptions are logged.
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit** with the message `DEV-38 Add realtime contracts and the in-memory change hub`.

### Task 2: Publishing changes after commit

**Files:**
- Create: `src/CslaStarter.Data/Realtime/ChangePublishingInterceptor.cs` (a `SaveChangesInterceptor` plus an `IDbTransactionInterceptor`)
- Modify: `DataServiceCollectionExtensions` (register it), and `TestDatabase.CreateContext` (accept an optional notifier)
- Test: `tests/CslaStarter.Integration.Tests/Realtime/ChangePublishingTests.cs`

**Behaviour:**

- **In `SavingChanges`:** collect one change per aggregate root that the save touches.
  - A root's own state gives Inserted, Updated or Deleted.
  - A child (`IHasAuditRoot`) counts as Updated for its root, unless the root is itself added or deleted in the same save.
  - The type and key come from `[AuditEntity]` and the primary key, the same as the audit.
  - Removed or added `UserRole` and `ProjectMemberRow` rows add `UserAccess` notices for their user id.
- **Publish:**
  - In `SavedChanges`, when no transaction is open, read each root's new `Version` (xmin) and publish.
  - When a transaction is open, hold the changes until `TransactionCommitted`; drop them on `TransactionRolledBack`.
  - A failure to publish is logged and never fails the save.

- [ ] **Step 1: Write the failing tests** with a recording notifier:
  - `Publish_Insert_PublishesInserted`
  - `Publish_ChildOnlyChange_NotifiesRootOnce`: two tasks change, so there's one Updated for `Project:<id>` with the new version.
  - `Publish_Delete_PublishesDeleted`
  - `Publish_RolledBack_PublishesNothing`
  - `Publish_CommittedTransaction_PublishesAfterCommit`
  - `Publish_MemberRemoved_PublishesUserAccessForThatUser`
  - `Publish_RoleChanged_PublishesUserAccess`
  - `Publish_CarriesChangerAndOrigin`
  - `Publish_NotifierFailure_DoesNotFailSave`
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit** with the message `DEV-38 Publish aggregate changes after commit`.

### Task 3: PostgreSQL backplane

**Files:**
- Create in `src/CslaStarter.Infrastructure/Realtime/`: `PostgresChangeBackplane.cs` (a `BackgroundService` that is also an `IEntityChangeNotifier`), `BackplaneMessage.cs`
- Modify: `RealtimeSetup.cs` (`Realtime:Backplane`)
- Test: `tests/CslaStarter.Integration.Tests/Realtime/BackplaneTests.cs`

**Behaviour:**

- **`PublishAsync`:** dispatches to the local hub first, then sends `SELECT pg_notify('app_entity_changes', @json)` on a pooled connection. The JSON carries the instance id.
- **Listener:**
  - It holds a dedicated connection with keepalive, runs `LISTEN app_entity_changes` and `LISTEN app_presence`, and loops on `WaitAsync`.
  - It ignores messages from its own instance id and dispatches everything else to the local hub.
  - On connection loss it reconnects with exponential backoff (0.5 seconds up to 30), then calls `RaiseResyncAsync`.
- **Readiness:** it reports listener health to `/health/ready`.

- [ ] **Step 1: Write the failing tests.** Each runs two backplanes on the same test database, each with its own hub and instance id.
  - `Backplane_ChangeOnA_ReachesSubscriberOnB`
  - `Backplane_OwnEcho_IsNotDispatchedTwice`
  - `Backplane_AfterReconnect_RaisesResync_AndReceives`: kill B's listener with `pg_terminate_backend`.
  - `Backplane_PayloadIsSmallAndHasNoFieldData`
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit** with the message `DEV-38 Add PostgreSQL LISTEN/NOTIFY backplane`.

### Task 4: Presence and cross-instance user access

**Files:**
- Create: `src/CslaStarter.Infrastructure/Realtime/PresenceTracker.cs`
- Modify:
  - `PostgresChangeBackplane` (presence messages: join, leave, heartbeat)
  - `UserDirectory`: subscribe to `UserAccess` and clear by user id, keeping a userId → (tid, oid) map
  - `UserRevalidatingAuthenticationStateProvider`: revalidate immediately on a `UserAccess` notice for its user
- Test: `tests/CslaStarter.Integration.Tests/Realtime/PresenceTests.cs`, `UserAccessTests.cs`

**Interfaces (Produces):**

```csharp
public enum PresenceMode { Viewing, Editing }
public sealed record PresenceEntry(Guid UserId, string DisplayName, string CircuitId, PresenceMode Mode);
public interface IPresenceTracker
{
    IDisposable Join(ChangeTopic topic, PresenceEntry me);              // leave on dispose
    IReadOnlyList<PresenceEntry> Present(ChangeTopic topic);            // everyone else on the topic, across instances
    IDisposable Watch(ChangeTopic topic, Func<Task> changed);
}
```

- [ ] **Step 1: Write the failing tests.**
  - `Presence_JoinAndLeave_AreVisibleToWatchers`
  - `Presence_AcrossInstances_JoinOnAVisibleOnB`
  - `Presence_RemoteEntryWithoutHeartbeat_ExpiresAfter90Seconds` (uses `FakeTimeProvider`)
  - `Presence_SameUserTwoCircuits_ListedOnce`
  - `UserAccess_Notice_ClearsDirectoryCacheOnEveryInstance`
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement.** Use one heartbeat timer per tracker, and sweep expired entries on the same tick.
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit** with the message `DEV-38 Add presence and cross-instance user access notices`.

### Task 5: Drafts

**Files:**
- Create: `src/CslaStarter.Data/Entities/DraftRow.cs`, `Configurations/DraftConfiguration.cs`, `src/CslaStarter.Data/Drafts/DraftStore.cs`, and the migration `AddDrafts`
- Create: `src/CslaStarter.Business/Drafts/DraftCodec.cs` (CSLA serialization of an object graph to and from `byte[]`)
- Create: `src/CslaStarter.Infrastructure/Realtime/DraftCleanupService.cs` (hourly; deletes expired drafts in batches of 500 through EF)
- Test: `tests/CslaStarter.Integration.Tests/Drafts/DraftStoreTests.cs`, `tests/CslaStarter.Business.Tests/Drafts/DraftCodecTests.cs`

**Interfaces (Produces):**

```csharp
public sealed record DraftKey(Guid UserId, string EntityType, string EntityKey);
public sealed record Draft(uint? Version, byte[] Payload, DateTimeOffset SavedAt);
public interface IDraftStore
{
    Task SaveAsync(DraftKey key, uint? version, byte[] payload, CancellationToken ct);  // upsert; expires 8 h after saved
    Task<Draft?> GetAsync(DraftKey key, CancellationToken ct);                          // null when missing or expired
    Task DeleteAsync(DraftKey key, CancellationToken ct);
}
public static class DraftCodec { byte[] Serialize(ApplicationContext ctx, object graph); T Deserialize<T>(ApplicationContext ctx, byte[] payload); }
```

- [ ] **Step 1: Write the failing tests.**
  - `Drafts_SaveAndGet_RoundTrips`
  - `Drafts_PayloadIsEncryptedAtRest`: the stored bytes don't contain the plaintext.
  - `Drafts_OtherUser_CannotReadOrDelete`: a key for another user than the store's `IDataAccessContext` throws `UnauthorizedAccessException`.
  - `Drafts_Expired_ReturnsNull`
  - `Drafts_Cleanup_DeletesExpiredOnly`
  - `DraftCodec_ProjectWithEdits_RoundTripsDirtyState`: a dirty `ProjectEdit` with an added task deserializes still dirty, with the task present.
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement.** `DraftRow` is `[AuditIgnore]`, with a composite key (user_id, entity_type, entity_key).
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit** with the message `DEV-38 Add encrypted server-side drafts`.

### Task 6: Documentation

**Files:** `docs/claude/realtime.md` (new); updates to the root `CLAUDE.md` guides list, `src/CslaStarter.{Core,Data,Infrastructure}/CLAUDE.md`, and `docs/claude/testing.md`.

- [ ] **Step 1: Write the guide** from the code: topics, publishing, the backplane, presence, drafts, configuration, and failure behaviour.
- [ ] **Step 2: Run `dotnet test`.** Expected: all pass.
- [ ] **Step 3: Commit** with the message `DEV-38 Document live updates, presence and drafts`.
