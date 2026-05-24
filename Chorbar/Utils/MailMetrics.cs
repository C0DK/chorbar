using Prometheus;

namespace Chorbar.Utils;

public sealed class MailMetrics
{
    private readonly Counter _mailSent = Metrics.CreateCounter(
        "chorbar_mail_sent_total",
        "Emails sent via the mail provider.",
        new CounterConfiguration { LabelNames = ["outcome"] }
    );

    public void Sent() => _mailSent.WithLabels("ok").Inc();

    public void Failed() => _mailSent.WithLabels("error").Inc();
}
