using Prometheus;

namespace Chorbar.Utils;

public sealed class EventMetrics
{
    private readonly Counter _eventsWritten = Metrics.CreateCounter(
        "chorbar_events_total",
        "Household events written, by kind.",
        new CounterConfiguration { LabelNames = ["event_kind"] }
    );

    public void Wrote(string eventKind) => _eventsWritten.WithLabels(eventKind).Inc();
}
