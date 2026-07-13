namespace Chorbar.Model;

public record UserEvent(
    Email Email,
    int Version,
    DateTimeOffset Timestamp,
    UserEventPayload Payload
)
{
    public UserInfo Apply(UserInfo user) => Payload.Apply(user, Timestamp);

    public UserSettings Apply(UserSettings user) =>
        Payload.Apply(user, Timestamp) with
        {
            History = user.History.Add(this),
        };

    public override string ToString() => $"{Payload}[{Version}|{Timestamp:s}|{Email}]";
}
