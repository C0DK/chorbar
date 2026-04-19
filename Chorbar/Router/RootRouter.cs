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
                return new PageResult(
                    new IndexPage(
                        chores: user.Chores.Select(ChoreCard)
                    )
                );
            }
        );
        app.MapPost(
            "/chore/add",
            async Task<IResult> (
                HttpContext context,
                UserStore store,
                [FromForm] string Label,
                CancellationToken cancellationToken
            ) =>
            {
                var user = await store.Write(Identity, new AddChore(Label), cancellationToken);
                // TODO: dont full redirect!
                return new PageResult(
                    new IndexPage(
                        chores: user.Chores.Select(ChoreCard)
                    )
                );
            }
        );
        app.MapPost(
            "/chore/do",
            async Task<IResult> (
                HttpContext context,
                UserStore store,
                [FromForm] string Label,
                CancellationToken cancellationToken
            ) =>
            {
                var user = await store.Write(Identity, new DoChore(Label), cancellationToken);
                // TODO: dont full redirect!
                return new PageResult(
                    new IndexPage(
                        chores: user.Chores.Select(ChoreCard)
                    )
                );
            }
        );
    }

    private static ChoreCard ChoreCard(KeyValuePair<string, Chore> chore) =>
        new ChoreCard(
            label: chore.Key,
            count: chore.Value.history.Count(),
            timeAgo: $"{(DateTimeOffset.UtcNow - chore.Value.history.LastOrDefault()).TotalMinutes:N0} Minutes"
        );

    private static Email Identity => new Email("c@cwb.dk");
}
