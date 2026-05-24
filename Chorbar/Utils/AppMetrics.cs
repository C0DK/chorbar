using Npgsql;
using Prometheus;

namespace Chorbar.Utils;

public static class AppMetrics
{
    // -- 2. Domain events (chore activity etc.) --------------------------------
    public static readonly Counter EventsWritten = Metrics.CreateCounter(
        "chorbar_events_total",
        "Household events written, by kind.",
        new CounterConfiguration { LabelNames = ["event_kind"] }
    );

    // -- 3. Auth funnel --------------------------------------------------------
    public static readonly Counter OtpSent = Metrics.CreateCounter(
        "chorbar_otp_sent_total",
        "OTP emails requested."
    );

    public static readonly Counter OtpVerified = Metrics.CreateCounter(
        "chorbar_otp_verified_total",
        "OTP verification attempts, by outcome.",
        new CounterConfiguration { LabelNames = ["outcome"] }
    );

    // -- 4. Mail delivery ------------------------------------------------------
    public static readonly Counter MailSent = Metrics.CreateCounter(
        "chorbar_mail_sent_total",
        "Emails sent via the mail provider.",
        new CounterConfiguration { LabelNames = ["outcome"] }
    );

    // -- 5. DB connection pool -------------------------------------------------
    private static readonly Gauge DbPoolTotal = Metrics.CreateGauge(
        "chorbar_db_pool_total",
        "Total connections in the Npgsql pool."
    );

    private static readonly Gauge DbPoolIdle = Metrics.CreateGauge(
        "chorbar_db_pool_idle",
        "Idle connections in the Npgsql pool."
    );

    private static readonly Gauge DbPoolBusy = Metrics.CreateGauge(
        "chorbar_db_pool_busy",
        "Busy (in-use) connections in the Npgsql pool."
    );

    public static void RegisterDbPoolStats(NpgsqlDataSource dataSource)
    {
        Metrics.DefaultRegistry.AddBeforeCollectCallback(() =>
        {
            var stats = dataSource.Statistics;
            DbPoolTotal.Set(stats.Total);
            DbPoolIdle.Set(stats.Idle);
            DbPoolBusy.Set(stats.Busy);
        });
    }
}
