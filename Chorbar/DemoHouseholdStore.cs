using System.Collections.Immutable;
using Chorbar.Model;
using Microsoft.Extensions.Caching.Memory;

namespace Chorbar;

public class DemoHouseholdStore(IMemoryCache cache, IHttpContextAccessor httpContextAccessor)
{
    private static readonly HouseholdId _demoId = new(0);
    private static readonly Email _demoEmail = new("demo@chor.bar");
    private static readonly MemoryCacheEntryOptions _cacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromHours(4),
    };

    private string CacheKey => $"demo:{httpContextAccessor.HttpContext!.Session.Id}";

    public Household Read()
    {
        return cache.GetOrCreate(
            CacheKey,
            entry =>
            {
                entry.SetOptions(_cacheOptions);
                return CreateSeedHousehold();
            }
        )!;
    }

    public Household Write(HouseholdEventPayload payload)
    {
        var household = Read();
        var now = DateTimeOffset.UtcNow;
        if (!payload.IsValid(household, now))
            throw new InvalidOperationException(
                $"Event '{payload.EventKind}' not valid! ({payload})"
            );
        var evt = new HouseholdEvent(
            HouseholdId: _demoId,
            Version: household.History.Length + 1,
            Timestamp: now,
            Payload: payload,
            CreatedBy: _demoEmail
        );
        household = evt.Apply(household);
        cache.Set(CacheKey, household, _cacheOptions);
        return household;
    }

    public Household Write(IEnumerable<HouseholdEventPayload> payloads)
    {
        var household = Read();
        foreach (var payload in payloads)
        {
            var now = DateTimeOffset.UtcNow;
            if (!payload.IsValid(household, now))
                throw new InvalidOperationException(
                    $"Event '{payload.EventKind}' not valid! ({payload})"
                );
            var evt = new HouseholdEvent(
                HouseholdId: _demoId,
                Version: household.History.Length + 1,
                Timestamp: now,
                Payload: payload,
                CreatedBy: _demoEmail
            );
            household = evt.Apply(household);
        }
        cache.Set(CacheKey, household, _cacheOptions);
        return household;
    }

    private static Household CreateSeedHousehold()
    {
        var now = DateTimeOffset.UtcNow;

        return new Household(
            Id: _demoId,
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
            ShoppingListItems: [],
            ShoppingListCategories: [],
            Todos: [],
            History: []
        );
    }
}
