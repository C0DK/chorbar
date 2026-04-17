namespace Chorbar.Model;

public record UserEvent(
    Email Identity,
    int Version,
    DateTimeOffset Timestamp,
    UserEventPayload Payload
)
{
    public User Apply(User user) =>
        Payload.Apply(user, Timestamp) with
        {
            History = user.History.Add(this),
        };
}
