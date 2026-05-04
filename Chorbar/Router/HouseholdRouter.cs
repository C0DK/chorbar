using Chorbar.Model;
using Chorbar.Templates;
using Chorbar.Utils;
using Microsoft.AspNetCore.Mvc;

namespace Chorbar.Routes;

public static class HouseholdRouter
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet(
            "/",
            async Task<IResult> (
                HttpContext context,
                HouseholdStore store,
                CancellationToken cancellationToken
            ) =>
            {
                var households = await store.List(cancellationToken).ToArrayAsync();
                var selector = new HouseholdSelector(
                    households: households.Select(h => new HouseholdSelectorOption(
                        id: h.Id.ToString() ?? "id",
                        name: h.Name ?? "name"
                    ))
                );
                if (context.Request.Headers["HX-Target"].Contains("modal"))
                {
                    context.Response.Headers.Append("HX-Push-Url", "false");
                    return new ModalResult(selector);
                }

                return new PageResult(selector);
            }
        );
        group.MapGet(
            "/new",
            async Task<IResult> (
                HttpContext context,
                HouseholdStore store,
                CancellationToken cancellationToken
            ) =>
            {
                return new PageResult(new NewHousehold());
            }
        );
        group.MapPost(
            "/new",
            async Task<IResult> (
                HttpContext context,
                HouseholdStore store,
                [Microsoft.AspNetCore.Mvc.FromForm] string name,
                CancellationToken cancellationToken
            ) =>
            {
                if (string.IsNullOrEmpty(name))
                    // error?
                    return new PageResult(new NewHousehold());
                var id = await store.New(name, cancellationToken);

                return new HxRedirectResult($"/household/{id.Value}/");
            }
        );

        MapSpecificHousehold(group.MapGroup("/{householdId:int}/"));
    }

    public static void MapSpecificHousehold(IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/",
            async Task<IResult> (
                HttpContext context,
                HouseholdStore store,
                HouseholdId householdId,
                CancellationToken cancellationToken
            ) =>
            {
                var household = await store.Read(householdId, cancellationToken);
                return new PageResult(
                    new HouseholdPage(
                        chores: household
                            .Chores.OrderBy(c =>
                                c.Value.Deadline()
                                ?? (
                                    c.Value.History.Any() ? c.Value.History.Last() : c.Value.Created
                                )
                            )
                            .Select(ChoreCard)
                    ),
                    household.Name
                );
            }
        );
        app.MapGet(
            "/edit",
            async Task<IResult> (
                HttpContext context,
                HouseholdStore store,
                HouseholdId householdId,
                CancellationToken cancellationToken
            ) =>
            {
                var household = await store.Read(householdId, cancellationToken);
                return new PageResult(EditPage(household), household.Name);
            }
        );
        app.MapPost(
            "/edit",
            async Task<IResult> (
                HttpContext context,
                HouseholdStore store,
                HouseholdId householdId,
                [Microsoft.AspNetCore.Mvc.FromForm] string name,
                CancellationToken cancellationToken
            ) =>
            {
                var household = await store.Read(householdId, cancellationToken);
                if (string.IsNullOrEmpty(name))
                    return new PageResult(EditPage(household), household.Name);

                household = await store.Write(householdId, new Rename(name), cancellationToken);
                return new PageResult(EditPage(household), household.Name);
            }
        );
        app.MapPost(
            "/invite",
            async Task<IResult> (
                HttpContext context,
                HouseholdStore store,
                HouseholdId householdId,
                [Microsoft.AspNetCore.Mvc.FromForm] Email email,
                CancellationToken cancellationToken
            ) =>
            {
                var household = await store.Write(
                    householdId,
                    new AddMember(email),
                    cancellationToken
                );

                return new PageResult(EditPage(household), household.Name);
            }
        );
        app.MapPost(
            "/remove_member",
            async Task<IResult> (
                HttpContext context,
                HouseholdStore store,
                HouseholdId householdId,
                [Microsoft.AspNetCore.Mvc.FromForm] Email email,
                CancellationToken cancellationToken
            ) =>
            {
                var household = await store.Write(
                    householdId,
                    new RemoveMember(email),
                    cancellationToken
                );

                return new PageResult(EditPage(household), household.Name);
            }
        );
        app.MapGet(
            "/chore/",
            async Task<IResult> (
                HttpContext context,
                HouseholdId householdId,
                HouseholdStore store,
                string label,
                CancellationToken cancellationToken
            ) =>
            {
                var household = await store.Read(householdId, cancellationToken);
                var chore = household.Chores.GetValueOrDefault(label);
                if (chore is null)
                    return Results.NotFound();

                return new PartialResult(ChoreCard(label, chore));
            }
        );
        app.MapGet(
            "/chore/details",
            async Task<IResult> (
                HttpContext context,
                HouseholdId householdId,
                HouseholdStore store,
                string label,
                CancellationToken cancellationToken
            ) =>
            {
                var household = await store.Read(householdId, cancellationToken);
                var chore = household.Chores.GetValueOrDefault(label);
                if (chore is null)
                    return Results.NotFound();

                return new PartialResult(ChoreInfo(label, chore));
            }
        );
        app.MapPost(
            "/chore/goal",
            async Task<IResult> (
                HttpContext context,
                HouseholdStore store,
                HouseholdId householdId,
                [FromForm] string label,
                [FromForm] DateUnit unit,
                CancellationToken cancellationToken,
                [FromForm] int numerator
            ) =>
            {
                var household = await store.Write(
                    householdId,
                    new SetGoal(label, numerator, unit),
                    cancellationToken
                );

                var chore = household.Chores[label];
                return new PartialResult(ChoreInfo(label, chore));
            }
        );
        app.MapPost(
            "/chore/add",
            async Task<IResult> (
                HttpContext context,
                HouseholdStore store,
                HouseholdId householdId,
                [FromForm] string label,
                CancellationToken cancellationToken
            ) =>
            {
                var household = await store.Write(
                    householdId,
                    new AddChore(label),
                    cancellationToken
                );

                var chore = household.Chores[label];
                return new PartialResult(ChoreCard(label, chore));
            }
        );
        app.MapPost(
            "/chore/remove",
            async Task<IResult> (
                HttpContext context,
                HouseholdStore store,
                HouseholdId householdId,
                [FromForm] string label,
                CancellationToken cancellationToken
            ) =>
            {
                var household = await store.Write(
                    householdId,
                    new RemoveChore(label),
                    cancellationToken
                );

                return new PageResult(
                    new HouseholdPage(chores: household.Chores.Select(ChoreCard)),
                    household.Name
                );
            }
        );
        app.MapPost(
            "/chore/do",
            async Task<IResult> (
                HttpContext context,
                HouseholdStore store,
                HouseholdId householdId,
                [FromForm] string label,
                CancellationToken cancellationToken
            ) =>
            {
                var household = await store.Write(
                    householdId,
                    new DoChore(label),
                    cancellationToken
                );
                var chore = household.Chores[label];
                return new PartialResult(ChoreInfo(label, chore));
            }
        );
        app.MapPost(
            "/chore/undo",
            async Task<IResult> (
                HttpContext context,
                HouseholdStore store,
                HouseholdId householdId,
                [FromForm] string label,
                [FromForm] DateTimeOffset timestamp,
                CancellationToken cancellationToken
            ) =>
            {
                var household = await store.Write(
                    householdId,
                    new UndoChore(label, timestamp),
                    cancellationToken
                );

                var chore = household.Chores[label];
                return new PartialResult(ChoreInfo(label, chore));
            }
        );
    }

    private static EditHousehold EditPage(Household household) =>
        new EditHousehold(
            name: household.Name,
            members: household.Members.Select(m => new HouseholdMemberEntity(email: m.ToString()))
        );

    private static ChoreCard ChoreCard(KeyValuePair<string, Chore> chore) =>
        ChoreCard(chore.Key, chore.Value);

    private static ChoreCard ChoreCard(string label, Chore chore) =>
        new ChoreCard(
            label: label,
            count: chore.History.Count(),
            hasDone: chore.History.Count() > 0,
            timeAgo: TimeAgo(chore.History.LastOrDefault()),
            hasGoal: chore.Goal is not null,
            deadline: TimeUntil(chore.Deadline()),
            goalNumerator: chore.Goal?.Numerator,
            goalUnit: chore?.Goal?.Unit.ToString()
        );

    private static ChoreInfo ChoreInfo(string label, Chore chore) =>
        new ChoreInfo(
            label: label,
            actions: chore.History.Select(timestamp => new ChoreActivity(
                timeAgo: TimeAgo(timestamp),
                timestamp: timestamp.ToString("O"),
                label: label
            )),
            count: chore.History.Count(),
            hasDone: chore.History.Count() > 0,
            timeAgo: TimeAgo(chore.History.LastOrDefault()),
            hasGoal: chore.Goal is not null,
            deadline: TimeUntil(chore.Deadline()),
            goalNumerator: chore.Goal?.Numerator,
            goalUnit: chore?.Goal?.Unit.ToString()
        );

    private static string TimeAgo(DateTimeOffset? timestamp)
    {
        if (timestamp is null)
            return "never";
        var span = (DateTimeOffset.UtcNow - timestamp.Value);

        return span switch
        {
            { TotalSeconds: < 60 } => "just now",
            { TotalMinutes: < 2 } => "a few seconds ago",
            { TotalMinutes: < 121 } => $"{span.TotalMinutes:N0} minutes ago",
            { TotalHours: < 49 } => $"{span.TotalHours:N0} hours ago",
            { TotalDays: < 14 } => $"{span.TotalDays:N0} days ago",
            { TotalDays: < 200 } => $"{timestamp.Value:d MMMM}",
            _ => timestamp.Value.ToString("d MMMM yyyy"),
        };
    }

    private static string TimeUntil(DateTimeOffset? timestamp)
    {
        if (timestamp is null)
            return "never";
        var span = (timestamp.Value - DateTimeOffset.UtcNow);

        return span switch
        {
            { TotalSeconds: < 0 } => "now",
            { TotalSeconds: < 60 } => "now",
            { TotalMinutes: < 2 } => "in a few seconds",
            { TotalMinutes: < 121 } => $"in {span.TotalMinutes:N0} minutes",
            { TotalHours: < 49 } => $"in {span.TotalHours:N0} hours",
            { TotalDays: < 20 } => $"in {span.TotalDays:N0} days",
            { TotalDays: < 200 } => $"on {timestamp.Value:d MMMM}",
            _ => $"on {timestamp:d MMMM yyyy}",
        };
    }
}
