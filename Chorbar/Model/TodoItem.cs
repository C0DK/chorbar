namespace Chorbar.Model;

public record TodoItem(int Id, string Label, DateTimeOffset? Done, int Order = -1)
{
    public bool IsDone => Done is not null;

    public bool CheckedOffRecently(TimeProvider timeProvider) =>
        Done >= timeProvider.GetUtcNow() - TimeSpan.FromHours(2);
}
