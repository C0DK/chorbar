# Chorbar

Chore-tracking app. Households own chores. Members track completions.

## Stack
- **Runtime**: .NET 10 / ASP.NET Core minimal APIs
- **DB**: PostgreSQL via Npgsql (raw SQL, no ORM)
- **Templates**: Strongbars (compile-time HTML, `Chorbar/Templates/`)
- **Frontend**: HTMX + partial results (`PartialResult`, `ModalResult`, `HxRedirectResult`)
- **Auth**: Cookie auth, hardcoded identity (`c@cwb.dk`) during dev

## Architecture: Event Sourcing
All state lives in `household_event` table. No mutable rows.

- `HouseholdStore` reads/writes events, folds into `Household`
- `HouseholdEventPayload` = discriminated union: `CreateNewHousehold | AddChore | DoChore | UndoChore | ...`
- `Household` = folded view (record, immutable)
- `HouseholdId` = strongly-typed int wrapper — must have `TryParse(string)` for route binding

## Key Types
| Type | Role |
|------|------|
| `HouseholdId` | Route-bindable int wrapper (`Model/HouseholdId.cs`) |
| `Email` | Route/form-bindable string wrapper |
| `HouseholdStore` | All DB access |
| `RootRouter` | All HTTP routes (`/household/{householdId:int}/...`) |

## Routing
```
GET  /household/                → list households
GET  /household/new             → new household form
POST /household/new             → create household
GET  /household/{id}/           → household page
GET  /household/{id}/edit       → edit page
POST /household/{id}/invite     → add member
POST /household/{id}/remove_member
POST /household/{id}/chore/add|remove|do|undo|goal
```

## Tests
`Chorbar.Tests/` — uses real DB via `DatabaseFixture`. No mocking.

## Known Gotchas
- `HouseholdId.TryParse(string)` — inverted logic bug was fixed (missing `!` on `int.TryParse`)
- `string.IsNullOrEmpty("name")` in router = always checks literal, not variable (latent bug)
