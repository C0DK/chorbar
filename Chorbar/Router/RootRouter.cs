using Chorbar.Model;
using Chorbar.Templates;
using Chorbar.Utils;
using Microsoft.AspNetCore.Mvc;

namespace Chorbar.Routes;

public static class RootRouter
{
    public static void Map(WebApplication app)
    {
        app.MapGet(
            "/",
            async Task<IResult> (
                HttpContext context,
                UserStore store,
                CancellationToken cancellationToken
            ) =>
            {
                var user = await store.Read(Identity, cancellationToken);
                return new PageResult(new IndexPage(chores: user.Chores.Select(ChoreCard)));
            }
        );
        app.MapGet(
            "/chore/",
            async Task<IResult> (
                HttpContext context,
                UserStore store,
                string label,
                CancellationToken cancellationToken
            ) =>
            {
                var user = await store.Read(Identity, cancellationToken);
                var chore = user.Chores.GetValueOrDefault(label);
                if (chore is null)
                    return Results.NotFound();

                return new PartialResult(ChoreCard(label, chore));
            }
        );
        app.MapGet(
            "/chore/details",
            async Task<IResult> (
                HttpContext context,
                UserStore store,
                string label,
                CancellationToken cancellationToken
            ) =>
            {
                var user = await store.Read(Identity, cancellationToken);
                var chore = user.Chores.GetValueOrDefault(label);
                if (chore is null)
                    return Results.NotFound();

                return new PartialResult(
                    new ChoreInfo(
                        label: label,
                        actions: chore.history.Select(timestamp =>
                            $"<li>Done {TimeAgo(timestamp)}</li>"
                        ),
                        count: chore.history.Count(),
                        timeAgo: TimeAgo(chore.history.LastOrDefault())
                    )
                );
            }
        );
        app.MapPost(
            "/chore/add",
            async Task<IResult> (
                HttpContext context,
                UserStore store,
                [FromForm] string label,
                CancellationToken cancellationToken
            ) =>
            {
                var user = await store.Write(Identity, new AddChore(label), cancellationToken);

                var chore = user.Chores[label];
                return new PartialResult(ChoreCard(label, chore));
            }
        );
        app.MapPost(
            "/chore/remove",
            async Task<IResult> (
                HttpContext context,
                UserStore store,
                [FromForm] string label,
                CancellationToken cancellationToken
            ) =>
            {
                var user = await store.Write(Identity, new RemoveChore(label), cancellationToken);

                return new PageResult(new IndexPage(chores: user.Chores.Select(ChoreCard)));
            }
        );
        app.MapPost(
            "/chore/do",
            async Task<IResult> (
                HttpContext context,
                UserStore store,
                [FromForm] string label,
                CancellationToken cancellationToken
            ) =>
            {
                var user = await store.Write(Identity, new DoChore(label), cancellationToken);
                var chore = user.Chores[label];
                return new PartialResult(
                    new ChoreInfo(
                        label: label,
                        actions: chore.history.Select(timestamp =>
                            $"<li>Done {TimeAgo(timestamp)}</li>"
                        ),
                        count: chore.history.Count(),
                        timeAgo: TimeAgo(chore.history.LastOrDefault())
                    )
                );
            }
        );
    }

    private static ChoreCard ChoreCard(KeyValuePair<string, Chore> chore) =>
        ChoreCard(chore.Key, chore.Value);

    private static ChoreCard ChoreCard(string label, Chore chore) =>
        new ChoreCard(
            label: label,
            count: chore.history.Count(),
            // TODO: better time!
            timeAgo: TimeAgo(chore.history.LastOrDefault())
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
            { TotalDays: < 7 } => $"{span.TotalHours:N0} days ago",
            _ => timestamp.Value.ToString("yyyy-MM-dd HH:MM"),
        };
    }

    private static Email Identity => new Email("c@cwb.dk");
}
