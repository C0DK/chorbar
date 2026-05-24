using Prometheus;

namespace Chorbar.Utils;

public static class AppMetrics
{
    // -- Domain events (chore activity etc.) --------------------------------
    public static readonly Counter EventsWritten = Metrics.CreateCounter(
        "chorbar_events_total",
        "Household events written, by kind.",
        new CounterConfiguration { LabelNames = ["event_kind"] }
    );

    // -- Auth funnel --------------------------------------------------------
    public static readonly Counter OtpSent = Metrics.CreateCounter(
        "chorbar_otp_sent_total",
        "OTP emails requested."
    );

    public static readonly Counter OtpVerified = Metrics.CreateCounter(
        "chorbar_otp_verified_total",
        "OTP verification attempts, by outcome.",
        new CounterConfiguration { LabelNames = ["outcome"] }
    );

    // -- Mail delivery ------------------------------------------------------
    public static readonly Counter MailSent = Metrics.CreateCounter(
        "chorbar_mail_sent_total",
        "Emails sent via the mail provider.",
        new CounterConfiguration { LabelNames = ["outcome"] }
    );

    // DB pool stats: Npgsql 8+ publishes pool metrics via
    // System.Diagnostics.Metrics (meter "Npgsql"), and prometheus-net 8.x
    // auto-collects them as db_client_connections_* gauges.
}
