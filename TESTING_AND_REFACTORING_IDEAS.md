# Testing & Refactoring Ideas

## Testing Gaps

### Event payload coverage
- `Rename` household — blank/whitespace name edge case now covered; consider also testing that rename appears in `History`
- `RemoveMember` — removed member removed from `List` results (currently only `Read` is tested)
- `RemoveChore` — removing a chore with an active goal; verify goal is also gone

### IsValid not exercised in isolation
All `IsValid` checks are currently tested only indirectly via `HouseholdStore.Write`. Pure unit tests for `IsValid` would be cheaper and faster to run (no DB needed):
- `AddChore.IsValid` — false when label already exists
- `RenameChore.IsValid` — false when target label exists
- `RemoveMember.IsValid` — false when user not a member
- `UndoChore.IsValid` — false when timestamp not in history
- `Rename.IsValid` — false on empty/whitespace string

### Serialization round-trip
No test verifies that `HouseholdEventPayload.Serialize()` + `Deserialize()` round-trips correctly for all 10 discriminated-union variants. A single parameterized test covering all `Kind` values would catch missed `[JsonDerivedType]` registrations early.

### `HouseholdStore.List` edge cases
- Household not returned if current user was invited then removed
- Ordering of returned households (stable?)
- Household appears after creator is also an `AddMember` target (duplicate add)
- `List` respects rename (comment in `HouseholdStore.cs:129` notes this is a known gap)

### `DoChore` / `UndoChore` ordering
- `UndoChore` removes exactly the right occurrence when the same chore was done at the same timestamp (unlikely but possible in tests with a frozen clock)

### Concurrency / version conflicts
No tests exercise concurrent writes to the same household. The version column exists but no uniqueness violation test exists.

### `HouseholdNotFound` path
`Read` throws `HouseholdNotFound` for an unknown ID — not currently tested.

### `NotMemberOfHouseholdException` message
Exception carries `id` and `identity` but message content is not asserted anywhere.

---

## Refactoring Ideas

### Extract shared time helper
`ChoreTest` and `HouseholdStoreTest` both define `t(int i)` with identical logic. Extract to a shared `TestTimeHelper` static class or a base class.

### `CreateNewHousehold.IsValid` / `Apply` throw
Both methods throw `InvalidOperationException` unconditionally. This is a code smell — the type is a valid `HouseholdEventPayload` but cannot be used polymorphically. Options:
- Remove `IsValid`/`Apply` from the base type and handle genesis events separately (e.g., a distinct `IGenesisPayload` interface)
- Or make `CreateNewHousehold` not derive from `HouseholdEventPayload` at all and handle it only inside `HouseholdStore.New`

### `AddMember.IsValid` always returns `true`
No guard against adding a duplicate member. `ImmutableHashSet.Add` silently ignores duplicates so it's not a bug, but it also means the event is always valid even if semantically a no-op. Consider returning `false` when the user is already a member.

### `HouseholdStore.List` inline SQL string interpolation
`List` builds its SQL with `$"""..."""` and embeds `CreateNewHousehold.Kind` / `AddMember.Kind` as string interpolations. This is safe (constants, not user input) but looks like it could be a SQL injection risk on first read. Extracting those kind constants into a shared static location and adding a comment would clarify intent.

### `HouseholdStore.Read` called once per event in `Write`
Inside `Write`, `Read` is called at the top of every iteration of the payload loop, re-fetching all events from DB each time. For a batch of N payloads this is O(N) round-trips. Could fold subsequent events in-memory between writes within the same transaction.

### Magic string `"Kind"` / `"User"` in SQL
`HouseholdStore.List` hard-codes JSON field names `'Kind'` and `'User'` as raw strings. These must stay in sync with the `[JsonPolymorphic(TypeDiscriminatorPropertyName = "Kind")]` attribute and the `Email` serialization. A single mismatch would silently break membership queries. Consider centralizing these names as constants.

### `string.IsNullOrEmpty("name")` latent bug
Noted in `CLAUDE.md` — the router checks the literal string `"name"` rather than the variable. This should be fixed and a test added for the empty-name route path.

### Test setup creates two households for all tests
`SetUp` always creates `_householdA` and `_householdB` with members, even for tests that only need one. Lazy/per-test setup or test fixtures scoped per test class would reduce overhead.
