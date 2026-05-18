using Chorbar.Templates;
using Chorbar.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace Chorbar.Controllers;

[Route("/")]
public class HomeController(NpgsqlConnection connection, IMemoryCache cache) : Controller
{
    [HttpGet("")]
    public async ValueTask<IResult> Index(CancellationToken cancellationToken)
    {
        var stats = await cache.GetOrCreateAsync(
            "landingpage/stats",
            (_) => Stats(cancellationToken),
            new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) }
        );
        return new PageResult(
            new LandingPage(users: stats.users, chores: stats.chores, choreTimes: stats.activities)
        );
    }

    private async Task<(int users, int chores, int activities)> Stats(
        CancellationToken cancellationToken
    )
    {
        using var command = new NpgsqlCommand(
            //language=sql
            """
            SELECT
                COUNT(*) FILTER (WHERE payload ->> 'Kind' = 'add_chore') AS chores,
                COUNT(*) FILTER (WHERE payload ->> 'Kind' = 'do_chore') AS activities,
                COUNT(DISTINCT created_by) AS users
            FROM household_event
            """,
            connection
        );

        return await command.FirstAsync(
            async (r, c) =>
                (
                    users: await r.Get<int>("users", c),
                    chores: await r.Get<int>("chores", c),
                    activities: await r.Get<int>("activities", c)
                ),
            cancellationToken
        );
    }
}
