using System.Collections.Immutable;
using Chorbar.Model;
using Microsoft.Extensions.Caching.Memory;

namespace Chorbar;

public class DemoHouseholdStore(IMemoryCache cache, IHttpContextAccessor httpContextAccessor)
    : IHouseholdStore
{
    public static readonly HouseholdId DemoHouseholdId = new(0);
    private static readonly Email _demoEmail = new("demo@chor.bar");
    private static readonly MemoryCacheEntryOptions _cacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromHours(4),
    };

    private string CacheKey => $"demo:{httpContextAccessor.HttpContext!.Session.Id}";

    public ValueTask<Household> Read(HouseholdId id, CancellationToken cancellationToken) =>
        ValueTask.FromResult(ReadHousehold());

    public ValueTask<Household> Write(
        HouseholdId id,
        HouseholdEventPayload payload,
        CancellationToken cancellationToken
    ) => Write(id, [payload], cancellationToken);

    public ValueTask<Household> Write(
        HouseholdId id,
        IEnumerable<HouseholdEventPayload> payloads,
        CancellationToken cancellationToken
    )
    {
        var household = ReadHousehold();
        foreach (var payload in payloads)
        {
            var now = DateTimeOffset.UtcNow;
            if (!payload.IsValid(household, now))
                throw new InvalidOperationException(
                    $"Event '{payload.EventKind}' not valid! ({payload})"
                );
            var evt = new HouseholdEvent(
                HouseholdId: DemoHouseholdId,
                Version: household.History.Length + 1,
                Timestamp: now,
                Payload: payload,
                CreatedBy: _demoEmail
            );
            household = evt.Apply(household);
        }
        cache.Set(CacheKey, household, _cacheOptions);
        return ValueTask.FromResult(household);
    }

    public IAsyncEnumerable<Household> List(CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public ValueTask<HouseholdId> Create(string name, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public ValueTask Delete(HouseholdId id, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    private Household ReadHousehold() =>
        cache.GetOrCreate(
            CacheKey,
            entry =>
            {
                entry.SetOptions(_cacheOptions);
                return CreateSeedHousehold();
            }
        )!;

    private static Household CreateSeedHousehold()
    {
        var now = DateTimeOffset.UtcNow;

        return new Household(
            Id: DemoHouseholdId,
            Name: "Demo Household",
            Creator: _demoEmail,
            Members: [_demoEmail],
            Chores: ImmutableDictionary<string, Chore>
                .Empty.Add(
                    "Vacuum living room",
                    new Chore(
                        Created: now.AddDays(-42),
                        History:
                        [
                            now.AddDays(-35),
                            now.AddDays(-28),
                            now.AddDays(-21),
                            now.AddDays(-14),
                            now.AddDays(-7),
                            now.AddDays(-1),
                        ],
                        Goal: new Goal(1, DateUnit.Week)
                    )
                )
                .Add(
                    "Clean bathroom",
                    new Chore(
                        Created: now.AddDays(-56),
                        History: [now.AddDays(-42), now.AddDays(-28), now.AddDays(-14)],
                        Goal: new Goal(2, DateUnit.Week)
                    )
                )
                .Add(
                    "Take out trash",
                    new Chore(
                        Created: now.AddDays(-18),
                        History:
                        [
                            now.AddDays(-15),
                            now.AddDays(-12),
                            now.AddDays(-9),
                            now.AddDays(-6),
                            now.AddDays(-3),
                        ],
                        Goal: new Goal(3, DateUnit.Day)
                    )
                )
                .Add(
                    "Water plants",
                    new Chore(
                        Created: now.AddDays(-28),
                        History: [now.AddDays(-21), now.AddDays(-14), now.AddDays(-5)],
                        Goal: new Goal(1, DateUnit.Week)
                    )
                )
                .Add(
                    "Mop kitchen floor",
                    new Chore(
                        Created: now.AddDays(-45),
                        History: [now.AddDays(-30)],
                        Goal: new Goal(2, DateUnit.Week)
                    )
                )
                .Add(
                    "Change bed sheets",
                    new Chore(
                        Created: now.AddDays(-60),
                        History:
                        [
                            now.AddDays(-46),
                            now.AddDays(-32),
                            now.AddDays(-18),
                            now.AddDays(-4),
                        ],
                        Goal: new Goal(2, DateUnit.Week)
                    )
                ),
            ShoppingListItems:
            [
                new ShoppingListItem(1, "Bananas", Category: "Produce", Order: 0),
                new ShoppingListItem(2, "Spinach", Category: "Produce", Order: 1),
                new ShoppingListItem(3, "Tomatoes", Category: "Produce", Order: 2),
                new ShoppingListItem(4, "Milk", Category: "Dairy", Order: 0),
                new ShoppingListItem(5, "Eggs", Category: "Dairy", Order: 1),
                new ShoppingListItem(6, "Butter", Category: "Dairy", Order: 2),
                new ShoppingListItem(7, "Rice", Category: "Pantry", Order: 0),
                new ShoppingListItem(8, "Pasta", Category: "Pantry", Order: 1),
                new ShoppingListItem(9, "Olive oil", Category: "Pantry", Order: 2),
                new ShoppingListItem(10, "Paper towels", Order: 0),
                new ShoppingListItem(11, "Dish soap", Order: 1),
            ],
            ShoppingListCategories: ["Produce", "Dairy", "Pantry"],
            Todos:
            [
                new TodoItem(1, "Fix leaky kitchen faucet", null, 1),
                new TodoItem(2, "Schedule dentist appointment", null, 2),
                new TodoItem(3, "Renew car insurance", null, 3),
                new TodoItem(4, "Organize garage", now.AddHours(-1), 4),
                new TodoItem(5, "Call electrician about porch light", now.AddHours(-1), 5),
            ],
            History: [],
            ShoppingListNextId: 12,
            ShoppingListEnabled: true,
            TodoListEnabled: true
        );
    }
}
